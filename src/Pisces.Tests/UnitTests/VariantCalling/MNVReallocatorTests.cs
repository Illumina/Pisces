using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Logic.VariantCalling;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Tests.UnitTests.Pisces
{
    public class MNVReallocatorTests
    {
        [Fact]
        [Trait("ReqID", "SDS-53")]
        [Trait("ReqID", "SDS-54")]
        [Trait("ReqID", "SDS-55")]
        [Trait("ReqID", "SDS-57")]
        public void ReallocateFailedMnvs()
        {
            var failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 101,
                Reference = "TTTTTTT",
                Alternate = "ATCAGGC",
                SupportByDirection = new []{10,20,30}
            }};

            //Happy path - break into three existing alleles, and up their support
            var calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC",
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },

            };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count);
            Assert.True(calledAlleles.All(a => a.AlleleSupport == 6));

            //Second half of big MNV could go to two called alleles or one bigger MNV - should take the big one.
            var triNucVariant = new CalledAllele()
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TTT",
                Alternate = "AGG",
                SupportByDirection = new []{5,6,1}
            };

            calledAlleles = new List<CalledAllele>{
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },
            triNucVariant
           };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(2, calledAlleles.Count(a => a.AlleleSupport == 6));
            Assert.True(calledAlleles.Where(a => a.Alternate.Length == 2).All(a => a.AlleleSupport == 5));
            Assert.Equal(6, triNucVariant.AlleleSupport);
            //Support by direction should be incremented by the amount of the failed variant
            Assert.Equal(new[] { 15, 26, 31 }, triNucVariant.SupportByDirection);

            //Big MNV has two valid sub-MNVs of equal length. Should take the one with higher support.
            var lowSupportTNV = new CalledAllele()
            {
                AlleleSupport = 3,
                Coordinate = 103,
                Reference = "TTT",
                Alternate = "CAG"
            };

            calledAlleles = new List<CalledAllele>{
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },
            lowSupportTNV
           };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count(a => a.AlleleSupport == 6));
            Assert.Equal(3, lowSupportTNV.AlleleSupport);
            Assert.Equal(new[] { 0, 0, 0 }, lowSupportTNV.SupportByDirection);


            //MNV at end has some overlap but extends beyond big MNV. Should not get any support from big MNV
            var mnvExtendsPastBigMnv = new CalledAllele()
            {
                AlleleSupport = 3,
                Coordinate = 106,
                Reference = "TTT",
                Alternate = "GCC"
            };

            calledAlleles = new List<CalledAllele>{
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },
            mnvExtendsPastBigMnv
           };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count(a => a.AlleleSupport == 6));
            Assert.Equal(3, mnvExtendsPastBigMnv.AlleleSupport);

            //MNV at beginning has some overlap but starts before big MNV. Should not get any support from big MNV
            var mnvStartsBeforeBigMnv = new CalledAllele()
            {
                AlleleSupport = 3,
                Coordinate = 100,
                Reference = "TTT",
                Alternate = "GAT"
            };

            calledAlleles = new List<CalledAllele>{
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },
            mnvStartsBeforeBigMnv
           };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count(a => a.AlleleSupport == 6));
            Assert.Equal(3, mnvStartsBeforeBigMnv.AlleleSupport);

            //Should not reallocate anything to indels
            var deletion = new CalledAllele()
            {
                Coordinate = 101,
                AlleleSupport = 5,
                Alternate = "ATC",
                Type = AlleleCategory.Deletion
            };
            calledAlleles = new List<CalledAllele>()
            {
              deletion
            };
            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(5, deletion.AlleleSupport);

            //Should work with overlaps that are not at the first base
            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 100,
                Reference = "TTTTTTTT",
                Alternate = "GATCAGGC"
            }};

            calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 104,
                Reference = "TT",
                Alternate = "AG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 106,
                Reference = "TT",
                Alternate = "GC"
            },

            };
            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);

            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count(a => a.Alternate.Length > 1)); //All three original MNVs should still be there
            Assert.Equal(1, calledAlleles.Count(a => a.Alternate.Length == 1)); //Should have an additional SNV for the first base
            Assert.Equal(1, calledAlleles.First(a => a.Alternate.Length == 1).AlleleSupport);
            Assert.Equal(AlleleCategory.Snv, calledAlleles.First(a => a.Alternate.Length == 1).Type);
            Assert.True(calledAlleles.Where(a => a.Alternate.Length > 1).All(a => a.AlleleSupport == 6));

            //If MNV can't be fully attributed to MNVs, break out into SNVs
            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 101,
                Reference = "TTTTTTTT",
                Alternate = "ATCAGGCA"
            }};

            calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 105,
                Reference = "TT",
                Alternate = "GG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 107,
                Reference = "TT",
                Alternate = "CA"
            },

            };
            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);
            PrintResults(calledAlleles);

            var expectedSnv = new CalledAllele
            {
                AlleleSupport = 1,
                Reference = "T",
                Alternate = "A",
                Coordinate = 104,
                Type = AlleleCategory.Snv
            };

            Assert.Equal(3, calledAlleles.Count(a => a.Alternate.Length > 1)); //All three original MNVs should still be there
            Assert.Equal(1, calledAlleles.Count(a => a.Alternate.Length == 1)); //Should have an additional SNV for the first base
            Assert.True(VerifyCalledAlleleMatch(expectedSnv, calledAlleles.First(a => a.Alternate.Length == 1)));
            Assert.True(calledAlleles.Where(a => a.Alternate.Length > 1).All(a => a.AlleleSupport == 6));

            //If MNV includes some reference bases, make sure they are accounted for as refs
            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 101,
                Reference = "TTTTTTTT",
                Alternate = "ATCTGGCA"
            }};

            calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 105,
                Reference = "TT",
                Alternate = "GG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 107,
                Reference = "TT",
                Alternate = "CA"
            },

            };
            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);
            PrintResults(calledAlleles);

            var expectedRef = new CalledAllele()
            {
                AlleleSupport = 1,
                Reference = "T",
                Alternate = "T",
                Coordinate = 104
            };

            Assert.Equal(3, calledAlleles.Count(a => a.Alternate.Length > 1)); //All three original MNVs should still be there
            Assert.Equal(0, calledAlleles.Count(a => a.Alternate.Length == 1)); //Should NOT have an additional ref
            Assert.True(calledAlleles.Where(a => a.Alternate.Length > 1).All(a => a.AlleleSupport == 6));

            //If MNV can't be fully attributed to MNVs, break out into SNVs
            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 101,
                Reference = "TTTTTTTT",
                Alternate = "ATCAGGCA"
            }};
            expectedSnv = new CalledAllele
            {
                AlleleSupport = 1,
                Reference = "T",
                Alternate = "A",
                Coordinate = 104
            };


            calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 105,
                Reference = "TT",
                Alternate = "GG"
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 107,
                Reference = "TT",
                Alternate = "CA"
            },
            expectedSnv

            };
            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);
            PrintResults(calledAlleles);


            Assert.Equal(3, calledAlleles.Count(a => a.Alternate.Length > 1)); //All three original MNVs should still be there
            Assert.Equal(1, calledAlleles.Count(a => a.Alternate.Length == 1)); //Should have an additional SNV for the first base
            Assert.Equal(2, calledAlleles.First(a => a.Alternate.Length == 1).AlleleSupport);
            Assert.True(calledAlleles.Where(a => a.Alternate.Length > 1).All(a => a.AlleleSupport == 6));

            //If MNV includes some reference bases - allocate to existing references if exist
            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 101,
                Reference = "TTTTTTTT",
                Alternate = "ATCTGGCA",
                Type = AlleleCategory.Mnv
            }};
            expectedRef = new CalledAllele()
            {
                AlleleSupport = 1,
                Reference = "T",
                Alternate = "T",
                Coordinate = 104,
                Type = AlleleCategory.Reference
            };

            //Don't output references ever
            calledAlleles = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 101,
                Reference = "TTT",
                Alternate = "ATC",
                Type = AlleleCategory.Mnv
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 105,
                Reference = "TT",
                Alternate = "GG",
                Type = AlleleCategory.Mnv
            },
            new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 107,
                Reference = "TT",
                Alternate = "CA",
                Type = AlleleCategory.Mnv
        },
            };

            MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles);
            PrintResults(calledAlleles);
            Assert.Equal(3, calledAlleles.Count(a => a.Alternate.Length > 1)); //All three original MNVs should still be there
            Assert.False(calledAlleles.Any(a => a .Type == AlleleCategory.Reference)); 
        }

        [Fact]
        public void ReallocateMnvs_BlockStraddling()
        {
            var failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 99,
                Reference = "TTTT",
                Alternate = "AGCG",
                SupportByDirection = new []{10,20,30}
            }};

            // If there are any overlapping MNVs in the current block, reallocate to them.
            
            var calledAlleles = new List<CalledAllele>{
                new CalledAllele
                {
                    AlleleSupport = 5,
                    Coordinate = 99,
                    Reference = "TTT",
                    Alternate = "AGC",
                },
            };

            var leftoversInNextBlock = MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles, 100);

            PrintResults(calledAlleles);
            Assert.Equal(1, calledAlleles.Count);
            Assert.True(calledAlleles.All(a => a.AlleleSupport == 6));
            PrintResults(leftoversInNextBlock.ToList());
            Assert.Equal(1, leftoversInNextBlock.Count());
            Assert.True(VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Snv, Coordinate = 102, Reference = "T", Alternate = "G", AlleleSupport = 1 },
                leftoversInNextBlock.First()));

            // If there are no overlapping MNVs in the current block to reallocate to, take what is in the current block 
            // as new SNVs and pass the remaining chunk as an MNV for the next block to deal with.

            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 99,
                Reference = "TTTT",
                Alternate = "AGCG",
                SupportByDirection = new []{10,20,30}
            }};

            calledAlleles = new List<CalledAllele>();

            leftoversInNextBlock = MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles, 100);

            PrintResults(calledAlleles);
            Assert.Equal(2, calledAlleles.Count);
            Assert.Equal(calledAlleles.Count(x => VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Snv, Coordinate = 100, Reference = "T", Alternate = "G", AlleleSupport = 1 }, x)), 1);

            PrintResults(leftoversInNextBlock.ToList());
            Assert.Equal(1, leftoversInNextBlock.Count());
            Assert.True(VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Mnv, Coordinate = 101, Reference = "TT", Alternate = "CG", AlleleSupport = 1 },
                leftoversInNextBlock.First()));

            // If there are no overlapping MNVs in the current block to reallocate to, take what is in the current block 
            // and reallocate to any existing SNVs and pass the remaining chunk as an MNV for the next block to deal with.

            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 99,
                Reference = "TTTT",
                Alternate = "AGCG",
                SupportByDirection = new []{10,20,30}
            }};

            var existingSNV = new CalledAllele
            {
                AlleleSupport = 5,
                Coordinate = 99,
                Reference = "T",
                Alternate = "A",
            };

            calledAlleles = new List<CalledAllele>
            {
                existingSNV
            };

            leftoversInNextBlock = MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles, 100);

            PrintResults(calledAlleles);
            Assert.Equal(2, calledAlleles.Count);
            Assert.True(calledAlleles.First(x=>VerifyCalledAlleleMatch(existingSNV, x)).AlleleSupport == 6);
            Assert.Equal(calledAlleles.Count(x=> VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Snv, Coordinate = 100, Reference = "T", Alternate = "G", AlleleSupport = 1}, x)), 1);

            PrintResults(leftoversInNextBlock.ToList());
            Assert.Equal(1, leftoversInNextBlock.Count());
            Assert.True(VerifyCalledAlleleMatch(
                new CalledAllele { Type=AlleleCategory.Mnv, Coordinate = 101, Reference = "TT", Alternate = "CG", AlleleSupport = 1 },
                leftoversInNextBlock.First()));

            // When passing remaining chunk to next block, if it has now been trimmed such that there is a reference edge, 
            // pass it as a reference plus the rest of the MNV

            failedMnvs = new List<CalledAllele>{new CalledAllele
            {
                AlleleSupport = 1,
                Coordinate = 99,
                Reference = "TTTTT",
                Alternate = "AGTCG",
                SupportByDirection = new []{10,20,30}
            }};

            calledAlleles = new List<CalledAllele>();

            leftoversInNextBlock = MnvReallocator.ReallocateFailedMnvs(failedMnvs, calledAlleles, 100);

            PrintResults(calledAlleles);
            Assert.Equal(2, calledAlleles.Count);
            Assert.Equal(calledAlleles.Count(x => VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Snv, Coordinate = 99, Reference = "T", Alternate = "A", AlleleSupport = 1 }, x)), 1);
            Assert.Equal(calledAlleles.Count(x => VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Snv, Coordinate = 100, Reference = "T", Alternate = "G", AlleleSupport = 1 }, x)), 1);

            PrintResults(leftoversInNextBlock.ToList());
            Assert.Equal(1, leftoversInNextBlock.Count());
            Assert.Equal(0, leftoversInNextBlock.Count(x => VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Reference, Coordinate = 101, Reference = "T", Alternate = "T", AlleleSupport = 1 }, x)));
            Assert.Equal(1, leftoversInNextBlock.Count(x=>VerifyCalledAlleleMatch(
                new CalledAllele { Type = AlleleCategory.Mnv, Coordinate = 102, Reference = "TT", Alternate = "CG", AlleleSupport = 1 },
                x)));

        }

        [Fact]
        public void ReallocateMnvs_DirectionalSupportReallocation()
        {
            var failed = new List<CalledAllele>()
            {
                new CalledAllele()
                {
                    AlleleSupport = 1,
                    SupportByDirection = new int[] {1, 0, 0},
                    Chromosome = "chr15",
                    Coordinate = 23685301,
                    Reference = "TCT",                    
                    Alternate = "CTC",
                },
                new CalledAllele()
                {
                    AlleleSupport = 1,
                    SupportByDirection = new int[] {0, 1, 0},
                    Chromosome = "chr15",
                    Coordinate = 23685303,
                    Reference = "TCT",                   
                    Alternate = "CGC",
                },
                new CalledAllele()
                {
                    AlleleSupport = 1,
                    SupportByDirection = new int[] {1, 0, 0},
                    Chromosome = "chr15",
                    Coordinate = 23685304,
                    Reference = "CTT",
                    Alternate = "GCA",
                }
            };
            var callable = new List<CalledAllele>();

            MnvReallocator.ReallocateFailedMnvs(failed, callable);

            //chr15  23685304 C G	SB: (-12.5015 vs -14.0728)	-- Original issue manifested as strand bias non-concordance in SNVs that had been created out of MNV reallocation
            // This variant was getting forward support of 2 and reverse of 1, when it should have been getting forward support of 1 and reverse of 1 for a total of 2 
            // (1 reverse support from 23685303 TC*T>CG*C breaking down and 1 forward from 23685304 C*TT>G*CA breaking down).  

            Assert.True(
                callable.Count(
                    x =>
                        x.Coordinate == 23685304 && x.Reference == "C" && x.Alternate ==
                            "G" && x.SupportByDirection[0]==1 && x.SupportByDirection[1]==1 && x.SupportByDirection[2]==0) == 1);

        }

        [Fact]
        public void BreakOffEdgeReferences()
        {
            // -----------------------------------------------
            // non-mnv should be returned as-is
            // -----------------------------------------------

            var nonMnv = new CalledAllele(AlleleCategory.Deletion)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCCTT",
                Alternate = "T",
            };
            var brokenOutAlleles = MnvReallocator.BreakOffEdgeReferences(nonMnv);
            Assert.Equal(1, brokenOutAlleles.Count());
            Assert.Equal(1, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(nonMnv, x)));

            // -----------------------------------------------
            // mnv without leading or trailing refs should be returned as-is
            // -----------------------------------------------

            var alleleWithoutLeadingRefs = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCCTT",
                Alternate = "AAAAAA",
            };
            brokenOutAlleles = MnvReallocator.BreakOffEdgeReferences(alleleWithoutLeadingRefs);
            Assert.Equal(1, brokenOutAlleles.Count());
            Assert.Equal(1, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(alleleWithoutLeadingRefs, x)));

            // -----------------------------------------------
            // allele with two leading references should have them broken off, leaving just the rest of the mnv
            // -----------------------------------------------

            var alleleWithLeadingRefs = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCCTT",
                Alternate = "TTAAAA",
            };

            var expectedLeadingRef1 = new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "T",
                Alternate = "T",
            };
            var expectedLeadingRef2 = new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 1001,
                AlleleSupport = 10,
                Reference = "T",
                Alternate = "T",
            };
            var expectedRemainingMnv = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1002,
                AlleleSupport = 10,
                Reference = "CCTT",
                Alternate = "AAAA",
            };

            brokenOutAlleles = MnvReallocator.BreakOffEdgeReferences(alleleWithLeadingRefs);
            Assert.Equal(1, brokenOutAlleles.Count());
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedLeadingRef1, x)));
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedLeadingRef2, x)));
            Assert.Equal(1, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedRemainingMnv, x)));

            // -----------------------------------------------
            // allele with two trailing references should have them broken off, leaving just the rest of the mnv
            // -----------------------------------------------

            var alleleWithTrailingRefs = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCCTT",
                Alternate = "AAAATT",
            };
            var expectedTrailingRef1 = new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 1004,
                AlleleSupport = 10,
                Reference = "T",
                Alternate = "T",
            };
            var expectedTrailingRef2 = new CalledAllele()
            {
                Chromosome = "chr1",
                Coordinate = 1005,
                AlleleSupport = 10,
                Reference = "T",
                Alternate = "T",
            };
            expectedRemainingMnv = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCC",
                Alternate = "AAAA",
            };

            brokenOutAlleles = MnvReallocator.BreakOffEdgeReferences(alleleWithTrailingRefs);
            Assert.Equal(1, brokenOutAlleles.Count());
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedTrailingRef1, x)));
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedTrailingRef2, x)));
            Assert.Equal(1, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedRemainingMnv, x)));


            // -----------------------------------------------
            // allele with two leading references and two trailing references should have them broken off, leaving just the rest of the mnv
            // -----------------------------------------------

            var alleleWithLeadingAndTrailingRefs = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1000,
                AlleleSupport = 10,
                Reference = "TTCCTT",
                Alternate = "TTAATT",
            };
            expectedRemainingMnv = new CalledAllele(AlleleCategory.Mnv)
            {
                Chromosome = "chr1",
                Coordinate = 1002,
                AlleleSupport = 10,
                Reference = "CC",
                Alternate = "AA",
            };

            brokenOutAlleles = MnvReallocator.BreakOffEdgeReferences(alleleWithLeadingAndTrailingRefs);
            Assert.Equal(1, brokenOutAlleles.Count());
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedLeadingRef1, x)));
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedLeadingRef2, x)));
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedTrailingRef1, x)));
            Assert.Equal(0, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedTrailingRef2, x)));
            Assert.Equal(1, brokenOutAlleles.Count(x => VerifyCalledAlleleMatch(expectedRemainingMnv, x)));
        }

        private static bool VerifyCalledAlleleMatch(CalledAllele expected, CalledAllele actual)
        {
            return expected.Coordinate == actual.Coordinate && 
                expected.Type == actual.Type &&
                expected.Reference == actual.Reference &&
                expected.Alternate == actual.Alternate &&
                expected.AlleleSupport == actual.AlleleSupport;
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
    }
}
