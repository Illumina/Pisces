using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.Interfaces;
using RealignIndels.Interfaces;
using RealignIndels.Logic;
using RealignIndels.Models;
using Xunit;
using RealignIndels.Logic.TargetCalling;

namespace RealignIndels.Tests.UnitTests
{    public class ChrRealignerTests
    {
        private BamAlignment CreateAlignment(string name, int position, string cigar, string bases, int nm = -1, uint mapq = 1)
        {
            var alignment = new BamAlignment();
            alignment.Name = name;
            alignment.Position = position;
            alignment.RefID = 1;

            alignment.MapQuality = mapq;
            alignment.CigarData = new CigarAlignment(cigar);

            alignment.Bases = bases;
            alignment.Qualities = new byte[alignment.Bases.Length];
            alignment.Qualities = Enumerable.Repeat((byte)30, alignment.Bases.Length).ToArray();  // Generates quality scores of 30
            alignment.SetIsProperPair(true);
            var tagUtils = new TagUtils();
            if (nm>=0)
            {                
                tagUtils.AddIntTag("NM", nm);                
            }
            alignment.TagData = tagUtils.ToBytes();

            return alignment;
        }

        [Fact]
        public void AdjustMapQuality()
        {
            // If realignment is improved, and original quality is 0<q<=20, and there are no mismatches, up the quality to 40
            RealignAndCheckQuality(10, false, 40);
            RealignAndCheckQuality(20, false, 40);

            // If realignment is improved, and original quality is 0, and there are no mismatches, and allow rescoring orig 0, up the quality to 40
            RealignAndCheckQuality(0, false, 40);

            // If realignment is improved, and original quality is 0, and there are no mismatches, and NOT allow rescoring orig 0, keep quality at 0
            RealignAndCheckQuality(0, false, 0, false);

            // If realignment is improved, and original quality is 0<q<=20, and there are any mismatches, keep the quality as-is
            RealignAndCheckQuality(10, true, 10);

            // If realignment is improved, and original quality is > 20, keep quality as-is, regardless of mismatches
            RealignAndCheckQuality(21, false, 21);
            RealignAndCheckQuality(21, true, 21);
            RealignAndCheckQuality(50, false, 50);
            RealignAndCheckQuality(50, true, 50);
        }

        [Fact]
        public void UpdateNMTag()
        {
            // should not be realigned and wrong NM will be kept 
            var hasIndelsWrongNM = new Read("chr", CreateAlignment("hasIndelsWrongNM", 0, "2M1I3M", "AATAAA", 2));
            var expectZeroMismatch = new Read("chr", CreateAlignment("expectZeroMismatch", 0, "6M", "AATAAA", 1));
            var expectOneMismatch = new Read("chr", CreateAlignment("expectOneMismatch", 0, "6M", "AATAAZ", 2));
            // realignment will fix wrong NM
            var ExpectOneMismatchWrongNM = new Read("chr", CreateAlignment("ExpectOneMismatchWrongNM", 0, "6M", "AATAAZ", 0));
            // No NM tag to start with and new NM tag will be added
            var noNMTag = new Read("chr", CreateAlignment("noNMTag", 0, "6M", "AATAAZ"));

            var extractorForRealign = new MockExtractor(new List<Read>
            {
                hasIndelsWrongNM,
                expectZeroMismatch,
                expectOneMismatch,
                ExpectOneMismatchWrongNM,
                noNMTag
            });
            var extractorForCandidates = new MockExtractor(new List<Read>
            {
                hasIndelsWrongNM,
                expectZeroMismatch,
                expectOneMismatch,
                ExpectOneMismatchWrongNM,
                noNMTag
            });

            var writer = new MockRealignmentWriter(new List<string>
            {
                // reads that are expected to be re-aligned 
                expectZeroMismatch.Name,
                expectOneMismatch.Name,
                ExpectOneMismatchWrongNM.Name,
                noNMTag.Name
            }, new List<string>
            {
                // reads that are expected to be written but not re-aligned 
                hasIndelsWrongNM.Name
            }
            );


            SetupExecute(extractorForRealign, extractorForCandidates, writer, true, 50,
                allowRescoringOrig0: true,
                chrReference: "AAAAAAAAAAAAAAAAAAAAAAAAAA", verifyRemappedReads: (reads) =>
                {
                    foreach (var read in reads)
                    {
                        if (read.Name == hasIndelsWrongNM.Name)
                        {
                            Assert.Equal(2, read.GetIntTag("NM"));
                        }
                        if (read.Name == expectZeroMismatch.Name)
                        {
                            Assert.Equal(0, read.GetIntTag("NM"));
                        }
                        if (read.Name == expectOneMismatch.Name)
                        {
                            Assert.Equal(1, read.GetIntTag("NM"));
                        }                        
                        if (read.Name == ExpectOneMismatchWrongNM.Name)
                        {
                            Assert.Equal(1, read.GetIntTag("NM"));
                        }
                        if (read.Name == noNMTag.Name)
                        {
                            Assert.Equal(1, read.GetIntTag("NM"));
                        }
                    }
                });

        }

        [Fact]
        public void CandidateExtractFilter()
        {
            // do not take candidates from reads with MapQ of zero, or secondary alignments
            var highMapQ = new Read("chr", CreateAlignment("highMapQ", 0, "2M1I3M", "AAGAAA"));
            var lowMapQ = new Read("chr", CreateAlignment("lowMapQ", 0, "2M1I3M", "AATAAA", mapq: 0));
            var secondaryRead = new Read("chr", CreateAlignment("secondaryRead", 0, "2M1I3M", "AACAAA"));
            secondaryRead.BamAlignment.SetIsSecondaryAlignment(true);

            var testIndel1 = new Read("chr", CreateAlignment("testIndel1", 0, "6M", "AAGAAA"));
            var testIndel2 = new Read("chr", CreateAlignment("testIndel2", 0, "6M", "AATAAA"));
            var testIndel3 = new Read("chr", CreateAlignment("testIndel2", 0, "6M", "AACAAA"));

            var extractorForRealign = new MockExtractor(new List<Read>
            {
                highMapQ,
                lowMapQ,
                secondaryRead,
                testIndel1,
                testIndel2,
                testIndel3
            });
            var extractorForCandidates = new MockExtractor(new List<Read>
            {
                highMapQ,
                lowMapQ,
                secondaryRead,
                testIndel1,
                testIndel2,
                testIndel3
            });

            var writer = new MockRealignmentWriter(new List<string>
            {
                // reads that are expected to be re-aligned 
                testIndel1.Name
            }, new List<string>
            {
                // reads that are expected to be written but not re-aligned 
                highMapQ.Name,
                lowMapQ.Name,
                secondaryRead.Name,
                testIndel2.Name,
                testIndel3.Name
            }
            );


            SetupExecute(extractorForRealign, extractorForCandidates, writer, true, 50,
                allowRescoringOrig0: true, chrReference: "AAAAAAAAAAAAAAAAAAAAAAAAAA");

        }

        [Fact]
        public void KeepOriginalIfAlignmentUnchanged()
        {
            // If realignment results in same alignment, don't count it as realigned
            // If realignment results in different cigar and same position, count it as realigned
            // If realignment results in same cigar and different position, count it as realigned -- having trouble recreating this situation...
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 2, "2M1I3M", "ZATAAZ"));
            var hasIndels2 = new Read("chr", CreateAlignment("hasIndels2", 2, "2M1I3M", "ZATAAZ"));
            var realignSamePosNewCigar = new Read("chr", CreateAlignment("candidateForRealignmentGoesToSamePos", 2, "6M", "ZATAAZ"));

            var extractorForRealign = new MockExtractor(new List<Read>
            {
                hasIndels,
                hasIndels2,
                realignSamePosNewCigar,
            });
            var extractorForCandidates = new MockExtractor(new List<Read>
            {
                hasIndels,
                hasIndels2,
                realignSamePosNewCigar,
            });

            var writer = new MockRealignmentWriter(new List<string>
            {
                realignSamePosNewCigar.Name,
                // the reads that are expected to be re-aligned
            }, new List<string>
            {
                hasIndels.Name,
                hasIndels2.Name
                // reads that are expected to be written but not re-aligned 
            });

            SetupExecute(extractorForRealign, extractorForCandidates, writer, true, 50, "YZAAAZZZZZZZ");


        }

        private void RealignAndCheckQuality(uint initialQuality, bool hasMismatches, uint expectedQuality, bool allowRescoringOrig0 = true)
        {
            
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var candidateForRealignment = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", hasMismatches ? "AATAAZ" : "AATAAA"));
            candidateForRealignment.BamAlignment.MapQuality = initialQuality;

            var extractorForRealign = new MockExtractor(new List<Read>
            {
                hasIndels,
                candidateForRealignment,
            });
            var extractorForCandidates = new MockExtractor(new List<Read>
            {
                hasIndels,
                candidateForRealignment,
            });

            var writer = new MockRealignmentWriter(new List<string>
            {
                candidateForRealignment.Name,
                // the reads that are expected to be re-aligned
            }, new List<string>
            {
                hasIndels.Name
                // reads that are expected to be written but not re-aligned 
            });


            SetupExecute(extractorForRealign, extractorForCandidates, writer, true, 50, 
                allowRescoringOrig0: allowRescoringOrig0,
                chrReference: "AAAAAAAAAAAAAAAAAAAAAAAAAA", verifyRemappedReads: (reads) =>
            {
                foreach (var read in reads)
                {
                    if (read.Name == candidateForRealignment.Name)
                    {
                        Assert.Equal(expectedQuality, read.MapQuality);
                    } 
                }
            });



        }

        // Testing SkipDuplicates = true where the variant frequency is below threshold.
        [Fact]
        public void SkipDuplicatesTrue_VariantBelowThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates not considered in the evidence counting
            // the frequency in this case should be 1 (uniq read with indel) / 3 (total uniq reads) = 33.3333333%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);
            // don't allow dups


            var extractorForRealign2 = new MockExtractor(new List<Read>
            {
                uniqRef,
                hasIndels,
                uniqCandidate,
                dupWithIndel,
                dupRef
            });
            var extractorForCandidates2 = new MockExtractor(new List<Read>
            {
                uniqRef,
                hasIndels,
                uniqCandidate,
                dupWithIndel,
                dupRef
            });

            var writerBelowThreshold = new MockRealignmentWriter(new List<string>
            {
                // the reads that are expected to be re-aligned
            }, new List<string>
            {
                // reads that are expected to be written but not re-aligned 
                  uniqCandidate.Name, hasIndels.Name, uniqRef.Name, dupWithIndel.Name, dupRef.Name
            });
            // when the threshold is set to 0.34 (34%) this case falls below with 33.33333%
            // No realignment should be triggered
            SetupExecute(extractorForRealign2, extractorForCandidates2, writerBelowThreshold, true, 50, "AAAAAAAAAAA", 0.34f);

        }

        // Testing SkipAndRemoveDuplicates = true where the variant frequency is below threshold.
        [Fact]
        public void SkipRemoveDuplicatesTrue_VariantBelowThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates not considered in the evidence counting
            // the frequency in this case should be 1 (uniq read with indel) / 3 (total uniq reads) = 33.3333333%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);
            // don't allow dups


            var extractorForRealign2 = new MockExtractor(new List<Read>
            {
                uniqRef,
                hasIndels,
                uniqCandidate,
                dupWithIndel,
                dupRef
            });
            var extractorForCandidates2 = new MockExtractor(new List<Read>
            {
                uniqRef,
                hasIndels,
                uniqCandidate,
                dupWithIndel,
                dupRef
            });

            var writerBelowThreshold = new MockRealignmentWriter(new List<string>
            {
                // the reads that are expected to be re-aligned
            }, new List<string>
            {
                // reads that are expected to be written but not re-aligned 
                  uniqCandidate.Name, hasIndels.Name, uniqRef.Name,
            });
            // when the threshold is set to 0.34 (34%) this case falls below with 33.33333%
            // No realignment should be triggered
            SetupExecute(extractorForRealign2, extractorForCandidates2, writerBelowThreshold, false, 50, "AAAAAAAAAAA", 0.34f, true);

        }

        // Testing SkipDuplicates = true where the variant frequency is above threshold.
        [Fact]
        public void SkipDuplicatesTrue_VariantAboveThreshold(){
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates not considered in the evidence counting
            // the frequency in this case should be 1 (uniq read with indel) / 3 (total uniq reads) = 33.3333333%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
                {
                    uniqCandidate.Name
                }, new List<string>
                {
                      uniqRef.Name, dupWithIndel.Name, dupRef.Name, hasIndels.Name
                });
            // The variant frequency (33%) in this case is above the threshold 0.32 (32%) 
            // Realignment of candidate reads should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, true, 50, "AAAAAAAAAAA", 0.32f);
        }
        // Testing SkipAndRemoveDuplicates = true where the variant frequency is above threshold.
        [Fact]
        public void SkipRemoveDuplicatesTrue_VariantAboveThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates not considered in the evidence counting
            // the frequency in this case should be 1 (uniq read with indel) / 3 (total uniq reads) = 33.3333333%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
                {
                     uniqCandidate.Name
                }, new List<string>
                {
                      uniqRef.Name, hasIndels.Name
                });
            // The variant frequency (33%) in this case is above the threshold 0.32 (32%) 
            // Realignment of candidate reads should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, false, 50, "AAAAAAAAAAA", 0.32f, true);
        }
        // Testing SkipDuplicates = false where the variant frequency is below threshold.
        [Fact]
        public void SkipDuplicatesFalse_VariantBelowThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates considered in the evidence counting
            // the frequency in this case should be 2 (uniq & dup reads with indel) / 5 (total reads) = 40%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
            {

            }, new List<string>
                {
                    uniqCandidate.Name, uniqRef.Name, dupRef.Name,hasIndels.Name, dupWithIndel.Name
                });
            // The variant frequency (40%) in this case is below the threshold 0.41 (41%) 
            //No realignment should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, false, 50, "AAAAAAAAAAA", 0.41f);
        }

        // Testing SkipAndRemoveDuplicates = false where the variant frequency is below threshold.
        [Fact]
        public void SkipRemoveDuplicatesFalse_VariantBelowThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates considered in the evidence counting
            // the frequency in this case should be 2 (uniq & dup reads with indel) / 5 (total reads) = 40%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
            {

            }, new List<string>
                {
                    uniqCandidate.Name, uniqRef.Name, dupRef.Name,hasIndels.Name, dupWithIndel.Name
                });
            // The variant frequency (40%) in this case is below the threshold 0.41 (41%) 
            // No realignment should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, false, 50, "AAAAAAAAAAA", 0.41f, false);
        }
        // Testing SkipDuplicates = false where the variant frequency is above threshold.
        [Fact]
        public void SkipDuplicatesFalse_VariantAboveThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates considered in the evidence counting
            // the frequency in this case should be 2 (uniq & dup reads with indel) / 5 (total reads) = 40%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
                {
                    uniqCandidate.Name
                }, new List<string>
                {
                    hasIndels.Name, dupWithIndel.Name,
                   uniqRef.Name, dupRef.Name,
                });
            // The variant frequency (40%) in this case is above the threshold 0.39 (39%) 
            // Realignment of candidate reads should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, false, 50, "AAAAAAAAAAA", 0.39f);
        }
        // Testing SkipAndRemoveDuplicates = false where the variant frequency is above threshold.
        [Fact]
        public void SkipRemoveDuplicatesFalse_VariantAboveThreshold()
        {
            // 1 unique read and 1 duplicate read have indels 
            // With duplicates considered in the evidence counting
            // the frequency in this case should be 2 (uniq & dup reads with indel) / 5 (total reads) = 40%
            var hasIndels = new Read("chr", CreateAlignment("hasIndels", 0, "2M1I3M", "AATAAA"));
            var uniqRef = new Read("chr", CreateAlignment("uniqRef", 0, "6M", "AAAAAA"));
            var uniqCandidate = new Read("chr", CreateAlignment("uniqCandidate", 0, "6M", "AATAAA"));
            var dupWithIndel = new Read("chr", CreateAlignment("dupWithIndels", 0, "2M1I3M", "AATAAA"));
            var dupRef = new Read("chr", CreateAlignment("dubRef", 0, "6M", "AAAAAA"));
            dupWithIndel.BamAlignment.SetIsDuplicate(true);
            dupRef.BamAlignment.SetIsDuplicate(true);

            var extractorForRealign = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });
            var extractorForCandidates = new MockExtractor(new List<Read>
                {
                    uniqRef,
                    hasIndels,
                    uniqCandidate,
                    dupWithIndel,
                    dupRef
                });

            var writer = new MockRealignmentWriter(new List<string>
                {
                   uniqCandidate.Name
                }, new List<string>
                {
                    hasIndels.Name, dupWithIndel.Name, uniqRef.Name, dupRef.Name,
                });
            // The variant frequency (40%) in this case is above the threshold 0.39 (39%) 
            // Realignment of candidate reads should be triggered
            SetupExecute(extractorForRealign, extractorForCandidates, writer, false, 50, "AAAAAAAAAAA", 0.39f, false);
        }

        private Read CopyRead(Read read)
        {
            var copiedRead = new Read(read.Chromosome, CreateAlignment(read.BamAlignment.Name, read.BamAlignment.Position, read.BamAlignment.CigarData.ToString(), read.BamAlignment.Bases));
            copiedRead.BamAlignment.SetIsDuplicate(read.IsPcrDuplicate);
            copiedRead.BamAlignment.SetIsSecondaryAlignment(!read.IsPrimaryAlignment);
            copiedRead.BamAlignment.SetIsSupplementaryAlignment(read.IsSupplementaryAlignment);
            if (read.HasSupplementaryAlignment)
            {
                var tagUtils = new TagUtils();
                tagUtils.AddStringTag("SA", "dummy");
                copiedRead.BamAlignment.AppendTagData(tagUtils.ToBytes());
            }
                
            return copiedRead;
        }

        [Fact]
        public void Execute()
        {
            var dupRead = new Read("chr", CreateAlignment("Duplicate", 5, "5S5M5I5M", "ACGTACGTACTATATAATAC"));
            dupRead.BamAlignment.SetIsDuplicate(true);
            var nonPrimaryRead = new Read("chr", CreateAlignment("NonPrimary", 5, "5S5M5I5M", "ACGTACGTACTATATAATAC"));
            nonPrimaryRead.BamAlignment.SetIsSecondaryAlignment(true);
            var SupplementaryRead = new Read("chr", CreateAlignment("Supplementary", 5, "5S5M5I5M", "ACGTACGTACTATATAATAC"));
            SupplementaryRead.BamAlignment.SetIsSupplementaryAlignment(true);
            var HasSupplementaryRead = new Read("chr", CreateAlignment("HasSupplementary", 5, "5S5M5I5M", "ACGTACGTACTATATAATAC"));
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("SA", "dummy");
            HasSupplementaryRead.BamAlignment.AppendTagData(tagUtils.ToBytes());
            var passesSuspicion = new Read("chr", CreateAlignment("PassesSuspicion", 0, "4M", "ACGT"));
            var hasIndels = new Read("chr", CreateAlignment("HasIndels", 5, "5S5M5I5M", "ACGTACGTACTATATAATAC"));

            // HasIndels shifts too far. Do not write it. Don't realign dups.
            var extractorForRealign = new MockExtractor(new List<Read>
            {
                CopyRead(dupRead),
                CopyRead(nonPrimaryRead),
                CopyRead(SupplementaryRead),
                CopyRead(HasSupplementaryRead),
                CopyRead(passesSuspicion),
                CopyRead(hasIndels)
            });
            var writer = new MockRealignmentWriter(new List<string>
            {
                
            }, new List<string>
            {
                hasIndels.Name, nonPrimaryRead.Name,SupplementaryRead.Name, HasSupplementaryRead.Name, dupRead.Name,passesSuspicion.Name
            });

            SetupMocksandExecute(extractorForRealign, writer, true, 2);

            // Allow realignment of dups, and increase max shift to let hasIndels through
            extractorForRealign = new MockExtractor(new List<Read>
            {
                CopyRead(dupRead),
                CopyRead(nonPrimaryRead),
                CopyRead(SupplementaryRead),
                CopyRead(HasSupplementaryRead),
                CopyRead(passesSuspicion),
                CopyRead(hasIndels)
            });
            writer = new MockRealignmentWriter(new List<string>
            {
                dupRead.Name,
                hasIndels.Name
            }, new List<string>
            {
                nonPrimaryRead.Name, SupplementaryRead.Name, HasSupplementaryRead.Name, passesSuspicion.Name
            });

            SetupMocksandExecute(extractorForRealign, writer, false, 50);

            //Don't allow dups, but make max shift big enough for hasIndels
            extractorForRealign = new MockExtractor(new List<Read>
            {
                CopyRead(dupRead),
                CopyRead(nonPrimaryRead),
                CopyRead(SupplementaryRead),
                CopyRead(HasSupplementaryRead),
                CopyRead(passesSuspicion),
                CopyRead(hasIndels)
            });
            writer = new MockRealignmentWriter(new List<string>
            {
                hasIndels.Name,
            }, new List<string>
            {
                 dupRead.Name, nonPrimaryRead.Name, SupplementaryRead.Name, HasSupplementaryRead.Name, passesSuspicion.Name
            });

            SetupMocksandExecute(extractorForRealign, writer, true, 50);

        }

        private void SetupExecute(IAlignmentExtractor extractorForRealign, IAlignmentExtractor extractorForCandidates, MockRealignmentWriter writer, 
            bool skipDups, int maxShift, string chrReference = null, float frequencyCutoff = 0, bool skipAndRemove = false, Action<IEnumerable<BamAlignment>> verifyRemappedReads = null, bool allowRescoringOrig0 = true, int realignWindowSize = 1000)
        {
            var ranker = new Mock<IIndelRanker>();

            var realigner = new ChrRealigner(new ChrReference() { Name = "chr", Sequence = chrReference ?? string.Join(string.Empty, Enumerable.Repeat("ACGT", 10)) },
                extractorForCandidates,
                extractorForRealign, new IndelTargetFinder(0), ranker.Object, 
                new  IndelTargetCaller(frequencyCutoff),
                new RealignStateManager(realignWindowSize: realignWindowSize), writer, skipDuplicates: skipDups, skipAndRemoveDuplicates: skipAndRemove, maxRealignShift: maxShift, allowRescoringOrig0: allowRescoringOrig0);

            realigner.Execute();
            Assert.Equal(writer.ReadsExpected, writer.ReadsWritten);

            verifyRemappedReads?.Invoke(writer.RemappedReads);
        }


        private void SetupMocksandExecute(IAlignmentExtractor extractorForRealign, IRealignmentWriter writer, bool skipDups, int maxShift, string chrReference = null, ITargetCaller targetCaller = null, IStateManager stateManager = null)

        {
                
                var extractorForCandidates = new Mock<IAlignmentExtractor>();
                var ranker = new Mock<IIndelRanker>();
                var indel = new CandidateIndel(new CandidateAllele("chr", 10, "C", "CTATATA", AlleleCategory.Insertion));
                var indelFinder = new Mock<IIndelCandidateFinder>();

                if (targetCaller == null)
            {
                var mocktargetCaller = new Mock<ITargetCaller>();
                mocktargetCaller.Setup(x => x.Call(It.IsAny<List<CandidateIndel>>(), It.IsAny<IAlleleSource>()))
                    .Returns(new List<CandidateIndel>()
                    {
                        indel
                    });

                targetCaller = mocktargetCaller.Object;

            }

            var realigner = new ChrRealigner(new ChrReference() { Sequence = chrReference ?? string.Join(string.Empty, Enumerable.Repeat("ACGT", 10))}, extractorForCandidates.Object,
                extractorForRealign, indelFinder.Object, ranker.Object, targetCaller,
                new RealignStateManager(), writer, skipDuplicates: skipDups, maxRealignShift: maxShift);

                        realigner.Execute();

            }

        }

    public class MockRealignmentWriter : IRealignmentWriter
    {
        private readonly List<string> _shouldBeRemapped;
        private readonly List<string> _shouldNotBeRemapped;
        public int ReadsWritten;
        public int ReadsExpected { get { return _shouldBeRemapped.Count + _shouldNotBeRemapped.Count; } }
        public List<BamAlignment> RemappedReads = new List<BamAlignment>();
        public MockRealignmentWriter(List<string> shouldBeRemapped, List<string> shouldNotBeRemapped)
        {
            _shouldBeRemapped = shouldBeRemapped;
            _shouldNotBeRemapped = shouldNotBeRemapped;
        }

        public void WriteRead(ref BamAlignment read, bool remapped)
        {
            Assert.Equal(_shouldBeRemapped.Contains(read.Name), remapped);
            if (_shouldNotBeRemapped.Contains(read.Name))
            {
                Assert.False(remapped);
            }
            ReadsWritten++;

            if (remapped)
            {
                RemappedReads.Add(read);
            }

            Console.WriteLine("Writing read: " + read.Name + read.Position + " remapped ? " + remapped);
        }

        #region Not Implemented in Mock
        public void FinishAll()
        {
            throw new NotImplementedException();
        }

        public void FlushAllBufferedRecords()
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            throw new NotImplementedException();
        }
        #endregion

    }


    public class MockExtractor : IAlignmentExtractor
    {
        private int _counter = 0;
        private List<Read> _reads;

        public MockExtractor(List<Read> reads)
        {
            _reads = reads;
        }

        public bool GetNextAlignment(Read read)
        {
            if (_counter >= _reads.Count)
            {
                return false;
            }

            read.Reset("chr", _reads[_counter].BamAlignment);
            _counter++;
            return true;
        }

        #region Not implemented in Mock
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool Jump(string chromosomeName, int position = 0)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool SourceIsStitched { get; }
        public bool SourceIsCollapsed { get; }

        public List<string> SourceReferenceList { get; }
        #endregion

    }

}