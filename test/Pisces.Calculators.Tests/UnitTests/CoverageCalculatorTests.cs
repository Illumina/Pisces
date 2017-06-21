using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

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
             new AlleleCount()
             {
                 AlleleType = AlleleType.T,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 100,101,111}
             },
             //Ref allele
             new AlleleCount()
             {
                 AlleleType = AlleleType.A,
                 Coordinate = 1,  
                 DirectionCoverage = new []{ 1,2,0}
             },
             //Coverage should consider other non-ref alleles, but ref support should not
             new AlleleCount()
             {
                 AlleleType = AlleleType.C,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 5,10,1}
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
                new AlleleCount()
                {
                    AlleleType = AlleleType.T,
                    Coordinate = 1, // coverage should only take into account the coordinate we're at
                    DirectionCoverage = new[] {100, 101, 111}
                },
                new AlleleCount()
                {
                    AlleleType = AlleleType.A,
                    Coordinate = 1, // coverage should only take into account the coordinate we're at
                    DirectionCoverage = new[] {21, 32, 0}
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
             new AlleleCount()
             {
                 AlleleType = AlleleType.T,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 100,101,111}
             },
             new AlleleCount()
             {
                 AlleleType = AlleleType.A,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 21,32,0}
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
            CalledAllele variant = new CalledAllele(AlleleCategory.Snv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "T",
                AlleleSupport = 10
            };

            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount()
             {
                 AlleleType = AlleleType.T,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 100,101,111}
             },
             new AlleleCount()
             {
                 AlleleType = AlleleType.A,
                 Coordinate = 1,  // coverage should only take into account the coordinate we're at
                 DirectionCoverage = new []{ 21,32,0}
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
             new AlleleCount()
             {
                 Coordinate = 2,
                 DirectionCoverage = new []{ 0,0,0}
             },
             new AlleleCount()
             {
                 Coordinate = 4,
                 DirectionCoverage = new []{ 0,0,0}
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
            };

            ComputeCoverageTest(variant, new List<AlleleCount>()
            {
             new AlleleCount()
             {
                 Coordinate = 2,
                 DirectionCoverage = new []{ 1, 1, 1}
             },
             new AlleleCount()
             {
                 Coordinate = 4,
                 DirectionCoverage = new []{ 1, 1, 1}
             }
            }, 
            new []
            {
                8, 7, 0
            }, 
            8+7,
            false, 100);

            //Reference support should be 0
            Assert.Equal(0, variant.ReferenceSupport);
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
                new AlleleCount()
                {
                    Coordinate = 2,
                    DirectionCoverage = new[] {10, 100, 20}
                },
                new AlleleCount()
                {
                    Coordinate = 4,
                    DirectionCoverage = new[] {30, 50, 200}
                }
                },
                new[] // expect internal average
                {
                    375, 650, 0
                },
                375+650);

            var insertion = new CalledAllele(AlleleCategory.Insertion)
            {
                ReferencePosition = 1,
                ReferenceAllele = "A",
                AlternateAllele = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 1,
                    DirectionCoverage = new[] {10, 100, 20} 
                },
                new AlleleCount()
                {
                    Coordinate = 2,
                    DirectionCoverage = new[] {30, 50, 200} 
                }
            },
                new[] // expect min
                {
                    100, 550, 0
                },
                100+550+0);

            var mnv = new CalledAllele(AlleleCategory.Mnv)
            {
                ReferencePosition = 1,
                ReferenceAllele = "CATG",
                AlternateAllele = "ATCA"
            };

            // For mnvs, take min of first and last datapoints.
            ComputeCoverageTest(mnv, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 1,
                    DirectionCoverage = new[] {10, 100, 20} //-> 20*5,110*5,0 = 100,550,0
                },
                new AlleleCount()
                {
                    Coordinate = 4,
                    DirectionCoverage = new[] {30, 50, 200} // -> 130*5,150*5,0 = 650,750,0
                }
            },
                new[] // expect internal average // - > (100 + 650)/2 , 550+750 /2 = 375,650,0 
                {
                    375, 650, 0
                },
                375+650+0);

            // For mnvs, take min of first and last datapoints.
            ComputeCoverageTest(mnv, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 1,
                    DirectionCoverage = new[] {1, 1, 0} //-> 1*5,1*5,0 = 5,5,0
                },
                new AlleleCount()
                {
                    Coordinate = 4,
                    DirectionCoverage = new[] {2, 2, 0} // -> 2*5,2*5,0 = 10,10,0
                }
            },
                new[] // expect internal average // - > (5 + 10)/2 , 5+10 /2 = 15/2,15/2 =  7.5,7.5
                {
                    7, 7, 0   //problem here is that we now have total coverage of 14 when we started with 15!!
                },
                15  //7.5+7.5, sop we dont loose that fraction.) 
                );
        }

        private void ComputeCoverageTest(CalledAllele variant, List<AlleleCount> stagedCounts,  
            int[] expectedClosestIntCoverageByDirection, int expectedCoverage, bool checkAux = true, int alleleSupport = 5, int expectedSnvRef = 0, int takenRefSupport = 0)
        {
            variant.AlleleSupport = alleleSupport;

            var mockStateManager = CreateMockStateManager(stagedCounts, takenRefSupport);
            
            new CoverageCalculator().Compute(variant, mockStateManager);
            Assert.Equal(expectedCoverage, variant.TotalCoverage);
           
            for ( var i = 0; i < expectedClosestIntCoverageByDirection.Length; i ++)
                Assert.Equal(expectedClosestIntCoverageByDirection[i], variant.EstimatedCoverageByDirection[i]);

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
        }

        private IAlleleSource CreateMockStateManager(List<AlleleCount> states, int refCounts = 0)
        {
            var mockAlleleCountSource = new Mock<IAlleleSource>();

            foreach (var state in states)
            {
                if (state.SpecificAlleleType)
                {
                    mockAlleleCountSource.Setup(
                        s => s.GetAlleleCount(state.Coordinate,
                            state.SpecificAlleleType ? state.AlleleType : It.IsAny<AlleleType>(),
                            It.IsAny<DirectionType>()))
                        .Returns((int c, AlleleType a, DirectionType d) => state.DirectionCoverage[(int) d]);
                }
                else
                {
                    mockAlleleCountSource.Setup(
                        s => s.GetAlleleCount(state.Coordinate,
                            It.IsAny<AlleleType>(),
                            It.IsAny<DirectionType>())).Returns((int c, AlleleType a, DirectionType d) => state.DirectionCoverage[(int)d]);                    
                }
            }

            mockAlleleCountSource.Setup(c => c.GetGappedMnvRefCount(It.IsAny<int>())).Returns(refCounts);

            return mockAlleleCountSource.Object;
        }

        public class AlleleCount
        {
            private AlleleType _alleleType;
            public bool SpecificAlleleType { get; private set; }
            public int Coordinate { get; set; }
            public int[] DirectionCoverage { get; set; }

            public AlleleCount()
            {
                SpecificAlleleType = false;
            }

            public AlleleType AlleleType
            {
                get { return _alleleType; }
                set
                {
                    _alleleType = value;
                    SpecificAlleleType = true;
                }
            }
        }
    }
}
