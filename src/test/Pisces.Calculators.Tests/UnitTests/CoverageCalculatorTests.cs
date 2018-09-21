using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Moq;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.RegionState;
using Xunit;
using Constants = Pisces.Domain.Constants;

namespace Pisces.Calculators.Tests
{
    public class CoverageCalculatorTests
    {
        private const int Coordinate_CountsPerAllele = 1000;
        private const int CoordinatePlusOne_CountsPerAllele = 10;
        private const int CoordinatePlusLengthInclusive_CountsPerAllele = 100;

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Point_HappyPath()
        {
            var variant = new CalledAllele(AlleleCategory.Snv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                AlleleSupport = 10
            };

            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount(AlleleType.T)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 100,101,111})
             },
             //Ref allele
             new AlleleCount(AlleleType.A)
             {
                 //AlleleType = AlleleType.A,
                 Coordinate = 1,  
                 DirectionCoverage = GetDirectionCoverage(new []{ 1,2,0})
             },
             //Coverage should consider other non-ref alleles, but ref support should not
             new AlleleCount(AlleleType.C)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 5,10,1})
             }
            },
            new []
            {
                106,
                113,
                112   //Stitched coverage is not reallocated here in the point-mutation case,
            },
            106+113+112,
            expectedSnvRef:3);
        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Point_WithGappedMnvTakingSupport()
        {
            var variant = new CalledAllele(AlleleCategory.Snv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                AlleleSupport = 10
            };

            //Although we make total ref support 53 below, 50 of it is "taken" by a gapped MNV, so we only expect 3 true ref support
            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
                new AlleleCount(AlleleType.T)
                {
                    Coordinate = 1, // coverage should only take into account the coordinate we're at
                    DirectionCoverage = GetDirectionCoverage(new[] {100, 101, 111})
                },
                new AlleleCount(AlleleType.A)
                {
                    Coordinate = 1, // coverage should only take into account the coordinate we're at
                    DirectionCoverage = GetDirectionCoverage(new[] {21, 32, 0})
                }

            },
                new[]
                {
                    121,
                    133,
                    111
                },
                121+133+111,
                expectedSnvRef: 3, takenRefSupport: 50);
        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Point_WithGappedMnvTakingSupport_Capped()
        {
            // test again but with when allele count depth is less than "taken" support, make sure we cap at 0
            // this is possible when collapsing and gapped MNVs suck up
            // support from ref bases that are low quality (those ref bases were never added to allele count)
            var refAllele = new CalledAllele()
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "A",
                AlleleSupport = 10
            };

            ComputeCoverageTest(refAllele, new List<AlleleCount>()
            {
             new AlleleCount(AlleleType.T)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 100,101,111})
             },
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 21,32,0})
             }

            },
            new[]
            {
                121,
                133,
                111
            },
             121+133+111,
            expectedSnvRef: 0, takenRefSupport: 150);

            // make sure snv is capped too
            var variant = new CalledAllele(AlleleCategory.Snv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                AlleleSupport = 10
            };

            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount(AlleleType.T)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 100,101,111})
             },
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = GetDirectionCoverage(new []{ 21,32,0})
             }

            },
            new[]
            {
                121,
                133,
                111
            },
            121+133+111,
            expectedSnvRef: 0, takenRefSupport: 150);

        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_ZeroCoverage()
        {
            var variant = new CalledAllele(AlleleCategory.Deletion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "ATCG",
                AlternateAllele = "A",
                AlleleSupport = 0
            };

            Action test = () => ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 2,
                 DirectionCoverage = GetDirectionCoverage(new []{ 0,0,0})
             },
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 4,
                 DirectionCoverage = GetDirectionCoverage(new []{ 0,0,0})
             }
            }, 
            new []
            {
                0, 0, 0
            },0, false);

            test();

            //Reference support should be 0
            Assert.Equal(0, variant.ReferenceSupport);

            //Frequency should be 0 (and not barf)
            Assert.Equal(0, variant.Frequency);

            //Now try the case where the VariantSupport is non-zero but the 
            //allele counts are zero (shouldn't happen but don't barf)
            variant.AlleleSupport = 10;
            test();

            //Reference support should be 0
            Assert.Equal(0, variant.ReferenceSupport);

            //Frequency should be 0 (and not barf)
            Assert.Equal(0, variant.Frequency);

        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_SupportGreaterThanCoverage()
        {
            //This shouldn't happen but don't barf
            var variant = new CalledAllele(AlleleCategory.Deletion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "ATCG",
                AlternateAllele = "A",
                AlleleSupport = 20
            };

            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 2,
                 DirectionCoverage = GetDirectionCoverage(new []{ 1, 1, 1})
             },
             new AlleleCount(AlleleType.A)
             {
                 Coordinate = 4,
                 DirectionCoverage = GetDirectionCoverage(new []{ 1, 1, 1})
             }
            }, 
            new []
            {
                2, 1, 0
            }, 
            2+1,
            false, 100);

            //Reference support should be 0
            Assert.Equal(0, variant.ReferenceSupport);
        }

        [Fact]
        public void ComputeCoverage_Insertions()
        {
            // All well covered
            var insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
            {
                new AlleleCount(AlleleType.T)
                {
                    Coordinate = 1,
                    DirectionCoverage = GetDirectionCoverage(new[] {10, 100, 20}, false),
                },
                new AlleleCount(AlleleType.C)
                {
                    Coordinate = 2,
                    DirectionCoverage = GetDirectionCoverage(new[] {30, 50, 200}, false),
                }
            },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0);

            // All well-anchored, including right side matching first base of insertion
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 } })
                    },
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,20, 0, 0, 0, 0, 0 }, {0,0,0,0,0,30, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 } })
                    },
                    new AlleleCount(AlleleType.T)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 }, {0,0,0,0,0,90, 0, 0, 0, 0, 0 } })
                    }

                },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0);

            // Demonstrating boundary cases responsive to insertion length - length 3 so min anchor 3
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG",
                SupportByDirection = new[] { 0, 0, 5 },
                WellAnchoredSupportByDirection = new[] { 0, 0, 5 },
                WellAnchoredSupport = 5,
                AlleleSupport = 5
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,1000, 0, 0, 0, 0, 0 }, {0,0,0,0,0,200, 0, 0, 0, 0, 0 } }) //200, 1100 
                    },
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,5,0,0,15, 0, 0, 0, 0, 0 }, {0,0,0,10,0,20, 0, 0, 0, 0, 0 }, {0,10,20,0,0,70, 0, 0, 0, 0, 0 } }) //70, 80
                    },
                    new AlleleCount(AlleleType.G)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,2,0,3,5, 0, 0, 0, 0, 0 }, {0,4,0,0,6,10, 0, 0, 0, 0, 0 }, {0,0,0,10,20,60, 0, 0, 0, 0, 0 } }) // 55, 65, (min anchor 2: 55, 61), (min anchor 3: 53, 61)
                    }

                },
                new[] // expect min
                {
                    125, 145, 0
                },
                125 + 145 + 0, suspiciousCoverage: 6, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 123, 141 });

            // Shorter insertion - more counts are fair game
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATC",
                SupportByDirection = new[] { 0, 0, 5 },
                WellAnchoredSupportByDirection = new[] { 0, 0, 5 },
                WellAnchoredSupport = 5,
                AlleleSupport = 5
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,1000, 0, 0, 0, 0, 0 }, {0,0,0,0,0,200, 0, 0, 0, 0, 0 } })
                    },
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,15, 0, 0, 5, 0, 0 }, {0,0,0,0,0,20, 0, 10, 0, 0, 0 }, {0,0,0,0,0,70, 0, 0, 20, 10, 0 } }) //70, 80
                    },
                    new AlleleCount(AlleleType.T)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,5, 3, 0, 2, 0, 0 }, {0,0,0,0,0,10, 6, 0, 0, 4, 0 }, {0,0,0,0,0,60, 20, 10, 0, 0, 0 } }) // 55, 65, (min anchor 2: 55, 61), (min anchor 3: 53, 61)
                    }

                },
                new[] // expect min
                {
                    125, 145, 0
                },
                125 + 145 + 0, suspiciousCoverage: 4, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 125, 141 });


            // All right side poorly-anchored, including base matching first base of insertion - take coverage from others but not that one
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10,0,0,0,0,0}, {0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 } }) // 20, 110, 0
                    },
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{20,0,0,0,0,0, 0, 0, 0, 0, 0 }, {30,0,0,0,0,0, 0, 0, 0, 0, 0 }, {100,0,0,0,0,0, 0, 0, 0, 0, 0 } }) //70, 80, 0
                    },
                    new AlleleCount(AlleleType.G)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{10,0,0,0,0,0, 0, 0, 0, 0, 0 }, {20,0,0,0,0,0, 0, 0, 0, 0, 0 }, {90,0,0,0,0,0, 0, 0, 0, 0, 0 } })
                    }

                },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0, suspiciousCoverage: 120, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 20, 80, 0 });


            // Everything well-anchored except base matching first base of insertion - take coverage from others but not that one
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 } }) // 20, 110, 0
                    },
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,20, 0, 0, 0, 0, 0 }, {0,0,0,0,0,30, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 } }) //70, 80, 0
                    },
                    new AlleleCount(AlleleType.G)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{10,0,0,0,0,0, 0, 0, 0, 0, 0 }, {20,0,0,0,0,0, 0, 0, 0, 0, 0 }, {90,0,0,0,0,0, 0, 0, 0, 0, 0 } })
                    }

                },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0, suspiciousCoverage: 120, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 20, 80, 0 });


            // Only coverage on the right side is poorly-anchored base matching first base of insertion
            // Ends up min-ing out to 0. TODO determine if there's a better behavior in these extremes.
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 } }) // 20, 110, 0
                    },
                    new AlleleCount(AlleleType.G)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{30,0,0,0,0,0, 0, 0, 0, 0, 0 }, {50,0,0,0,0,0, 0, 0, 0, 0, 0 }, {200,0,0,0,0,0, 0, 0, 0, 0, 0 } })
                    }

                },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0, suspiciousCoverage: 20 + 110 + 0, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 0, 0, 0 }, checkAux: false);

           
            // Only coverage on the right side is poorly-anchored base matching first base of insertion, and a tiny bit of other coverage
            // Ends up min-ing out to very low number so we're going to get very high VAF. TODO determine if there's a better behavior in these extremes.
            insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
                {
                    new AlleleCount(AlleleType.A)
                    {
                        Coordinate = 2,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,10, 0, 0, 0, 0, 0 }, {0,0,0,0,0,100, 0, 0, 0, 0, 0 }, {0,0,0,0,0,20, 0, 0, 0, 0, 0 } }) // 20, 110, 0
                    },
                    new AlleleCount(AlleleType.G)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{29,0,0,0,0,0, 0, 0, 0, 0, 0 }, {49,0,0,0,0,0, 0, 0, 0, 0, 0 }, {199,0,0,0,0,0, 0, 0, 0, 0, 0 } })
                    },
                    new AlleleCount(AlleleType.C)
                    {
                        Coordinate = 1,
                        DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,1, 0, 0, 0, 0, 0 }, {0,0,0,0,0,1, 0, 0, 0, 0, 0 }, {0,0,0,0,0,1, 0, 0, 0, 0, 0 } })
                    }
                },
                new[] // expect min
                {
                    20, 110, 0
                },
                20 + 110 + 0, suspiciousCoverage: 18 + 109 + 0, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 2, 1, 0 }, checkAux: false);


                // Extreme cases: Insertion on the very edge of an amplicon (no tiling, or last amp in tile)
                // Het repeat insertion at end of amplicon
                //      12345 6789
                // PROBE:       acxxx 
                // REF: XXXXA TTAC
                // POS: XXXXATTtac (start:1, end: 6)
                // ANC:     2i1sss
                // NEG: XXXXAT tac (start:1, end: 6)
                // ANC:     21 sss
                insertion = new CalledAllele(AlleleCategory.Insertion)
                {
                    ReferencePosition = 5,
                    ReferenceAllele = "A",
                    AlternateAllele = "AT",
                    AlleleSupport = 50
                };

                // Without anchor awareness, we would get 50 / 49 = 100% ins VAF?
                // With anchor awareness, we get 50 / 0 = undefined VAF?
                // Both seem unfortunate but at least in second case we're not over-confident?
                ComputeCoverageTest(insertion, new List<AlleleCount>()
                    {
                        new AlleleCount(AlleleType.A)
                        {
                            Coordinate = 6,
                            DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,100,0,0,0,0, 0, 0, 0, 0, 0 } }) // 50,50
                        },
                        new AlleleCount(AlleleType.T)
                        {
                            Coordinate = 5,
                            DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {98,0,0,0,0,0, 0, 0, 0, 0, 0 } }) // 25,25
                        },
                    },
                    new[] // expect min
                    {
                        49, 49, 0
                    }, 49 + 49 + 0, suspiciousCoverage: 49 + 49 + 0, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 0, 0, 0 }, checkAux: false);


                // Het repeat insertion at end of amplicon, with a concurrent SNV
                //      12345 6789
                // PROBE:       acxxx 
                // REF: XXXXA TTAC
                // INS: XXXXATTTac (start:1, end: 7)
                // ANC:     32i1ss
                // SNV: XXXXAT Gac (start:1, end: 7)
                // ANC:     32 1ss
                insertion = new CalledAllele(AlleleCategory.Insertion)
                {
                    ReferencePosition = 5,
                    ReferenceAllele = "A",
                    AlternateAllele = "AT",
                    AlleleSupport = 50
                };

                // Without anchor awareness, we would get 50 / 94 = ~50% ins VAF, which is "right"
                // With anchor awareness, IF the ins support is only from anchored sources, we get 50 / 46 = 100% ins VAF. 
                // ... If the ins support is also from unanchored, we get close to the "right" vaf
                ComputeCoverageTest(insertion, new List<AlleleCount>()
                    {
                        new AlleleCount(AlleleType.A)
                        {
                            Coordinate = 6,
                            DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,100,0,0,0,0, 0, 0, 0, 0, 0 } }) // 50,50
                        },
                        new AlleleCount(AlleleType.T)
                        {
                            Coordinate = 5,
                            DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {48,0,0,0,0,0, 0, 0, 0, 0, 0 } }) // 24,24
                        },
                        new AlleleCount(AlleleType.G)
                        {
                            Coordinate = 5,
                            DirectionCoverage = GetDirectionCoverage(new[,] {{0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {0,0,0,0,0,0, 0, 0, 0, 0, 0 }, {46,0,0,0,0,0, 0, 0, 0, 0, 0 } }) // 23,23
                        },
                    },
                    new[] // expect min
                    {
                        47, 47, 0
                    },
                    47 + 47 + 0, suspiciousCoverage: 24 + 24 + 0, expectedClosestIntCoverageByDirectionAnchorAware: new[] { 23, 23, 0 }, checkAux: false);

        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Spanning_HappyPath()
        {
            var deletion = new CalledAllele(AlleleCategory.Deletion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "ATCG",
                AlternateAllele = "A"
            };

            ComputeCoverageTest(deletion, new List<AlleleCount>()
            {
                new AlleleCount(AlleleType.A)
                {
                    Coordinate = 2,
                    DirectionCoverage = GetDirectionCoverage(new[] {10, 100, 20}),
                },
                new AlleleCount(AlleleType.A)
                {
                    Coordinate = 4,
                    DirectionCoverage = GetDirectionCoverage(new[] {30, 50, 200}),
                }
                },
                new[] // expect internal average
                {
                    75, 130, 0
                },
                75 + 130);





            var mnv = new CalledAllele(AlleleCategory.Mnv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "CATG",
                AlternateAllele = "ATCA"
            };

            // For mnvs, take min of first and last datapoints.
            ComputeCoverageTest(mnv, new List<AlleleCount>()
            {
                new AlleleCount(AlleleType.C)
                {
                    Coordinate = 1,
                    DirectionCoverage = GetDirectionCoverage(new[] {10, 100, 20}), //-> 20,110,0 
                },
                new AlleleCount(AlleleType.G)
                {
                    Coordinate = 4,
                    DirectionCoverage = GetDirectionCoverage(new[] {30, 50, 200}), // -> 130,150,0
                }
            },
                new[] // expect internal average // - > (20 + 130)/2 , 110+150 /2 = 75,150,0 
                {
                    75, 130, 0
                },
                75+130+0);

            // For mnvs, take min of first and last datapoints.
            ComputeCoverageTest(mnv, new List<AlleleCount>()
            {
                new AlleleCount(AlleleType.A)
                {
                    Coordinate = 1,
                    DirectionCoverage = GetDirectionCoverage(new[] {5, 5, 0}), 
                },
                new AlleleCount(AlleleType.A)
                {
                    Coordinate = 4,
                    DirectionCoverage = GetDirectionCoverage(new[] {10, 10, 0}), 
                }
            },
                new[] // expect internal average // - > (1 + 2)/2 , (1+2) /2 = 1.5,1.5
                {
                    7, 7, 0   //problem here is that we now have total coverage of 14 when we started with 15!!
                },
                15  //7.5+7.5, sop we dont loose that fraction.) 
                );
        }

        private void ComputeCoverageTest(CalledAllele variant, List<AlleleCount> stagedCounts,
            int[] expectedClosestIntCoverageByDirection, int expectedCoverage, bool checkAux = true,
            int alleleSupport = 5, int expectedSnvRef = 0, int takenRefSupport = 0, int[] expectedClosestIntCoverageByDirectionAnchorAware = null, int suspiciousCoverage = 0)
        {
            // No consideration for anchoring
            var fullyAnchorSupportedVariant = new CalledAllele(variant);
            fullyAnchorSupportedVariant.WellAnchoredSupport = 5;
            fullyAnchorSupportedVariant.AlleleSupport = 5;
            fullyAnchorSupportedVariant.SupportByDirection = new[] { 0, 0, 5 };
            fullyAnchorSupportedVariant.WellAnchoredSupportByDirection = new[] { 0, 0, 5 };

            ComputeCoverageTestInternal(fullyAnchorSupportedVariant, stagedCounts, expectedClosestIntCoverageByDirection, expectedCoverage, checkAux, alleleSupport, expectedSnvRef, takenRefSupport, false);


            if (variant.Type != AlleleCategory.Insertion)
            {
                return;
            }

            // Consideration for anchoring - 0 unanchored, unanchored coverage weight should be 0
            fullyAnchorSupportedVariant = new CalledAllele(variant);
            fullyAnchorSupportedVariant.WellAnchoredSupport = 5;
            fullyAnchorSupportedVariant.AlleleSupport = 5;
            fullyAnchorSupportedVariant.SupportByDirection = new[] { 0, 0, 5 };
            fullyAnchorSupportedVariant.WellAnchoredSupportByDirection = new[] { 0, 0, 5 };

            ComputeCoverageTestInternal(fullyAnchorSupportedVariant, stagedCounts, expectedClosestIntCoverageByDirectionAnchorAware ?? expectedClosestIntCoverageByDirection, expectedClosestIntCoverageByDirectionAnchorAware?.Sum() ?? expectedCoverage - suspiciousCoverage, checkAux, alleleSupport, expectedSnvRef, takenRefSupport, true, expectedUnanchoredCoverageWeight: 0);


            // Consideration for anchoring - all support is unanchored. Count everything.
            var fullyUnanchoredSupportedVariant = new CalledAllele(variant);
            fullyUnanchoredSupportedVariant.WellAnchoredSupport = 0;
            fullyUnanchoredSupportedVariant.AlleleSupport = 5;
            fullyUnanchoredSupportedVariant.SupportByDirection = new[] { 0, 0, 5 };
            fullyUnanchoredSupportedVariant.WellAnchoredSupportByDirection = new[] { 0, 0, 0 };

            ComputeCoverageTestInternal(fullyUnanchoredSupportedVariant, stagedCounts, expectedClosestIntCoverageByDirection, expectedCoverage, checkAux, alleleSupport, expectedSnvRef, takenRefSupport, true, expectedUnanchoredCoverageWeight: 1); //weight always 1 if anchored support 0


            // Consideration for anchoring - equal support is unanchored. Count everything.
            var equalSupportAnchoredUnanchored = new CalledAllele(variant);
            var supportFromUnanchored = suspiciousCoverage * 0.5f;
            var totalSupport = (int)(supportFromUnanchored + (0.5f*(expectedCoverage - suspiciousCoverage)));
            equalSupportAnchoredUnanchored.WellAnchoredSupport = (int)(totalSupport - supportFromUnanchored);
            equalSupportAnchoredUnanchored.AlleleSupport = totalSupport;
            equalSupportAnchoredUnanchored.SupportByDirection = new[] { 0, 0, totalSupport };
            equalSupportAnchoredUnanchored.WellAnchoredSupportByDirection = new[] { 0, 0, (int)(totalSupport - supportFromUnanchored) };

            ComputeCoverageTestInternal(equalSupportAnchoredUnanchored, stagedCounts, expectedClosestIntCoverageByDirection, expectedCoverage, checkAux, alleleSupport, expectedSnvRef, takenRefSupport, true, expectedUnanchoredCoverageWeight: variant.Type == AlleleCategory.Insertion && suspiciousCoverage > 0 ? 1 : 0);

            // Consideration for anchoring - in between
            for (int i = 0; i < expectedCoverage/20; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var totalsupport = Math.Max(expectedCoverage, 20 * i);
                    var partlyUnanchorSupportedVariant = new CalledAllele(variant);
                    partlyUnanchorSupportedVariant.WellAnchoredSupport = j * totalSupport/10;
                    partlyUnanchorSupportedVariant.AlleleSupport = totalSupport;
                    ComputeCoverageTestInternalWeighted(partlyUnanchorSupportedVariant, stagedCounts, expectedClosestIntCoverageByDirectionAnchorAware != null ? expectedClosestIntCoverageByDirectionAnchorAware.Sum() : expectedCoverage - suspiciousCoverage, expectedCoverage, 0, 1);
                }

            }

        }


        private void ComputeCoverageTestInternalWeighted(CalledAllele variant, List<AlleleCount> stagedCounts,
            int expectedCoverageLowerBound, int expectedCoverageUpperBound, float expectedWeightLowerBound, float expectedWeightUpperBound)
        {
            var mockStateManager = CreateMockStateManager(stagedCounts, 0);

            new CoverageCalculator(true).Compute(variant, mockStateManager);
            Assert.True(expectedCoverageLowerBound <= variant.TotalCoverage);
            Assert.True(expectedCoverageUpperBound >= variant.TotalCoverage);

            Assert.True(expectedWeightLowerBound <= variant.UnanchoredCoverageWeight);
            Assert.True(expectedWeightUpperBound >= variant.UnanchoredCoverageWeight);

        }

        private void ComputeCoverageTestInternal(CalledAllele variant, List<AlleleCount> stagedCounts,  
            int[] expectedClosestIntCoverageByDirection, int expectedCoverage, bool checkAux = true, int alleleSupport = 5, int expectedSnvRef = 0, int takenRefSupport = 0, bool considerAnchor = false, float expectedUnanchoredCoverageWeight = 0)
        {
            //variant.AlleleSupport = alleleSupport;

            var mockStateManager = CreateMockStateManager(stagedCounts, takenRefSupport);
            
            new CoverageCalculator(considerAnchor).Compute(variant, mockStateManager);
            Assert.Equal(expectedCoverage, variant.TotalCoverage);

            if (expectedClosestIntCoverageByDirection != null)
            {
                for (var i = 0; i < expectedClosestIntCoverageByDirection.Length; i++)
                    Assert.Equal(expectedClosestIntCoverageByDirection[i], variant.EstimatedCoverageByDirection[i]);
            }

            if (checkAux)
            {
                //"Reference" Support should be the coverage less the variant support, if we don't have an SNV
                if (variant.Type != AlleleCategory.Reference)
                {
                    if (variant.Type != AlleleCategory.Snv && variant.Type != AlleleCategory.Reference)
                        Assert.Equal(expectedCoverage - variant.AlleleSupport, variant.ReferenceSupport);
                    else
                        Assert.Equal(expectedSnvRef, variant.ReferenceSupport);

                }
                else
                {
                    Assert.Equal(expectedSnvRef, variant.AlleleSupport);
                }

                //Frequency should be support/coverage
                Assert.Equal((float) variant.AlleleSupport/expectedCoverage, variant.Frequency);
            }

            Assert.Equal(expectedUnanchoredCoverageWeight, variant.UnanchoredCoverageWeight);

        }

        private IAlleleSource CreateMockStateManager(List<AlleleCount> states, int refCounts = 0)
        {
            var mockAlleleCountSource = new Mock<IAlleleSource>();

            var numAnchorTypes = 5;
            var regionState = new RegionState(1, 1000, numAnchorTypes);

            
            foreach (var state in states)
            {
                for (var directionIndex = 0; directionIndex < Constants.NumDirectionTypes; directionIndex++)
                {
                    for (var anchorIndex = 0; anchorIndex < numAnchorTypes * 2 + 1; anchorIndex++)
                    {
                        for (var i = 0; i < state.DirectionCoverage[directionIndex, anchorIndex]; i++)
                        {
                            regionState.AddAlleleCount(state.Coordinate, state.AlleleType, (DirectionType)directionIndex, anchorIndex);
                        }
                    }
                }
                mockAlleleCountSource.Setup(
                        s => s.GetAlleleCount(state.Coordinate,
                            state.AlleleType,
                            It.IsAny<DirectionType>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>()))
                    .Returns((int c, AlleleType a, DirectionType d, int minAnchor, int? maxAnchor, bool fromEnd, bool symm) =>
                        {
                            return regionState.GetAlleleCount(c, a, d, minAnchor, maxAnchor, fromEnd, symm);
                        }
                        );

            }

            mockAlleleCountSource.Setup(c => c.GetGappedMnvRefCount(It.IsAny<int>())).Returns(refCounts);

            return mockAlleleCountSource.Object;
        }

        private int[,] GetDirectionCoverage(int[,] directionCoverage, int numAnchorTypes = 5)
        {
            // Make sure the array passed in matches to how we plan to stage it
            Assert.Equal(3 * (numAnchorTypes * 2 + 1), directionCoverage.Length);
            return directionCoverage;
        }

        private int[,] GetDirectionCoverage(int[] directionCoverageIgnoreAnchor, bool randomizeAnchorWeights = false, int numAnchorValues = 11)
        {
            var directionCoverage = new int[3, 11];
            if (directionCoverageIgnoreAnchor.Length != 3)
            {
                throw new Exception("Non-anchored direction coverage test array must be of length 3.");
            }

            if (randomizeAnchorWeights)
            {
                var random = new Random();

                for (var d = 0; d < 3; d++)
                {
                    for (var c = 0; c < directionCoverageIgnoreAnchor[d]; c++)
                    {
                        var randomAnchorValue = random.Next(0, numAnchorValues);
                        directionCoverage[d, randomAnchorValue]++;
                    }
                }

            }
            else
            {
                // Everything is well-covered
                directionCoverage[0, 5] = directionCoverageIgnoreAnchor[0];
                directionCoverage[1, 5] = directionCoverageIgnoreAnchor[1];
                directionCoverage[2, 5] = directionCoverageIgnoreAnchor[2];
            }

            return directionCoverage;
        }

        public class AlleleCount
        {
            public int Coordinate { get; set; }
            public int[,] DirectionCoverage { get; set; }

            public AlleleCount(AlleleType alleleType)
            {
                AlleleType = alleleType;
            }

            public AlleleType AlleleType { get; }
        }
    }
}
