using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.Calculators;
using Pisces.Domain;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.Models;
using Xunit;

namespace Pisces.Logic.VariantCalling.Tests
{
    public class VariantCallerTests
    {
        private const int HighCoverageMultiplier = 100;
        private const int LowCoverageMultiplier = 1;
        private const int NormalCoverageMultiplier = 10;
        private readonly int NumAlleles = Constants.NumCovContributingAlleleTypes;
        private const int NumDirections = 3;

        [Fact]
        [Trait("ReqID", "SDS-44")]
        public void EvaluateVariants()
        {
            var config = new VariantCallerConfig
            {
                MaxVariantQscore = 100,
                EstimatedBaseCallQuality = 20,
                ChrReference = new ChrReference
                {
                    Sequence = "ACGTACGT",
                    Name = "Boo"
                },
                GenotypeCalculator = new SomaticGenotypeCalculator()
            };

            var variantCaller = new AlleleCaller(config);

            var highCoverageCoordinate = 123;
            var lowCoverageCoordinate = 456;

            var passingVariant = new CandidateAllele("chr1", highCoverageCoordinate, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 500, 0, 0 } // Freq is 500/1500, q is 100
            };
            var passingVariant2 = new CandidateAllele("chr1", highCoverageCoordinate, "A", "C", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 500, 0, 0 } // Freq is 500/1500, q is 100
            };
            var lowFreqVariant = new CandidateAllele("chr2", highCoverageCoordinate, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 1, 0, 0 } // Freq is 1/1500, q is 0
            };
            var lowCoverageVariant = new CandidateAllele("chr3", lowCoverageCoordinate, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 10, 0, 0 } // Freq is 10/15, q is 100
            };
            var lowqVariant = new CandidateAllele("chr4", highCoverageCoordinate, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 40, 0, 0 } // Freq is 40/1500, q is 72
            };
            var passingReferenceHigh = new CandidateAllele("chr1", highCoverageCoordinate, "A", "A", AlleleCategory.Reference)
            {
                SupportByDirection = new[] { 500, 0, 0 } // Freq is 500/1500, q is 100
            };
            var passingReferenceLow = new CandidateAllele("chr3", lowCoverageCoordinate, "A", "A", AlleleCategory.Reference)
            {
                SupportByDirection = new[] { 10, 0, 0 } // Freq is 10/15, q is 100
            };
            var candidateVariants = new List<CandidateAllele>
            {
                passingVariant
            };


            //Variants should be correctly mapped
            var mockAlleleCountSource = MockStateManager(highCoverageCoordinate, lowCoverageCoordinate).Object;
            var BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            var BaseCalledAllele = BaseCalledAlleles.First();
            Assert.Equal(passingVariant.Alternate, BaseCalledAllele.Alternate);
            Assert.Equal(passingVariant.Reference, BaseCalledAllele.Reference);
            Assert.Equal(passingVariant.Chromosome, BaseCalledAllele.Chromosome);
            Assert.Equal(passingVariant.Coordinate, BaseCalledAllele.Coordinate);
            Assert.Equal(passingVariant.Support, BaseCalledAllele.AlleleSupport);
            Assert.True(passingVariant.Type != AlleleCategory.Reference);
            Assert.True(BaseCalledAllele.Type == AlleleCategory.Snv);

            //After the Calculator steps are performed, variants that don't meet 
            //our requirements to be callable should drop out

            //High coverage requirement - lowCoverageVariant should drop out.
            config.MinCoverage = (HighCoverageMultiplier * NumAlleles * NumDirections) - 1;
            config.IncludeReferenceCalls = false;
            config.MinVariantQscore = 0;
            config.MinFrequency = 0;

            variantCaller = new AlleleCaller(config);

            candidateVariants = new List<CandidateAllele>
            {
                passingVariant,
                lowFreqVariant,
                lowCoverageVariant
            };

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);

            Assert.Equal(2, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));

            //High coverage but allow reference calls = nothing should drop out
            config.IncludeReferenceCalls = true;

            variantCaller = new AlleleCaller(config);

            candidateVariants = new List<CandidateAllele>
            {
                passingVariant,
                lowFreqVariant,
                lowCoverageVariant
            };

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            foreach (var cvar in BaseCalledAlleles)
            {
                Console.WriteLine(cvar.VariantQscore);
            }

            Assert.Equal(3, BaseCalledAlleles.Count());

            //High frequency requirement - low frequency variant should drop out
            config.MinCoverage = 0;
            config.IncludeReferenceCalls = false;
            config.MinVariantQscore = 0;
            config.MinFrequency = ((float)lowCoverageVariant.Support + 1) / (HighCoverageMultiplier * NumAlleles * NumDirections);

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);

            Assert.Equal(2, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));

            //High q score requirement - low frequency variant should drop out
            config.MinCoverage = 0;
            config.IncludeReferenceCalls = false;
            config.MinVariantQscore = 0;
            config.MinFrequency = 0;
            config.MinVariantQscore = VariantQualityCalculator.AssignPoissonQScore(lowqVariant.Support,
                (HighCoverageMultiplier * Constants.NumCovContributingAlleleTypes * Constants.NumDirectionTypes), config.EstimatedBaseCallQuality,
                config.MaxVariantQscore) + 1;

            candidateVariants = new List<CandidateAllele>
            {
                passingVariant,
                passingVariant2,
                lowFreqVariant,
                lowCoverageVariant,
                lowqVariant
            };

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            Assert.Equal(3, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant2)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowqVariant)));



            //High genotype q score requirement - low GQ variant should filter

            config.MinCoverage = 0;
            config.IncludeReferenceCalls = false;
            config.MinVariantQscore = 0;
            config.MinFrequency = 0;
            config.GenotypeCalculator = new DiploidGenotypeCalculator();
            config.LowGTqFilter = 20;
            config.MaxGenotypeQscore = int.MaxValue;

            candidateVariants = new List<CandidateAllele>
            {
                passingVariant,
                passingVariant2,
                lowFreqVariant,
                lowCoverageVariant,
                lowqVariant
            };

            variantCaller = new AlleleCaller(config);
            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            Assert.Equal(2, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant2)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowqVariant)));

            var calledList = BaseCalledAlleles.ToList();
            Assert.True(calledList[0].Filters.Contains(FilterType.LowGenotypeQuality));//1249, forced to 0 for nocall.
            Assert.True(calledList[1].Filters.Contains(FilterType.LowGenotypeQuality));//1249, forced to 0 for nocall
                                                                                       //Assert.True(calledList[2].Filters.Contains(FilterType.LowGenotypeQuality));//0 pruned

            //go back to somatic config for the rest of the tests.
            config.GenotypeCalculator = new SomaticGenotypeCalculator();
            config.LowGTqFilter = 0;
            config.MaxGenotypeQscore = 0;
            config.MinVariantQscore = VariantQualityCalculator.AssignPoissonQScore(lowqVariant.Support,
                   (HighCoverageMultiplier * Constants.NumCovContributingAlleleTypes * Constants.NumDirectionTypes), config.EstimatedBaseCallQuality,
                   config.MaxVariantQscore) + 1;

            // reference calls included
            candidateVariants = new List<CandidateAllele>
            {
                passingReferenceHigh,
                passingReferenceLow
            };

            variantCaller = new AlleleCaller(config);
            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            Assert.Equal(2, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingReferenceHigh)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingReferenceLow)));

            // reference calls only included if no passing variant
            candidateVariants = new List<CandidateAllele>
            {
                passingReferenceHigh,
                passingReferenceLow,
                passingVariant,
                passingVariant2,
                lowFreqVariant,
                lowCoverageVariant,
                lowqVariant
            };

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            Assert.Equal(3, BaseCalledAlleles.Count());
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, passingReferenceHigh)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, passingReferenceLow)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant2)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowqVariant)));

            // reference calls only included if no passing variant (lowCoverageVariant fails)

            candidateVariants = new List<CandidateAllele>
            {
                passingReferenceLow,
                lowCoverageVariant,
            };

            config.IncludeReferenceCalls = false;
            config.MinCoverage = (HighCoverageMultiplier * NumAlleles * NumDirections) - 1;

            variantCaller = new AlleleCaller(config);

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);
            Assert.Equal(1, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingReferenceLow)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));

            // candidates outside of intervals are trimmed off

            config.MinCoverage = 0;
            config.MinVariantQscore = 0;
            config.MinFrequency = 0;

            variantCaller = new AlleleCaller(config, new ChrIntervalSet(new List<Region>() { new Region(highCoverageCoordinate, lowCoverageCoordinate) }, "chr1"));

            candidateVariants = new List<CandidateAllele>
            {
                passingVariant,
                lowFreqVariant,
                lowCoverageVariant
            };

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);

            Assert.Equal(3, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));

            variantCaller = new AlleleCaller(config, new ChrIntervalSet(new List<Region>() { new Region(highCoverageCoordinate, highCoverageCoordinate) }, "chr1"));

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource).Values.SelectMany(v => v);

            Assert.Equal(2, BaseCalledAlleles.Count());
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingVariant)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, lowFreqVariant)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, lowCoverageVariant)));
        }

        [Fact]
        [Trait("ReqID", "SDS-53")]
        public void CallVariants_MnvReallocation()
        {
            var config = new VariantCallerConfig
            {
                MaxVariantQscore = 100,
                EstimatedBaseCallQuality = 20,
                IncludeReferenceCalls = true,
                ChrReference = new ChrReference
                {
                    Sequence = "ACGTACGT",
                    Name = "Boo"
                },
                GenotypeCalculator = new SomaticGenotypeCalculator()
            };

            var variantCaller = new AlleleCaller(config);

            // -----------------------------------------------
            // Happy path : with refs
            // - Failing MNVs that are sub-MNVs of bigger ones should not be recipients of reallocation; they should be reallocated themselves though.
            // - Failing SNVs should be able to be rescued
            // - Refs should have their support incremented by failed gapped MNVs
            // -----------------------------------------------

            var failingMnvToReallocate = new CandidateAllele("chr1", 101, "TTTTTTTT", "ATCTGTGA", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 50, 0, 0 } // Freq is 50/150
            };
            var failingMnvToNotRescue = new CandidateAllele("chr1", 101, "TTT", "ATC", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 5, 0, 0 } // Freq is 5/150
            };
            var failingSnvToRescue = new CandidateAllele("chr1", 101, "T", "A", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 50, 0, 0 } // Freq is 50/150
            };
            var passingMnv = new CandidateAllele("chr1", 105, "TTT", "GTG", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 100, 0, 0 } // Freq is 100/150
            };
            var passingDeletion = new CandidateAllele("chr1", 105, "TTT", "T", AlleleCategory.Deletion)
            {
                SupportByDirection = new[] { 100, 0, 0 } // Freq is 100/150
            };

            var mockStateManager = MockStateManager(1000, 1001).Object;

            config.MinCoverage = 0;
            config.MinVariantQscore = 0;
            config.MinFrequency = .5f;

            variantCaller = new AlleleCaller(config);

            var candidateVariants = new List<CandidateAllele>
            {
                failingSnvToRescue,
                failingMnvToNotRescue,
                failingMnvToReallocate,
                passingMnv,
                passingDeletion
            };

            var BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockStateManager).Values.SelectMany(v => v);

            PrintResults(BaseCalledAlleles.ToList());

            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, failingMnvToReallocate)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, failingMnvToNotRescue)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingMnv, 150, 1))); // Passing MNV should have additional support from big failed MNV
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, failingSnvToRescue, 105))); // SNV should be rescued and have support from both failed MNVs
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingDeletion))); // Passing deletion should still be called
            // There should NOT be new refs from where the MNV broke down, and should be emitted regardless of support
            Assert.False(BaseCalledAlleles.Any(v => v.Coordinate == 102 && v .Type == AlleleCategory.Reference && v.AlleleSupport == 55)); // Should have support from both failed MNVs
            Assert.False(BaseCalledAlleles.Any(v => v.Coordinate == 104 && v .Type == AlleleCategory.Reference && v.AlleleSupport == 50)); // Should have support from the big failed MNV only
            //There should not be a new ref at position 106 from the passing gapped MNV
            Assert.False(BaseCalledAlleles.Any(v => v.Coordinate == 106 && v .Type == AlleleCategory.Reference && v.AlleleSupport > 0));


            // -----------------------------------------------
            // Happy path : without refs
            // - Failing MNVs that are sub-MNVs of bigger ones should not be recipients of reallocation; they should be reallocated themselves though.
            // - Failing SNVs should be able to be rescued
            // -----------------------------------------------

            failingMnvToReallocate = new CandidateAllele("chr1", 101, "TTTTTTTT", "ATCTGTGA", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 50, 0, 0 } // Freq is 50/150
            };
            failingMnvToNotRescue = new CandidateAllele("chr1", 101, "TTT", "ATC", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 5, 0, 0 } // Freq is 5/150
            };
            failingSnvToRescue = new CandidateAllele("chr1", 101, "T", "A", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 50, 0, 0 } // Freq is 50/150
            };
            passingMnv = new CandidateAllele("chr1", 105, "TTT", "GTG", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 100, 0, 0 } // Freq is 100/150
            };
            passingDeletion = new CandidateAllele("chr1", 105, "TTT", "T", AlleleCategory.Deletion)
            {
                SupportByDirection = new[] { 100, 0, 0 } // Freq is 100/150
            };

            candidateVariants = new List<CandidateAllele>
            {
                failingSnvToRescue,
                failingMnvToNotRescue,
                failingMnvToReallocate,
                passingMnv,
                passingDeletion
            };

            config.IncludeReferenceCalls = false;
            variantCaller = new AlleleCaller(config);

            BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockStateManager).Values.SelectMany(v => v);

            PrintResults(BaseCalledAlleles.ToList());

            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, failingMnvToReallocate)));
            Assert.False(BaseCalledAlleles.Any(v => MatchVariants(v, failingMnvToNotRescue)));
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingMnv, 150, 1))); // Passing MNV should have additional support from big failed MNV
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, failingSnvToRescue, 105))); // SNV should be rescued and have support from both failed MNVs
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingDeletion))); // Passing deletion should still be called
            // There should be no refs from where the MNV broke down since we have IncludeReferenceCalls set to false
            Assert.False(BaseCalledAlleles.Any(v => v.Coordinate == 102 && v .Type == AlleleCategory.Reference));
            Assert.False(BaseCalledAlleles.Any(v => v.Coordinate == 104 && v .Type == AlleleCategory.Reference));

        }

        [Fact]
        public void CallVariants_MnvTakingRefSupport()
        {
            var config = new VariantCallerConfig
            {
                MaxVariantQscore = 100,
                EstimatedBaseCallQuality = 20,
                IncludeReferenceCalls = true,
                ChrReference = new ChrReference
                {
                    Sequence = "ACGTACGT",
                    Name = "Boo"
                },
                GenotypeCalculator = new SomaticGenotypeCalculator()
            };

            var variantCaller = new AlleleCaller(config);

            //Failing MNV shouldn't contribute
            var passingMnv = new CandidateAllele("chr1", 305, "TTA", "GTG", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 10, 0, 0 }
            };
            var passingSnv = new CandidateAllele("chr1", 306, "T", "G", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 200, 0, 0 }
            };
            var passingDeletion = new CandidateAllele("chr1", 305, "TTT", "T", AlleleCategory.Deletion)
            {
                SupportByDirection = new[] { 100, 0, 0 }
            };

            var mockAlleleCountSource = MockStateManager(306, 0);
            mockAlleleCountSource.Setup(c => c.GetGappedMnvRefCount(306)).Returns(10);
            mockAlleleCountSource.Setup(c => c.AddGappedMnvRefCount(It.IsAny<Dictionary<int, int>>())).Callback((Dictionary<int, int> lookup) =>
            {
                Assert.Equal(1, lookup.Count);
                Assert.True(lookup.ContainsKey(306));
                Assert.Equal(10, lookup[306]);
            });

            config.MinCoverage = 0;
            config.MinVariantQscore = 0;
            config.MinFrequency = 0;

            variantCaller = new AlleleCaller(config);

            var candidateVariants = new List<CandidateAllele>
            {
                passingMnv,
                passingDeletion,
                passingSnv
            };

            var BaseCalledAlleles = variantCaller.Call(new CandidateBatch(candidateVariants), mockAlleleCountSource.Object).Values.SelectMany(v => v);

            PrintResults(BaseCalledAlleles.ToList());

            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingMnv, 10))); // Passing MNV should have additional support from big failed MNV
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingSnv, 200))); // Passing SNV should have coverage that includes the passing MNV but not support
            Assert.True(BaseCalledAlleles.Any(v => MatchVariants(v, passingDeletion))); // Passing deletion should not do anything here

            Assert.Equal((3 * HighCoverageMultiplier) - passingMnv.Support, (BaseCalledAlleles.First(v => MatchVariants(v, passingSnv))).ReferenceSupport); // Passing SNV should have coverage that includes the passing MNV but not support

        }

        [Fact]
        public void CallVariants_MnvReallocatesToDifferentBlock()
        {
            var config = new VariantCallerConfig
            {
                MaxVariantQscore = 100,
                EstimatedBaseCallQuality = 20,
                IncludeReferenceCalls = true,
                MinCoverage = 0,
                MinVariantQscore = 0,
                MinFrequency = 6f / 150,
                ChrReference = new ChrReference
                {
                    Sequence = "ACGTACGT",
                    Name = "Boo"
                },
                GenotypeCalculator = new SomaticGenotypeCalculator()
            };

            var variantCaller = new AlleleCaller(config);

            var passingMnv = new CandidateAllele("chr1", 1999, "TTT", "CCC", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 10, 0, 0 }
            };

            var failingMnv = new CandidateAllele("chr1", 2000, "TTT", "GGG", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };
            var failingMnv2 = new CandidateAllele("chr1", 1999, "TTT", "AAA", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };
            var failingGappedMnv = new CandidateAllele("chr1", 2000, "TTT", "ATA", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };


            var mockStateManager = MockStateManager(306, 0);


            variantCaller = new AlleleCaller(config);

            var candidateVariants = new List<CandidateAllele>
            {
                passingMnv,
                failingMnv,
                failingMnv2,
                failingGappedMnv
            };

            var batch = new CandidateBatch(candidateVariants) { MaxClearedPosition = 2000 };

            var BaseCalledAlleles = variantCaller.Call(batch, mockStateManager.Object);
            mockStateManager.Setup(c => c.AddCandidates(It.IsAny<IEnumerable<CandidateAllele>>()))
                .Callback((IEnumerable<CandidateAllele> vars) => Console.WriteLine(vars.Count()));
            mockStateManager.Verify(c => c.AddCandidates(It.IsAny<IEnumerable<CandidateAllele>>()), Times.Once);

            // For regular MNVs that span blocks, whole sub-MNV belonging to next block should be passed over together.
            // If it begins with a ref, should skip that ref and just deliver the rest of the MNV. Thus we should have the following added to the next block:
            //  - MNV at 2001 from failingMnv
            //  - SNV at 2001 from failingMnv2
            //  - SNV at 2002 from failingGappedMnv

            mockStateManager.Verify(c => c.AddCandidates(It.Is<IEnumerable<CandidateAllele>>(x => x.Count() == 3)), Times.Once);
            mockStateManager.Verify(c => c.AddCandidates(It.Is<IEnumerable<CandidateAllele>>(x =>
                x.Count(a => a.Coordinate == 2001) == 2
                && x.Count(a => a.Coordinate == 2002) == 1)),
                Times.Once);
            mockStateManager.Verify(c => c.AddCandidates(It.Is<IEnumerable<CandidateAllele>>(x =>
                x.Count(a => a.Coordinate == 2001 && a.Type == AlleleCategory.Mnv) == 1
                && x.Count(a => a.Coordinate == 2001 && a.Type == AlleleCategory.Snv) == 1
                && x.Count(a => a.Coordinate == 2001 && a.Type == AlleleCategory.Reference) == 0
                && x.Count(a => a.Coordinate == 2002 && a.Type == AlleleCategory.Snv) == 1
                )),
                Times.Once);

            var variants = BaseCalledAlleles.Values.SelectMany(v => v);
            PrintResults(variants.ToList());

            Assert.True(variants.Any(v => MatchVariants(v, passingMnv, 10))); // Passing MNV should have additional support from big failed MNV

        }

        [Fact]
        public void CallVariants_MnvReallocatesToSnvOutsideInterval()
        {
            var config = new VariantCallerConfig
            {
                MaxVariantQscore = 100,
                EstimatedBaseCallQuality = 20,
                IncludeReferenceCalls = true,
                MinFrequency = 6f / 150,
                ChrReference = new ChrReference
                {
                    Sequence = "ACGTACGT",
                    Name = "Boo"
                },
                GenotypeCalculator = new SomaticGenotypeCalculator()
            };

            var intervalSet = new ChrIntervalSet(new List<Region>() { new Region(1900, 1950) }, "chr1");
            var variantCaller = new AlleleCaller(config, intervalSet);

            // -----------------------------------------------
            // Passing MNV that spans interval edge should be called if it begins within intervals
            // Failing MNVs that span interval edge and are reallocated to SNVs should only have those SNVs called if they are within intervals
            // (broken-out SNVs outside intervals should not be called even if they gain enough support to be called).
            // -----------------------------------------------

            var passingMnv = new CandidateAllele("chr1", 1950, "TTT", "CCC", AlleleCategory.Mnv)
            {
                SupportByDirection = new[] { 10, 0, 0 }
            };
            var failingMnv1 = new CandidateAllele("chr1", 1950, "TTT", "GGG", AlleleCategory.Mnv) // only the first SNV should be called (1950 T>G)
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };
            var failingMnv1Booster = new CandidateAllele("chr1", 1949, "TTTT", "GGGG", AlleleCategory.Mnv) // only the second SNV should be called (1950 T>G)
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };
            var failingMnv2 = new CandidateAllele("chr1", 1950, "TTT", "AAA", AlleleCategory.Mnv) // none of these should be called
            {
                SupportByDirection = new[] { 5, 0, 0 }
            };

            var mockStateManager = MockStateManager(306, 0);

            var candidateVariants = new List<CandidateAllele>
            {
                passingMnv,
                failingMnv1,
                failingMnv2,
                failingMnv1Booster
            };

            var batch = new CandidateBatch(candidateVariants) { MaxClearedPosition = 2000 };

            var BaseCalledAlleles = variantCaller.Call(batch, mockStateManager.Object).Values.SelectMany(v => v);
            PrintResults(BaseCalledAlleles.ToList());

            Assert.Equal(2, BaseCalledAlleles.Count());
        }

        [Fact]
        public void GetRefSupportFromGappedMnvs()
        {
            var calledAlleles = new List<CalledAllele>()
            {
                //Ref gap at 13
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Coordinate = 12,
                    Reference = "ATG",
                    Alternate = "CTA",
                    AlleleSupport = 15
                },

                //Ref gap at 124
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Coordinate = 123,
                    Reference = "ATG",
                    Alternate = "CTA",
                    AlleleSupport = 25
                },
                //Different allele with ref gap at 124
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Coordinate = 121,
                    Reference = "ATATG",
                    Alternate = "CACTA",
                    AlleleSupport = 11
                },
                //No ref gaps
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Coordinate = 456,
                    Reference = "ACG",
                    Alternate = "CTA",
                    AlleleSupport = 25
                },
                //2 ref gaps at 78901 and 78903
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Coordinate = 78900,
                    Reference = "ATGCA",
                    Alternate = "CTACT",
                    AlleleSupport = 25
                },
                //Deletion shouldn't contribute
                new CalledAllele(AlleleCategory.Deletion)
                {
                    Coordinate = 91000,
                    Reference = "ATGC",
                    Alternate = "A",
                    AlleleSupport = 25
                },
                //Insertion shouldn't contribute
                new CalledAllele(AlleleCategory.Insertion)
                {
                    Coordinate = 92000,
                    Reference = "A",
                    Alternate = "AT",
                    AlleleSupport = 25
                },
                //SNV shouldn't contribute
                new CalledAllele(AlleleCategory.Snv)
                {
                    Coordinate = 93000,
                    Reference = "A",
                    Alternate = "C",
                    AlleleSupport = 25
                },

            };
            var takenRefSupport = AlleleCaller.GetRefSupportFromGappedMnvs(calledAlleles);

            //Single allele with ref gap at 13
            Assert.False(takenRefSupport.ContainsKey(12));
            Assert.False(takenRefSupport.ContainsKey(14));
            Assert.True(takenRefSupport.ContainsKey(13));
            Assert.Equal(15, takenRefSupport[13]);

            //Two alleles contributing ref gap at 124, for a total of 36
            Assert.False(takenRefSupport.ContainsKey(123));
            Assert.False(takenRefSupport.ContainsKey(125));
            Assert.True(takenRefSupport.ContainsKey(124));
            Assert.Equal(36, takenRefSupport[124]);

            //No ref gaps at 456 or surrounding
            Assert.False(takenRefSupport.ContainsKey(456));
            Assert.False(takenRefSupport.ContainsKey(457));
            Assert.False(takenRefSupport.ContainsKey(458));

            //5-base MNV with 2 ref gaps at 78901 and 78903
            Assert.False(takenRefSupport.ContainsKey(78900));
            Assert.False(takenRefSupport.ContainsKey(78902));
            Assert.False(takenRefSupport.ContainsKey(78904));
            Assert.True(takenRefSupport.ContainsKey(78901));
            Assert.True(takenRefSupport.ContainsKey(78903));
            Assert.Equal(25, takenRefSupport[78901]);
            Assert.Equal(25, takenRefSupport[78903]);

            //Other allele types shouldn't contribute to the tracking
            Assert.False(takenRefSupport.ContainsKey(91000));
            Assert.False(takenRefSupport.ContainsKey(92000));
            Assert.False(takenRefSupport.ContainsKey(93000));

        }

        private static void PrintResults(List<CalledAllele> calledAlleles)
        {
            Console.WriteLine("--------------------");
            foreach (var baseCalledAllele in calledAlleles)
            {
                Console.WriteLine(baseCalledAllele.Coordinate + " " + baseCalledAllele.Reference + " > " + baseCalledAllele.Alternate + " : " +
                                  baseCalledAllele.AlleleSupport + " freq: " + baseCalledAllele.Frequency);
            }
        }


        private bool MatchVariants(CalledAllele BaseCalledAllele, CandidateAllele candidateVariant, int? expectedSupport = null, float? expectedFreq = null)
        {
            if (BaseCalledAllele.Chromosome == candidateVariant.Chromosome
                && BaseCalledAllele.Coordinate == candidateVariant.Coordinate
                && BaseCalledAllele.Reference == candidateVariant.Reference
                && BaseCalledAllele.Alternate == candidateVariant.Alternate
                && BaseCalledAllele.Type == candidateVariant.Type
                && (expectedFreq == null || BaseCalledAllele.Frequency == expectedFreq)
                && (expectedSupport == null || BaseCalledAllele.AlleleSupport == expectedSupport)
                )
                return true;
            return false;
        }

        private Mock<IAlleleSource> MockStateManager(int highCoverageCoordinate, int lowCoverageCoordinate)
        {
            var mockAlleleCountSource = new Mock<IAlleleSource>();
            mockAlleleCountSource.Setup(
            s => s.GetAlleleCount(It.IsAny<int>(),
                It.IsAny<AlleleType>(),
                It.IsAny<DirectionType>())).Returns(NormalCoverageMultiplier);
            mockAlleleCountSource.Setup(
            s => s.GetAlleleCount(highCoverageCoordinate,
                It.IsAny<AlleleType>(),
                It.IsAny<DirectionType>())).Returns(HighCoverageMultiplier);
            mockAlleleCountSource.Setup(
            s => s.GetAlleleCount(lowCoverageCoordinate,
                It.IsAny<AlleleType>(),
                It.IsAny<DirectionType>())).Returns(LowCoverageMultiplier);

            return mockAlleleCountSource;
        }
    }
}