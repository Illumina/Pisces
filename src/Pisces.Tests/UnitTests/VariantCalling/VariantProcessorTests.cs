using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pisces.Calculators;
using Pisces.Logic.VariantCalling;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Tests.UnitTests.Pisces
{
    public class VariantProcessorTests
    {
        private string _nonRepeatingString;

        [Fact]
        public void HappyPath()
        {
            var variant = TestHelper.CreatePassingVariant(false);
            variant.ReferenceSupport = 10;
            AlleleProcessor.Process(variant, 0.01f, 100, 20, false, 0f, 10f, 0, null, new ChrReference());
            Assert.False(variant.Filters.Any());
            Assert.Equal(0.02f, variant.FractionNoCalls);
            Assert.Equal(Genotype.HeterozygousAltRef, variant.Genotype);
        }

        [Fact]
        [Trait("ReqID", "SDS-49")]
        public void FractionNoCallScenarios()
        {
            ExecuteNoCallTest(900, 100, 0.1f); // standard
            ExecuteNoCallTest(0, 0, 0f); // nothing
            ExecuteNoCallTest(500, 0, 0f); // no no calls
            ExecuteNoCallTest(0, 500, 1f);  // all no calls
        }

        private void ExecuteNoCallTest(int totalCoverage, int numNoCalls, float expectedFraction)
        {
            var variant = TestHelper.CreatePassingVariant(false);
            variant.NumNoCalls = numNoCalls;
            variant.TotalCoverage = totalCoverage;

            AlleleProcessor.Process(variant, 0.01f, 100, 20, false, 10f, 10f, 8, null, new ChrReference());

            Assert.Equal(expectedFraction, variant.FractionNoCalls);
        }

        [Fact]
        [Trait("ReqID", "SDS-48")]
        public void Filtering()
        {
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 0f, 0f, 0, new List<FilterType>());
            ExecuteFilteringTest(499, 30, false, 500, 30, false, 0f, 0f, 0, new List<FilterType>() { FilterType.LowDepth });
            ExecuteFilteringTest(500, 24, false, 500, 25, false, 0f, 0f, 0, new List<FilterType>() { FilterType.LowVariantQscore });
            ExecuteFilteringTest(500, 30, true, 500, 25, false, 0f, 0f, 0, new List<FilterType>() { FilterType.StrandBias }); // less than threshold
            ExecuteFilteringTest(500, 30, false, 500, 30, true, 0f, 0f, 0, new List<FilterType>() { FilterType.StrandBias }); // variant on one strand only
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 1f, 0f, 0, new List<FilterType>() { FilterType.LowVariantFrequency });
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 0f, 50f, 0, new List<FilterType>() { }); //FilterType.LowGenotypeQuality not currently supported.
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 0f, 0f, 2, new List<FilterType>() { FilterType.IndelRepeatLength });
            ExecuteFilteringTest(500, 30, false, null, 30, false, null, null, null, new List<FilterType>());
            ExecuteFilteringTest(0, 0, true, 101, 20, false, 0f, 50f, 2, new List<FilterType>() { FilterType.StrandBias, FilterType.LowDepth, FilterType.IndelRepeatLength });//FilterType.LowGenotypeQuality not currently supported.
            ExecuteFilteringTest(1, 0, true, 101, 20, false, 0f, 50f, 2, new List<FilterType>() { FilterType.StrandBias, FilterType.LowVariantQscore, FilterType.LowDepth, FilterType.IndelRepeatLength });//FilterType.LowGenotypeQuality not currently supported.


            // Ref alleles should not have filters (except LowDP and q30) even if they would have qualified
            ExecuteFilteringTest(500, 30, true, 500, 25, false, 0f, 0f, 0, new List<FilterType>() { }, true); // if non-ref, would have been flagged for SB
            ExecuteFilteringTest(500, 30, false, 500, 30, true, 0f, 0f, 0, new List<FilterType>() { }, true); // if non-ref, would have been flagged for SB
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 1f, 0f, 0, new List<FilterType>() {}, true); // if non-ref, would have been flagged for low VF
            ExecuteFilteringTest(500, 30, false, 500, 30, false, 0f, 0f, 2, new List<FilterType>() { }, true); // if non-ref, would have been flagged for R8
            ExecuteFilteringTest(0, 0, true, 101, 20, false, 0f, 50f, 2, new List<FilterType>() { FilterType.LowDepth }, true); // if non-ref, would have been flagged for R8
            ExecuteFilteringTest(1, 0, true, 101, 20, false, 0f, 50f, 2, new List<FilterType>() { FilterType.LowVariantQscore, FilterType.LowDepth }, true); // if non-ref, would have been flagged for R8

            // Ref alleles should be allowed to have LowDP and q30 filters
            ExecuteFilteringTest(499, 30, false, 500, 30, false, 0f, 0f, 0, new List<FilterType>() { FilterType.LowDepth }, true);
            ExecuteFilteringTest(500, 24, false, 500, 25, false, 0f, 0f, 0, new List<FilterType>() { FilterType.LowVariantQscore }, true);

        }

        [Fact]
        public void IndelRepeat()
        {
            _nonRepeatingString = GetPadding();

            // Simple Case: R8 with homopolymer repeat
            ExecuteRepeatTest("A", "A", 7, 8, false);
            ExecuteRepeatTest("A", "A", 9, 8, true);
            ExecuteRepeatTest("A", "A", 8, 8, true);

            // Homopolymer repeat but different base
            ExecuteRepeatTest("A", "T", 7, 8, false);
            ExecuteRepeatTest("A", "T", 9, 8, false);
            ExecuteRepeatTest("A", "T", 8, 8, false);

            // Single variant base with multi-base repeat
            ExecuteRepeatTest("A", "ATC", 7, 8, false);
            ExecuteRepeatTest("A", "ATC", 8, 8, false);
            ExecuteRepeatTest("A", "ATC", 9, 8, false);

            // Multi-base variant repeat
            ExecuteRepeatTest("ATC", "ATC", 7, 8, false);
            ExecuteRepeatTest("ATC", "ATC", 9, 8, true);
            ExecuteRepeatTest("ATC", "ATC", 8, 8, true);

            // Multi-base insertion with homopolymer
            ExecuteRepeatTest("ATC", "A", 7, 8, false);
            ExecuteRepeatTest("ATC", "A", 9, 8, false);
            ExecuteRepeatTest("ATC", "A", 8, 8, false);

            // Window is technically 50 bp but we only scan to n bases down from our variant where n is (49 - 1 - repeatUnit.Length). 49 because 50 includes our variant coord. 
            // This seems overcomplicated, but the way Isis did it. When replacing this logic we'll probably want to change this window behavior.
            ExecuteRepeatTest("A", "A", 47, 48, false);
            ExecuteRepeatTest("A", "A", 50, 48, true);
            ExecuteRepeatTest("A", "A", 48, 48, true);
            ExecuteRepeatTest("A", "A", 80, 49, false); // We can really only detect up to 48 repeats to the right
            ExecuteRepeatTest("AT", "AT", 23, 24, false);
            ExecuteRepeatTest("AT", "AT", 25, 24, true);
            ExecuteRepeatTest("AT", "AT", 24, 24, true);
            ExecuteRepeatTest("AT", "AT", 50, 25, false); // We can really only detect up to 24 repeats to the right

            // Variant bases themselves are a repeat - simplify unit before checking
            ExecuteRepeatTest("AA", "A", 7, 8, false);
            ExecuteRepeatTest("AA", "A", 9, 8, true);
            ExecuteRepeatTest("AA", "A", 8, 8, true);
        }

        [Fact]
        public void RMxN()
        {
            // ------------------------------------
            // SNV/MNV Cases
            // ------------------------------------
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3);
            ExecuteRMxNMnvTest("GG", "CCC*CGGG", 3);
            ExecuteRMxNMnvTest("CC", "CCCCG*GG", 3);

            ExecuteRMxNMnvTest("GG", "ACACA*CGGGG", 3);
            ExecuteRMxNMnvTest("AC", "ACACACG*GGG", 3);

            ExecuteRMxNMnvTest("AAA", "CAGCAGC*AGAAAAAA", 3);
            ExecuteRMxNMnvTest("CAG", "CAGCAGCAGA*AAAAA", 3);

            // ------------------------------------
            // Wiki Examples
            // ------------------------------------
            // A.1 (no example?)
            // A.2
            ExecuteRMxNIndelTest("ACACACACACAC", "N*ACACGGAC", 2);

            // A.3
            ExecuteRMxNMnvTest("T", "ACACAC*ACACAC", 0);
            ExecuteRMxNIndelTest("TCA", "ACACAC*ACACAC", 1); // 1 for each of Cs and As
            ExecuteRMxNIndelTest("TAC", "ACACAC*ACACAC", 6); 

            // A.4
            ExecuteRMxNIndelTest("AC", "N*ACACAC", 3);
            ExecuteRMxNIndelTest("AC", "N*ACACGGACAC", 2);
            ExecuteRMxNIndelTest("AC", "N*ACCACCACC", 1);
            ExecuteRMxNIndelTest("AC", "N*ACACACAC", 4);
            ExecuteRMxNIndelTest("AC", "N*ACACA", 2);
            ExecuteRMxNIndelTest("AC", "N*AAA", 3);

            // B.1	 
            // TODO : SCYLLA CASE AC > G  -- ExecuteRMxNTest("G", "ACACACACACA*CGGGGG", 5, AlleleCategory.Mnv);
            ExecuteRMxNMnvTest("GG", "ACACACACACA*CGGGGG", 5);
            ExecuteRMxNMnvTest("AC", "ACACACACACACG*GGGG", 5);
            ExecuteRMxNMnvTest("ACAC", "ACACACACACACG*GGGG", 5);

            // B.2
            ExecuteRMxNIndelTest("GGACAC", "ACAC*ACACACAC", 6);
            ExecuteRMxNIndelTest("ACACG", "ACAC*ACACACAC", 6);
            ExecuteRMxNIndelTest("ACACGAC", "ACAC*ACACACAC", 6);

            // Both ends are repeats. Take the larger one.
            ExecuteRMxNIndelTest("ACACGG", "ACACACACACAC*GGGGG", 6);
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGG", 6);

            ExecuteRMxNIndelTest("ACACGG", "ACACACACACAC*GGGGGGG", 7);
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGGGG", 7);

            //  TODO : SCYLLA CASE C > ACACGGGG --  ExecuteRMxNTest("ACACGGGG", "ACACACACACAC*GGGGGGGGGGGGGGGG", 5);

            // ------------------------------------
            // Test the repeat unit length limits
            // ------------------------------------
            // Indel
            ExecuteRMxNIndelTest("ACG", "N*ACGACGACG", 3, 3); 
            ExecuteRMxNIndelTest("ACG", "N*ACGACGACG", 1, 2); // Get just 1 (for the single base edges)
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGG", 5, 1); // Doesn't find the AC-repeat (6) but does see the G-repeat (5)

            // MNV
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3, 1);
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3, 3);
            ExecuteRMxNMnvTest("GG", "ACACA*CGGGG", 1, 1); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("AC", "ACACACG*GGG", 1, 1); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("AAA", "CAGCAGC*AGAAAAAA", 1, 2); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("CAG", "CAGCAGCAGA*AAAAA", 1, 2); // Get just 1 (for the single base edges)

        }

        private void ExecuteRMxNIndelTest(string variantBases, string referenceSequence, int expectedRepeatLength, int? maxRepeatUnitLength = null)
        {
            ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, AlleleCategory.Insertion, maxRepeatUnitLength ?? variantBases.Length);
            ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, AlleleCategory.Deletion, maxRepeatUnitLength ?? variantBases.Length);
        }

        private void ExecuteRMxNMnvTest(string variantBases, string referenceSequence, int expectedRepeatLength,int? maxRepeatUnitLength = null)
        {
             ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, variantBases.Length > 1 ? AlleleCategory.Mnv : AlleleCategory.Snv, maxRepeatUnitLength ?? variantBases.Length);
        }

        private void ExecuteRMxNTest(string variantBases, string referenceSequence, int expectedRepeatLength, AlleleCategory category, int maxRepeatUnitLength)
        {
            var alleleCoordinate = referenceSequence.IndexOf('*');
            var cleanReferenceSequence = referenceSequence.Replace("*", "");

            var refAllele = "";
            var altAllele = "";

            if (category == AlleleCategory.Insertion)
            {
                refAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, 1);
                altAllele = refAllele + variantBases;
            }
            else if (category == AlleleCategory.Deletion)
            {
                altAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, 1);
                refAllele = altAllele + variantBases;
            }
            else
            {
                refAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, variantBases.Length);
                altAllele = variantBases;
            }

            var allele = new CalledAllele(category)
            {
                Reference = refAllele,
                Alternate = altAllele,
                Coordinate = alleleCoordinate
            };

            RMxNFilterSettings rmxnFilterSettings = new RMxNFilterSettings();
            rmxnFilterSettings.RMxNFilterFrequencyLimit = 1.1f;
            rmxnFilterSettings.RMxNFilterMaxLengthRepeat = maxRepeatUnitLength;
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength;

            // If expected repeats == N, flag
            AlleleProcessor.Process(allele, 0.01f, 0, 0,
                true, 0, 0, null, rmxnFilterSettings, new ChrReference() { Sequence = cleanReferenceSequence });
            Assert.True(allele.Filters.Contains(FilterType.RMxN));

            // If expected repeats > N, flag
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength-1;

            AlleleProcessor.Process(allele, 0.01f, 0, 0,
                true, 0, 0, null, rmxnFilterSettings, new ChrReference() { Sequence = cleanReferenceSequence });

            Assert.True(allele.Filters.Contains(FilterType.RMxN));

            // If expected repeats < N, flag
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength + 1;

            AlleleProcessor.Process(allele, 0.01f, 0, 0,
                true, 0, 0, null, rmxnFilterSettings, new ChrReference() { Sequence = cleanReferenceSequence });

            Assert.False(allele.Filters.Contains(FilterType.RMxN));

            // RMxN isn't valid on reference calls
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength + 1;

            AlleleProcessor.Process(new CalledAllele() { },   0.01f, 0, 0,
                true, 0, 0, null, rmxnFilterSettings, new ChrReference() { Sequence = cleanReferenceSequence });

            Assert.False(allele.Filters.Contains(FilterType.RMxN));

        }

        private void ExecuteRepeatTest(string variantBases, string repeatUnit, int repeatsInReference, int repeatThreshold, bool shouldBeFlagged)
        {
            var offset = 5 * repeatUnit.Length * repeatsInReference;
            offset = 10;

            // Test with insertion
            var variant = CreateRepeatVariant(variantBases);
            variant.Coordinate = offset + repeatUnit.Length;
            var chrReference = new ChrReference
            {
                Sequence = CreateRepeat(repeatUnit, repeatsInReference, variant.Coordinate)
            };


            AlleleProcessor.Process(variant,   0.01f, 0, 0,
                true, 0, 0, repeatThreshold, null, chrReference);

            Assert.Equal(shouldBeFlagged, variant.Filters.Contains(FilterType.IndelRepeatLength));

            // Test with deletion
            variant = CreateRepeatVariant(variantBases, true);
            variant.Coordinate = offset + repeatUnit.Length;

            AlleleProcessor.Process(variant,   0.01f, 0, 0,
                true, 0, 0, repeatThreshold, null, chrReference);
            Assert.Equal(shouldBeFlagged, variant.Filters.Contains(FilterType.IndelRepeatLength));
        }

        private CalledAllele CreateRepeatVariant(string repeatUnit, bool isDeletion = false)
        {
            var variant = TestHelper.CreatePassingVariant(false);
            variant.Coordinate = 5;

            if (isDeletion)
            {
                variant.Type = AlleleCategory.Deletion;
                variant.Alternate = "A";
                variant.Reference = "A" + repeatUnit;
            }
            else
            {
                variant.Type = AlleleCategory.Insertion;
                variant.Reference = "A";
                variant.Alternate = "A" + repeatUnit;                
            }
            return variant;
        }

        private string CreateRepeat(string repeatUnit, int numRepeats, int coordinate = 0, int repeatRelativeToVariant=0, bool isDeletion = false)
        {
            var sb = new StringBuilder();

            sb.Append(_nonRepeatingString.Substring(0, coordinate + repeatRelativeToVariant - (isDeletion ? 2 : 1)) + "Z");
            
            for (var i = 0; i < numRepeats; i++)
            {
                sb.Append(repeatUnit);
            }

            sb.Append(_nonRepeatingString.Substring(0, 1000));

            return sb.ToString();
        }

        private string GetPadding()
        {
            var chars = Enumerable.Range(0, char.MaxValue + 1)
                      .Select(i => (char)i)
                      .Where(c => char.IsSymbol(c))
                      .ToList();

            return string.Join("X", chars);
        }

        private void ExecuteFilteringTest(int totalCoverage, int qscore, bool strandBias, 
            int? lowDepthFilter, int minQscore, bool singleStrandVariant, float? variantFreq, 
            float? lowGQ, int? indelRepeat, List<FilterType> expectedFilters, bool isRefAllele = false)
        {
            var variant = TestHelper.CreatePassingVariant(isRefAllele);
            variant.TotalCoverage = totalCoverage;
            variant.VariantQscore = qscore;
            variant.StrandBiasResults.BiasAcceptable = !strandBias;
            variant.StrandBiasResults.VarPresentOnBothStrands = !singleStrandVariant;
            var chrRef = new ChrReference();

            if (indelRepeat > 0)
            {
                variant.Alternate   = "AAAAAA";
                chrRef.Sequence     = "AAAAAAAA";

                if (!isRefAllele)
                    variant.Type = AlleleCategory.Insertion;
            }


            AlleleProcessor.Process(variant,   0.01f, lowDepthFilter, minQscore, 
                true, variantFreq, lowGQ, indelRepeat, null, chrRef);

            Assert.Equal(variant.Filters.Count, expectedFilters.Count);
            foreach(var filter in variant.Filters)
                Assert.True(expectedFilters.Contains(filter));
        }

    }
}
