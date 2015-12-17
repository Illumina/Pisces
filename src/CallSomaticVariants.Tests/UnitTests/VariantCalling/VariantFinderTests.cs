using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Logic.VariantCalling;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.UnitTests.Models;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.VariantCalling
{
    public enum CandidateVariantType
    {
        Snv, Mnv, Insertion, Deletion, Reference
    }
    public class CandidateVariantsTest
    {
        public string RefChromosome { get; set; }
        public string CigarString { get; set; }
        public string ReadBases { get; set; }
        public CandidateVariantTestExpectations Expectations { get; set; }
        public byte[] ReadQualities { get; set; }
        public int MaxLengthMnv { get; set; }
        public int MaxLengthInterveningRef { get; set; }

        public CandidateVariantsTest(int position, string refChromosome, string cigarString, string readBases, byte[] readQualities, int maxLengthMnv = 20, int maxLengthInterveningRef = 2, CandidateVariantTestExpectations expectations = null)
        {
            var numPrefixClip = new CigarAlignment(cigarString).GetPrefixClip();
            RefChromosome = String.Concat(Enumerable.Repeat("N", position - 1 - (int)numPrefixClip)) + refChromosome + "NNNNN";
            CigarString = cigarString;
            ReadBases = readBases;
            ReadQualities = readQualities;
            MaxLengthInterveningRef = maxLengthInterveningRef;
            MaxLengthMnv = maxLengthMnv;
            Expectations = expectations;
        }
    }

    public class ExpectedVariant
    {
        public readonly int CoordExpected;
        public readonly string RefExpected;
        public readonly string AltExpected;
        public readonly CandidateVariantType TypeExpected;

        public ExpectedVariant(CandidateVariantType typeExpected, string refExpected, string altExpected, int coordExpected)
        {
            CoordExpected = coordExpected;
            RefExpected = refExpected;
            AltExpected = altExpected;
            TypeExpected = typeExpected;
        }
    }

    public class CandidateVariantTestExpectations
    {
        public int NumVariantsExpected { get; set; }
        public List<ExpectedVariant> ExpectedVariants { get; set; }
        public CandidateVariantTestExpectations(int numVariantsExpected, CandidateVariantType typeExpected, string refExpected, string altExpected, int coordExpected)
        {
            ExpectedVariants = new List<ExpectedVariant>();
            NumVariantsExpected = numVariantsExpected;
            ExpectedVariants.Add(new ExpectedVariant(typeExpected,refExpected,altExpected,coordExpected));
        }
        public CandidateVariantTestExpectations(int numVariantsExpected, List<ExpectedVariant> expectedVariants)
        {
            ExpectedVariants = expectedVariants;
            NumVariantsExpected = numVariantsExpected;
        }
    }

    //Where T is the alignment source - either AlignmentSet for us, or BamAlignment for Pisces
    //Where U is the variant output
    //Where V is the place where you'll find the variants
    public interface IVariantFromCigarSuite<T, U, V>
    {
        IEnumerable<CandidateVariantsTest> Tests { get; set; }
        T BuildReadsToCheckForVariants(CandidateVariantsTest test);
        IEnumerable<U> GetVariants(CandidateVariantsTest test, T reads);
    }

    public abstract class VariantFromCigarSuite<T, U, V> : IVariantFromCigarSuite<T, U, V>
    {
        protected string _chromName = "chr17";
        protected int _readStartPos = 1234567;
        protected int _qualityCutoff = 20;
        protected int _maxLengthMnv = 20;
        protected int _maxLengthInterveningRef = 2;

        public IEnumerable<CandidateVariantsTest> Tests { get; set; }

        protected VariantFromCigarSuite()
        {
            Tests = new List<CandidateVariantsTest>();
        }
        public abstract T BuildReadsToCheckForVariants(CandidateVariantsTest test);
        public abstract IEnumerable<U> GetVariants(CandidateVariantsTest test, T reads);

        public abstract void CheckVariantBasicInfo(CandidateVariantsTest test, IEnumerable<U> results, bool refExpected = false);

        private void ExecuteTest(CandidateVariantsTest candidateVariantsTest)
        {
            var reads = BuildReadsToCheckForVariants(candidateVariantsTest);
            var variants = GetVariants(candidateVariantsTest, reads);
            CheckVariantBasicInfo(candidateVariantsTest, variants);
        }

        public void SnvTests()
        {
            var typeExpected = CandidateVariantType.Snv;

            var cigarString = "1M";
            var variantPositionInRead = 0;
            byte[] goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            var badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            var badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            var goodOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });

            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "G", cigarString, "T", goodQualities)
                {
                    Expectations = new CandidateVariantTestExpectations(1, typeExpected, "G", "T", _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "G", cigarString, "T", badQualities)
                {
                    Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "G", cigarString, "G", goodQualities)
                {
                    Expectations =
                        new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "G", cigarString, "T", goodOnlyAtMySite)
                {
                    Expectations = new CandidateVariantTestExpectations(1, typeExpected, "G", "T", _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "G", cigarString, "T", badOnlyAtMySite)
                {
                    Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });

            cigarString = "2M";
            variantPositionInRead = 1;
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            goodOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AT", goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, "G", "T", _readStartPos + variantPositionInRead)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AT", badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos + variantPositionInRead)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AT", goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, "G", "T", _readStartPos + variantPositionInRead)
            });

            //An SNV that is actually an "N" should not be emitted as an SNV
            cigarString = "2M";
            variantPositionInRead = 1;
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            goodOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AN", goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, "G", "N", _readStartPos + variantPositionInRead)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AN", badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos + variantPositionInRead)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, "AG", cigarString, "AN", goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, "G", "N", _readStartPos + variantPositionInRead)
            });


        }

        public void MnvTests()
        {
            var typeExpected = CandidateVariantType.Mnv;

            var cigarString = "3M";
            var variantPositionInRead = 0;
            byte[] goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            var badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            var badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff,
                _qualityCutoff - 1, new[] {variantPositionInRead, variantPositionInRead + 1, variantPositionInRead + 2});
            var goodOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1,
                _qualityCutoff, new[] {variantPositionInRead, variantPositionInRead + 1, variantPositionInRead + 2});

            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCC", cigarString, "TAA", goodQualities)
                {
                    Expectations = new CandidateVariantTestExpectations(1, typeExpected, "GCC", "TAA", _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCC", cigarString, "TAA", badQualities)
                {
                    Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCC", cigarString, "GCC", goodQualities)
                {
                    Expectations =
                        new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCC", cigarString, "TAA", goodOnlyAtMySite)
                {
                    Expectations = new CandidateVariantTestExpectations(1, typeExpected, "GCC", "TAA", _readStartPos)
                });
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCC", cigarString, "TAA", badOnlyAtMySite)
                {
                    Expectations = new CandidateVariantTestExpectations(0, typeExpected, null, null, _readStartPos)
                });

            cigarString = "5M";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            //1 intervening ref - if we allow intervening refs, should get the full 5-base variant
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCCTA", cigarString, "TAATC", goodQualities)
                {
                    Expectations =
                        new CandidateVariantTestExpectations(1, typeExpected, "GCCTA", "TAATC", _readStartPos)
                });
            //2 refs on the end - should only get the 3-base mnv
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "GCCGG", cigarString, "TAAGG", goodQualities)
                {
                    Expectations = new CandidateVariantTestExpectations(1, typeExpected, "GCC", "TAA", _readStartPos)
                });

            //Intervening Ref Tests
            //If we have variants that are broken up by more refs than allowed, should output as individual SNV/MNVs (if we output refs, should output as SNVs + Refs)

            //2 MNVs separated by refs
            var allTRef = "TTT" + "TTTT" + "TTT";
            var fourTsepAs = "AAA" + "TTTT" + "GGG";
            cigarString = "10M";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            
            var leftSideMnv = new ExpectedVariant(typeExpected, "TTT", "AAA", _readStartPos);
            var rightSideMnv = new ExpectedVariant(typeExpected, "TTT", "GGG", _readStartPos + 7);

            var splitMnvExpectations = new CandidateVariantTestExpectations(2, new List<ExpectedVariant>{leftSideMnv,rightSideMnv});
            var combinedMnvExpectations = new CandidateVariantTestExpectations(1, new List<ExpectedVariant>()
            {
                new ExpectedVariant(typeExpected, allTRef, fourTsepAs, _readStartPos),
            });

            //2 MNVs separated by many more refs than threshold - should yield 2 mnvs
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, allTRef, cigarString, fourTsepAs,
                    goodQualities, 20, 2, splitMnvExpectations));

            //2 MNVs separated by less refs than threshold - should yield one long mnv
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, allTRef, cigarString, fourTsepAs,
                    goodQualities, 20, 5, combinedMnvExpectations));

            //2 MNVs separated by exactly as many refs as threshold - should yield one long mnv
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, allTRef, cigarString, fourTsepAs,
                    goodQualities, 20, 4, combinedMnvExpectations));


            //2 MNVs separated by one more ref than threshold - should yield 2 mnvs
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, allTRef, cigarString, fourTsepAs,
                    goodQualities, 20, 3, splitMnvExpectations));


            //1 SNV and 1 MNV separated by Refs
            leftSideMnv = new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos);
            rightSideMnv = new ExpectedVariant(typeExpected, "TTTT", "GGGG", _readStartPos + 6);

            splitMnvExpectations = new CandidateVariantTestExpectations(2, new List<ExpectedVariant> { leftSideMnv, rightSideMnv });
            combinedMnvExpectations = new CandidateVariantTestExpectations(1, new List<ExpectedVariant>
            {
                new ExpectedVariant(typeExpected, "T" + "TTTTT" + "TTTT", "A" + "TTTTT" + "GGGG", _readStartPos)
            });

            //1 SNV and 1 MNV separated by more refs than threshold - should yield 1 SNV and 1 MNV
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "T" + "TTTTT" + "TTTT", cigarString, "A" + "TTTTT" + "GGGG",
                    goodQualities, 20, 4, splitMnvExpectations));

            //1 SNV and 1 MNV separated by as many refs as threshold - should yield one long MNV
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "T" + "TTTTT" + "TTTT", cigarString, "A" + "TTTTT" + "GGGG",
                    goodQualities, 20, 5, combinedMnvExpectations));

            //1 SNV and 1 MNV separated by less refs than threshold - should yield one long MNV
            ExecuteTest(
                new CandidateVariantsTest(_readStartPos, "T" + "TTTTT" + "TTTT", cigarString, "A" + "TTTTT" + "GGGG",
                    goodQualities, 20, 6, combinedMnvExpectations));

            //1 continuous MNV followed by all reference - should yield one MNV
            //1 SNV followed by all reference - should yield one SNV

            //3-piece MNV
            var threePieceRef = "T" + "TTT" + "T" + "TTT" + "TT";
            var threePieceMnv = "A" + "TTT" + "G" + "TTT" + "CC";
            cigarString = "10M";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            leftSideMnv = new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos);
            var middleMnv = new ExpectedVariant(CandidateVariantType.Snv, "T", "G", _readStartPos + 4);
            rightSideMnv = new ExpectedVariant(CandidateVariantType.Mnv, "TT", "CC", _readStartPos + 8);
            
            splitMnvExpectations = new CandidateVariantTestExpectations(3, new List<ExpectedVariant> { leftSideMnv, middleMnv, rightSideMnv });

            combinedMnvExpectations = new CandidateVariantTestExpectations(1, new List<ExpectedVariant>
            {
                new ExpectedVariant(typeExpected, threePieceRef, threePieceMnv, _readStartPos)
            });

            //3-piece MNV separated by refs below threshold - should yield one long mnv
            ExecuteTest(
              new CandidateVariantsTest(_readStartPos, threePieceRef, cigarString, threePieceMnv,
                  goodQualities, 20, 5, combinedMnvExpectations));

            //3-piece MNV separated by refs above threshold - should yield 3 separate variants
            ExecuteTest(
              new CandidateVariantsTest(_readStartPos, threePieceRef, cigarString, threePieceMnv,
                  goodQualities, 20, 2, splitMnvExpectations));

            //3-piece MNV separated by refs at threshold - should yield one long mnv
            ExecuteTest(
              new CandidateVariantsTest(_readStartPos, threePieceRef, cigarString, threePieceMnv,
                  goodQualities, 20, 3, combinedMnvExpectations));

            //3-piece MNV where first two are separated by refs above threshold
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "T" + "TTTT" + "T" + "TT" + "TT", cigarString, "A" + "TTTT" + "G" + "TT" + "CC",
              goodQualities, 20, 3, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                leftSideMnv,
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTT", "GTTCC", _readStartPos + 5),
            })));

            //3-piece MNV where second two are separated by refs above threshold
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "T" + "TT" + "T" + "TTTT" + "TT", cigarString, "A" + "TT" + "G" + "TTTT" + "CC",
              goodQualities, 20, 3, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTT", "ATTG", _readStartPos),
                rightSideMnv
            })));

            //At or above MNV length threshold
            //One base longer than threshold - should yield MNV + SNV
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT"+"TTTT"+"TTT", cigarString, "AAA"+"AAAA"+"AAA",
              goodQualities, 9, 3, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT"+"TTTT"+"TT", "AAA"+"AAAA"+"AA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos + 9),
            })));

            //More than one base longer than threshold - should yield MNV + MNV
            ExecuteTest(new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AAA" + "AAAA" + "AAA",
              goodQualities, 8, 3, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT"+"TTTT"+"T", "AAA"+"AAAA"+"A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TT", "AA", _readStartPos + 8),
            })));
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AAA" + "AAAA" + "AAA",
              goodQualities, 6, 3, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT" + "TTT", "AAA"+"AAA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTT", "AAAA", _readStartPos + 6),
            })));

            //Many times longer than threshold - should yield as many MNVs of threshold length as fit in original MNV + leftover
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AAA" + "AAAA" + "AAA",
              goodQualities, 3, 3, new CandidateVariantTestExpectations(4, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT", "AAA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT", "AAA", _readStartPos+3),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT", "AAA", _readStartPos+6),
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos + 9),
            })));

            //Intervening ref right at length threshold should not contribute to MNV - it should be cut off the base before and the ref should stand alone
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AA" + "T" + "AAAA" + "AAA",
              goodQualities, 3, 3, new CandidateVariantTestExpectations(4, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TT", "AA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT", "AAA", _readStartPos+3),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTT", "AAA", _readStartPos+6),
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos + 9),
            })));

            //If there are Ns, end the MNV 
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AA" + "N" + "AAAA" + "AAA",
              goodQualities, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TT", "AA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTTT", "AAAAAAA", _readStartPos+3),
            })));
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ANA" + "N" + "AAA" + "AAA",
  goodQualities, 20, 20, new CandidateVariantTestExpectations(3, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos+2),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTT", "AAAAAA", _readStartPos+4),
            })));
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ANT" + "N" + "AAA" + "AAA",
  goodQualities, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTT", "AAAAAA", _readStartPos+4),
            })));
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ATN" + "A" + "AAA" + "AAA",
  goodQualities, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTTT", "AAAAAAA", _readStartPos+3),
            })));


            //If the quality drops off in the middle of the MNV, end it (same way we would for an N at that spot)
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { 2 });
            ExecuteTest(
            new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "AA" + "C" + "AAAA" + "AAA",
              badOnlyAtMySite, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Mnv, "TT", "AA", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTTT", "AAAAAAA", _readStartPos+3),
            })));
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { 1,3 });
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ACA" + "C" + "AAA" + "AAA",
  badOnlyAtMySite, 20, 20, new CandidateVariantTestExpectations(3, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos+2),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTT", "AAAAAA", _readStartPos+4),
            })));
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { 1,3 });
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ACT" + "C" + "AAA" + "AAA",
  badOnlyAtMySite, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTT", "AAAAAA", _readStartPos+4),
            })));
            badOnlyAtMySite = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { 2 });
            ExecuteTest(
new CandidateVariantsTest(_readStartPos, "TTT" + "TTTT" + "TTT", cigarString, "ATC" + "A" + "AAA" + "AAA",
  badOnlyAtMySite, 20, 20, new CandidateVariantTestExpectations(2, new List<ExpectedVariant>
            {
                new ExpectedVariant(CandidateVariantType.Snv, "T", "A", _readStartPos),
                new ExpectedVariant(CandidateVariantType.Mnv, "TTTTTTT", "AAAAAAA", _readStartPos+3),
            })));

        }

        public void DeletionTests()
        {
            var typeExpected = CandidateVariantType.Deletion;

            var cigarString = "1M1D1M";
            var variantPositionInRead = 1;
            var refRead = "GCT";
            var altRead = "GT";
            var refAllele = "GC";
            var altAllele = "G";
            var goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            var badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            var badOnlyAtLeftOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead - 1 });
            var badOnlyAtRightOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            var badEverywhereExceptBookendsOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead - 1, variantPositionInRead });


            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtLeftOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtRightOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            cigarString = "3M2D2M";
            variantPositionInRead = 3;
            refRead = "AAGACTA";
            altRead = "AAGTA";
            refAllele = "GAC";
            altAllele = "G";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtLeftOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead - 1 });
            badOnlyAtRightOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            badEverywhereExceptBookendsOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead - 1, variantPositionInRead });


            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtLeftOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtRightOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badEverywhereExceptBookendsOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion at the very beginning of the read
            cigarString = "2D2M";
            variantPositionInRead = 0;
            refRead = "ACTA";
            altRead = "TA";
            refAllele = "NAC";
            altAllele = "N";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtRightOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtRightOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion at the very beginning of the read with softclip
            cigarString = "3S2D2M";
            variantPositionInRead = 0;
            refRead = "YYYACTA";
            altRead = "ZZZTA";
            refAllele = "YAC";
            altAllele = "Y";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion at the very end of the read
            cigarString = "2M2D";
            variantPositionInRead = 2;
            refRead = "ACTA";
            altRead = "AC";
            refAllele = "CTA";
            altAllele = "C";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtLeftOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead -1 });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtLeftOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion at the very end of the read with softclip
            cigarString = "2M2D3S";
            variantPositionInRead = 2;
            refRead = "ACTAZZZ";
            altRead = "ACYYY";
            refAllele = "CTA";
            altAllele = "C";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            badOnlyAtLeftOfDel = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead - 1 });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtLeftOfDel)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion is the only operation in read, except for softclip
            cigarString = "2S2D";
            variantPositionInRead = 0;
            refRead = "ZZAC";
            altRead = "YY";
            refAllele = "ZAC";
            altAllele = "Z";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            //Deletion with SNV right before it should still show preceding ref base as alt (or should it?)
            cigarString = "3M2D2M";
            variantPositionInRead = 3;
            refRead = "AAGACTA";
            altRead = "AATTA";
            refAllele = "GAC";
            altAllele = "G";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(2, new List<ExpectedVariant>()
                {
                    new ExpectedVariant(CandidateVariantType.Snv,"G","T",_readStartPos +variantPositionInRead - 1),
                    new ExpectedVariant(CandidateVariantType.Deletion, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)

                })
            });

            //Softclip shouldn't mess things up
            cigarString = "3S3M2D2M";
            variantPositionInRead = 3; // take into account soft clip
            refRead = "ZZZAAGACTA";
            altRead = "YYYAATTA";
            refAllele = "GAC";
            altAllele = "G";

            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(2, new List<ExpectedVariant>()
                {
                    new ExpectedVariant(CandidateVariantType.Snv,"G","T",_readStartPos + variantPositionInRead - 1),
                    new ExpectedVariant(CandidateVariantType.Deletion, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)

                })
            });

            cigarString = "24S78M15D3M1D45M";
            variantPositionInRead = 78;
            refRead = "GGACAGCATCAAATCATCCATTGCTTGGGACGGCAAGGGGGACTGTAGATGGGTGAAAAGAGCAGTCAGAGGACCAGGTCATCAGCCCCCCAGCCCCCCAGC" + "CCTCCAGGTCCCCAG" + "CCC" + "T" + "CCAGGTCCCCAGCCCAACCCTTGTACTTACCAGAACGTTGTTTTC";
            altRead = "GGACAGCATCAAATCATCCATTGCTTGGGACGGCAAGGGGGACTGTAGATGGGTGAAAAGAGCAGTCAGAGGACCAGGTCATCAGCCCCCCAGCCCCCCAGC"+                      "CCC"+        "CCAGGTCCCCAGCCCAACCCTTGTACTTACCAGAACGTTGTTTTC";
            refAllele = "CT";
            altAllele = "C";

            _readStartPos = 7579573;
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(2, new List<ExpectedVariant>()
                {
                    new ExpectedVariant(CandidateVariantType.Deletion,"CCCTCCAGGTCCCCAG","C",_readStartPos + variantPositionInRead - 1),
                    new ExpectedVariant(CandidateVariantType.Deletion, refAllele, altAllele, _readStartPos + variantPositionInRead + 15 + 2) // skip 15D to next variant position

                })
            });

        }

        public void InsertionTests()
        {
            var typeExpected = CandidateVariantType.Insertion;

            var cigarString = "1M1I1M";
            var variantPositionInRead = 1;
            var refRead = "GT";
            var altRead = "GCT";
            var refAllele = "G";
            var altAllele = "GC";
            var goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            var badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            var goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });
            var badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos)
            });

            cigarString = "3M1I1M";
            variantPositionInRead = 3;
            refRead = "AAGT";
            altRead = "AAGCT";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            // Insertion at the beginning of the read.
            cigarString = "1I4M";
            variantPositionInRead = 0;
            refRead = "AAGT";
            altRead = "CAAGT";
            refAllele = "N";
            altAllele = "NC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            // Insertion at the beginning of the read with soft clipping.
            cigarString = "5S1I4M";
            variantPositionInRead = 0;
            refRead = "TTTTTAAGT";
            altRead = "TTTTTCAAGT";
            refAllele = "T";
            altAllele = "TC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead + 5 });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead + 5 });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            // Insertion at the end of the read.
            cigarString = "4M1I";
            variantPositionInRead = 4;
            refRead = "CAAG";
            altRead = "CAAGT";
            refAllele = "G";
            altAllele = "GT";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            // Insertion at the end of the read with soft clipping.
            cigarString = "4M1I5S";
            variantPositionInRead = 4;
            refRead = "AAGTTTTTT";
            altRead = "AAGTCTTTTT";
            refAllele = "T";
            altAllele = "TC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead  });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead  });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            // Entire read is an insertion plus softclip; softclip becomes the anchor
            cigarString = "5S1I";
            variantPositionInRead = 0;
            refRead = "TTTTT";
            altRead = "TTTTTC";
            refAllele = "T";
            altAllele = "TC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });


            // Entire read is an insertion. No anchor; throw an exception.
            cigarString = "5I";
            variantPositionInRead = 0;
            refRead = "AAGT";
            altRead = "CAAGT";
            refAllele = "N";
            altAllele = "NC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);

            Assert.Throws<Exception>(
                () => ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
                {
                    Expectations =
                        new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele,
                            _readStartPos + variantPositionInRead - 1)
                }));


            cigarString = "3M5I1M";
            variantPositionInRead = 3;
            refRead = "AAGT";
            altRead = "AAGCCCCCT";
            refAllele = "G";
            altAllele = "GCCCCC";
            goodQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff);
            badQualities = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1);
            goodOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff - 1, _qualityCutoff, new[] { variantPositionInRead });
            badOnlyAtInsStart = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead });
            var badOnlyAtInsMiddle = CandidateFinderTestHelpers.QualitiesArray(cigarString, _qualityCutoff, _qualityCutoff - 1, new[] { variantPositionInRead + 2 });

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            //Test of quality is applied to first base only. Low quality base in the middle of an insertion should still let us output an insertion
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsMiddle)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });

            refRead = "AAGT";
            altRead = "AAGCCNCCT";
            refAllele = "G";
            altAllele = "GCCNCC";

            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodQualities)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badQualities)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, goodOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsStart)
            {
                Expectations = new CandidateVariantTestExpectations(0, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
            //Test of quality is applied to first base only. Low quality base in the middle of an insertion should still let us output an insertion
            ExecuteTest(new CandidateVariantsTest(_readStartPos, refRead, cigarString, altRead, badOnlyAtInsMiddle)
            {
                Expectations = new CandidateVariantTestExpectations(1, typeExpected, refAllele, altAllele, _readStartPos + variantPositionInRead - 1)
            });
        }
    }

    public class CallSomaticVariantsVariantFromCigarSuite : VariantFromCigarSuite<AlignmentSet, CandidateAllele, IEnumerable<CandidateAllele>>
    {
        public override AlignmentSet BuildReadsToCheckForVariants(CandidateVariantsTest test)
        {
            return CandidateFinderTestHelpers.CreateAlignmentSet(test.CigarString, test.ReadBases, test.ReadQualities, _readStartPos);
        }

        public override IEnumerable<CandidateAllele> GetVariants(CandidateVariantsTest test, AlignmentSet reads)
        {
            var vf = new CandidateVariantFinder(_qualityCutoff,test.MaxLengthMnv,test.MaxLengthInterveningRef,true);
            var candidates = vf.FindCandidates(reads, test.RefChromosome, _chromName);
            return candidates;
        }

        public override void CheckVariantBasicInfo(CandidateVariantsTest test, IEnumerable<CandidateAllele> results, bool expectRef = false)
        {
            var resultsFiltered = expectRef ? results : results.Where(x => x.Type != AlleleCategory.Reference);
            var resultCount = resultsFiltered.Count();
            PrintResults(results);
            Assert.Equal(test.Expectations.NumVariantsExpected, resultCount);

            if (resultCount == 0)
            {
                return;
            }

            var filtered = resultsFiltered.OrderBy(v=>v.Coordinate).ToList();
            Assert.Equal(test.Expectations.ExpectedVariants.Count(),filtered.Count());

            for (int i = 0; i < filtered.Count(); i++)
            {
                var result = filtered[i];
                Assert.Equal(_chromName, result.Chromosome);
                Assert.Equal(test.Expectations.ExpectedVariants[i].CoordExpected, result.Coordinate);
                Assert.Equal(test.Expectations.ExpectedVariants[i].RefExpected, result.Reference);
                Assert.Equal(test.Expectations.ExpectedVariants[i].AltExpected, result.Alternate);
                CheckVariantType(test.Expectations.ExpectedVariants[i], result);                
            }
        }

        private void PrintResults(IEnumerable<CandidateAllele> results)
        {
            Console.WriteLine("-------------------");
            results = results.OrderBy(v => v.Coordinate);
            foreach (var result in results)
            {
                Console.WriteLine("{0}\t{1}\t{2}\t{3}", result.Chromosome, result.Coordinate, result.Reference,
                    result.Alternate);
            }
        }


        private void CheckVariantType(ExpectedVariant expectedVariant, CandidateAllele variant)
        {
            switch (expectedVariant.TypeExpected)
            {
                case CandidateVariantType.Deletion:
                    {
                        Assert.Equal(AlleleCategory.Deletion, variant.Type);
                        break;
                    }
                case CandidateVariantType.Insertion:
                    {
                        Assert.Equal(AlleleCategory.Insertion, variant.Type);
                        break;
                    }
                case CandidateVariantType.Mnv:
                    {
                        Assert.Equal(AlleleCategory.Mnv, variant.Type);
                        break;
                    }
                case CandidateVariantType.Snv:
                    {
                        Assert.Equal(AlleleCategory.Snv, variant.Type);
                        break;
                    }
            }
        }

    }

    public class VariantFinderTests
    {
        public const DirectionType Forward = DirectionType.Forward;
        public const DirectionType Reverse = DirectionType.Reverse;
        public const DirectionType Stitched = DirectionType.Stitched;

        [Fact]
        [Trait("ReqID","SDS-33")]
        public void FindCandidateVariants_SnvScenarios()
        {
            var testSuite = new CallSomaticVariantsVariantFromCigarSuite();
            testSuite.SnvTests();
        }

        [Fact]
        [Trait("ReqID", "SDS-36")]
        public void FindCandidateVariants_DelScenarios()
        {
            var testSuite = new CallSomaticVariantsVariantFromCigarSuite();
            testSuite.DeletionTests();
        }

        [Fact]
        [Trait("ReqID", "SDS-35")]
        public void FindCandidateVariants_InsScenarios()
        {
            var testSuite = new CallSomaticVariantsVariantFromCigarSuite();
            testSuite.InsertionTests();
        }

        [Fact]
        [Trait("ReqID", "SDS-34")]
        public void FindCandidateVariants_MnvScenarios()
        {
            var testSuite = new CallSomaticVariantsVariantFromCigarSuite();
            testSuite.MnvTests();
        }

        //[Fact]
        //public void FindCandidateVariants_ReturnsNothing_IfCigarM_AndMatchesGenome()
        //{
        //    var candidates = CandidateVariantsTest("G","1M", "G");
        //    Assert.Equal(0, candidates.Count());
        //}


        //Variant starts at first position of stitched region and ends within stitched region
        //Variant starts at first position of stitched region and ends on edge of stitched region
        //Variant starts at first position of stitched region and ends after end of stitched region
        //Variant starts within stitched region and ends within stitched region
        //Variant starts within stitched region and ends on edge of stitched region
        //Variant starts within stitched region and ends after end of stitched region
        //Variant starts before stitched region and ends within stitched region
        //Variant starts before stitched region and ends on far edge of stitched region
        //Variant starts before stitched region and ends after end of stitched region
        //Variant starts before stitched region and ends before stitched region
        //Variant starts before stitched region and ends at first position of stitched region
        //Variant starts well after stitched region and ends after stitched region
        //Variant starts right after stitched region and ends after stitched region- 
        //??
        //Variant starts at second position of stitched region and ends within stitched region
        //Variant starts at second position of stitched region and ends on edge of stitched region
        //Variant starts at second position of stitched region and ends after end of stitched region

        [Fact]
        public void GetSupportDirection_Insertion_ShouldReturnMaxDirectionBeforeAndAfter_InStitchedRead()
        {
            var testSetup = new CallSomaticVariantsSupportDirectionTestSetup();
            var supportDirectionSuite = new SupportDirectionSuite<DirectionType, Read, CandidateAllele>(testSetup, Forward, Reverse, Stitched);

            var chromosome = "chr1";
            var coordinate = 2;
            var reference = "T";
            var alternate = "TATC";
            //This is called at position 2, if the read is at position 0

            var variant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Insertion);
            supportDirectionSuite.RunInsertionScenarios(variant);
        }

        [Fact]
        public void GetSupportDirection_MNV_ShouldReturnMaxDirectionStartAndEnd_InStitchedRead()
        {
            var testSetup = new CallSomaticVariantsSupportDirectionTestSetup();
            var supportDirectionSuite = new SupportDirectionSuite<DirectionType, Read, CandidateAllele>(testSetup, Forward, Reverse, Stitched);

            var chromosome = "chr1";
            var coordinate = 3;
            var reference = "TGG";
            var alternate = "ATC";
            //This is called at position 3, if the read is at position 0

            var variant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv);
            supportDirectionSuite.RunMnvScenarios(variant);
        }


        [Fact]
        public void GetSupportDirection_Deletion_ShouldReturnMinDirectionBeforeAndAfter_InStitchedRead()
        {
            var testSetup = new CallSomaticVariantsSupportDirectionTestSetup();
            var supportDirectionSuite = new SupportDirectionSuite<DirectionType, Read, CandidateAllele>(testSetup, Forward, Reverse, Stitched);

            var chromosome = "chr1";
            var coordinate = 2;
            var reference = "ATCG";
            var alternate = "A";
            //This is called at position 2, if the read is at position 0

            //For deletions, the variant is stitched only if 
            var variant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Deletion);
            supportDirectionSuite.RunDeletionScenarios(variant, 3);
        }

        [Fact]
        public void GetSupportDirection_SNV_ShouldReturnDirectionAtThatPosition_InStitchedRead()
        {
            var testSetup = new CallSomaticVariantsSupportDirectionTestSetup();

            //Variant starts at first position of stitched region - should be stitched
            // 0 0 0 2 2 2 2 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = testSetup.GetAlignmentWithCoverageDirections(3, 4, 3, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 0, 0, 1 });

            //Variant starts within stitched region - should be stitched
            // 0 0 2 2 2 2 2 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(2, 5, 3, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 0, 0, 1 });

            //Variant starts at far edge of stitched region - should be stitched
            // 0 0 2 2 1 1 1 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(2, 2, 6, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 0, 0, 1 });

            //Variant starts immediately after stitched region - should be reverse
            // 0 2 2 1 1 1 1 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(1, 2, 7, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 0, 1, 0 });

            //Variant starts well after stitched region - should be reverse
            // 0 2 1 1 1 1 1 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(1, 1, 8, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 0, 1, 0 });

            //Variant starts immediately before stitched region - should be forward
            // 0 0 0 0 2 2 2 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(4, 3, 3, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 1, 0, 0 });

            //Variant starts well before stitched region - should be forward
            // 0 0 0 0 0 2 2 1 1 1     CoverageDirection
            // N N N V N N N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            alignment = testSetup.GetAlignmentWithCoverageDirections(5, 2, 3, 0);
            ExecuteSupportDirectionTest(alignment, new[] { 1, 0, 0 });
        }

        private void ExecuteSupportDirectionTest(Read alignment, int[] expectedSupport, int coordinate = 3)
        {
            var chromosome = "chr1";
            var reference = "A";
            var alternate = "T";

            var variant = CandidateVariantFinder.Create(AlleleCategory.Snv, chromosome, coordinate, reference, alternate,
                alignment, CallSomaticVariantsSupportDirectionTestSetup.VariantStartInRead);
            VerifySupportDirection(variant, expectedSupport);
        }

        private void VerifySupportDirection(CandidateAllele variant, int[] expectedSupport)
        {
            for (var i = 0; i < variant.SupportByDirection.Length; i++)
                Assert.Equal(variant.SupportByDirection[i], expectedSupport[i]);
        }
    }
}