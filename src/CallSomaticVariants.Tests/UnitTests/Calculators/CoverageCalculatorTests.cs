using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using Moq;
using Xunit;

namespace CallSomaticVariants.Logic.Calculators.Tests
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
            var variant = new CalledVariant(AlleleCategory.Snv)
            {
                Coordinate = 1,
                Reference = "A",
                Alternate = "T",
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
            expectedSnvRef:3);
        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Point_WithGappedMnvTakingSupport()
        {
            var variant = new CalledVariant(AlleleCategory.Snv)
            {
                Coordinate = 1,
                Reference = "A",
                Alternate = "T",
                AlleleSupport = 10
            };

            //Although we make total ref support 53 below, 50 of it is "taken" by a gapped MNV, so we only expect 3 true ref support
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
            expectedSnvRef: 3, takenRefSupport: 50);
            
        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_ZeroCoverage()
        {
            var variant = new CalledVariant(AlleleCategory.Deletion)
            {
                Coordinate = 1,
                Reference = "ATCG",
                Alternate = "A",
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
            }, false);

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
            var variant = new CalledVariant(AlleleCategory.Deletion)
            {
                Coordinate = 1,
                Reference = "ATCG",
                Alternate = "A",
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
            }, false, 100);

            //Reference support should be 0
            Assert.Equal(0, variant.ReferenceSupport);
        }

        [Fact]
        [Trait("ReqID", "SDS-39")]
        [Trait("ReqID", "SDS-40")]
        public void ComputeCoverage_Spanning_HappyPath()
        {
            var deletion = new CalledVariant(AlleleCategory.Deletion)
            {
                Coordinate = 1,
                Reference = "ATCG",
                Alternate = "A"
            };

            ComputeCoverageTest(deletion, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 2,
                    DirectionCoverage = new[] {10, 100, 20}  // redist = 100, 550, 0
                },
                new AlleleCount()
                {
                    Coordinate = 4,
                    DirectionCoverage = new[] {30, 50, 200} // redist = 650, 750, 0
                }
            },
                new[] // expect internal average
                {
                    375, 650, 0
                });

            var insertion = new CalledVariant(AlleleCategory.Insertion)
            {
                Coordinate = 1,
                Reference = "A",
                Alternate = "ATCG"
            };

            ComputeCoverageTest(insertion, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 1,
                    DirectionCoverage = new[] {10, 100, 20} // redist = 100, 550, 0
                },
                new AlleleCount()
                {
                    Coordinate = 2,
                    DirectionCoverage = new[] {30, 50, 200} // redist = 650, 750, 0
                }
            },
                new[] // expect min
                {
                    100, 550, 0
                });

            var mnv = new CalledVariant(AlleleCategory.Mnv)
            {
                Coordinate = 1,
                Reference = "CATG",
                Alternate = "ATCA"
            };

            // For mnvs, take min of first and last datapoints.
            ComputeCoverageTest(mnv, new List<AlleleCount>()
            {
                new AlleleCount()
                {
                    Coordinate = 1,
                    DirectionCoverage = new[] {10, 100, 20} // redist = 100, 550, 0
                },
                new AlleleCount()
                {
                    Coordinate = 4,
                    DirectionCoverage = new[] {30, 50, 200} // redist = 650, 750, 0
                }
            },
                new[] // expect internal average
                {
                    375, 650, 0
                });
        }

        private void ComputeCoverageTest(BaseCalledAllele variant, List<AlleleCount> stagedCounts,  
            int[] expectedCoverageByDirection, bool checkAux = true, int alleleSupport = 5, int expectedSnvRef = 0, int takenRefSupport = 0)
        {
            variant.AlleleSupport = alleleSupport;

            var mockStateManager = CreateMockStateManager(stagedCounts, takenRefSupport);

            var expectedCoverage = expectedCoverageByDirection.Sum();

            CoverageCalculator.Compute(variant, mockStateManager);

            Assert.Equal(expectedCoverage, variant.TotalCoverage);

            for( var i = 0; i < expectedCoverageByDirection.Length; i ++)
                Assert.Equal(expectedCoverageByDirection[i], variant.TotalCoverageByDirection[i]);

            if (checkAux)
            {
                //"Reference" Support should be the coverage less the variant support, if we don't have an SNV
                if (variant is CalledVariant && variant.Type != AlleleCategory.Snv)
                    Assert.Equal(expectedCoverage - variant.AlleleSupport, ((CalledVariant) variant).ReferenceSupport);
                else
                {
                    Assert.Equal(expectedSnvRef, ((CalledVariant)variant).ReferenceSupport);
                }

                //Frequency should be support/coverage
                Assert.Equal((float) variant.AlleleSupport/expectedCoverage, variant.Frequency);
            }
        }

        private IStateManager CreateMockStateManager(List<AlleleCount> states, int refCounts = 0)
        {
            var mockAlleleCountSource = new Mock<IStateManager>();

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
