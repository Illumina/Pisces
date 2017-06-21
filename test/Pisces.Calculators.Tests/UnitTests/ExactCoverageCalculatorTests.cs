using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Alignment.Domain.Sequencing;
using Xunit;

namespace Pisces.Calculators.Tests.UnitTests
{
    public class ExactCoverageCalculatorTests
    {
        [Fact]
        public void Insertion()
        {
            // wildtype full span 
            // Ref -------++++-------
            //       ffsss    ssrr  (stitched both sides - should be stitched) 
            //       ffsss    rrrr  (stitched on one side - should be reverse)
            //       fffff    ssrr  (stitched on one side - should be forward)
            var readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 14,
                Cigar = new CigarAlignment("9M"),
                DirectionString = "2F:5S:2R"
            };
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:3S:4R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "5F:2S:2R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Forward, readSummary);

            // non spanning
            // Ref -------++++-------
            //       sssss          (should not contribute) 
            //                sssss (should not contribute) 
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 10,
                CigarString = "5M",
                DirectionString = "5S"
            };
            ExecuteTest(AlleleCategory.Insertion, null, readSummary);

            readSummary.ClipAdjustedStartPosition = 11;
            readSummary.ClipAdjustedEndPosition = 15;
            ExecuteTest(AlleleCategory.Insertion, null, readSummary);

            // mutant full span
            // Ref -------++++-------
            //       ffsssssssssrr  (stitched both sides - should be stitched) 
            //       ffsssrrrrrrrr  (stitched on one side - should be reverse)
            //       fffffffffssrr  (stitched on one side - should be forward)
            //       ffffffffsssrr  (partial stitched - should be stitched)
            //       ffssssrrrrrrr  (partial stitched - should be stitched)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 14,
                CigarString = "5M4I4M",
                DirectionString = "2F:9S:2R"
            };
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:3S:8R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "9F:2S:2R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "8F:3S:2R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:4S:7R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            // mutant partial or exact span
            // Ref -------++++-------
            //       ffssssrrr      (partial stitched - should be stitched) 
            //       ffsssr         (partial span - stitched on one side - should be reverse)
            //            fffsssrr  (partial stitched - should be stitched) 
            //              ffssrr  (partial span - stitched on one side - should be reverse)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 10,
                CigarString = "5M4I",
                DirectionString = "2F:4S:3R"
            };
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "5M1I";
            readSummary.DirectionString = "2F:3S:1R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Reverse, readSummary);

            readSummary.ClipAdjustedStartPosition = 11;
            readSummary.ClipAdjustedEndPosition = 14;
            readSummary.CigarString = "4I4M";
            readSummary.DirectionString = "2F:2S:2R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "2I4M";
            readSummary.DirectionString = "2F:3S:1R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Forward, readSummary);

            // other mutant?
            // Ref -------++++-------
            //       ffs---sssssrr  (deletion next to insertion of diff length?)
            //       ffs---rrrrrrr  (deletion next to insertion of diff length?)
            //       ffsss-----rrr  (deletion next to insertion of diff length?)
            //       ffs-------srr  (big deletion)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 14,
                CigarString = "3M3D7M",
                DirectionString = "2F:6S:2R"
            };
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:1S:7R";            
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Reverse, readSummary);

            readSummary.CigarString = "5M1D3M";
            readSummary.DirectionString = "2F:3S:3R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Reverse, readSummary);

            readSummary.CigarString = "3M3D3M";
            readSummary.DirectionString = "2F:2S:2R";
            ExecuteTest(AlleleCategory.Insertion, DirectionType.Stitched, readSummary);
        }

        [Fact]
        public void Deletion()
        {
            // wildtype full span
            // Ref -----dddd-------
            //       ffssssssrr  (stitched both sides - should be stitched) 
            //       ffsrrrrrrr  (stitched on one side - should be reverse)
            //       ffssrrrrrr  (partial stitch span - should be stitched)
            //       fffffffsrr  (stitched on one side - should be forward)
            //       ffffffssrr  (partial stitch span - should be stitched)
            var readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 8,
                ClipAdjustedEndPosition = 13,
                CigarString = "10M",
                DirectionString = "2F:6S:2R"
            };
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:1S:7R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "2F:2S:6R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "7F:1S:2R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "6F:2S:2R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            
            // non spanning
            // Ref -------dddd-------
            //       sssss          (should not contribute) 
            //                sssss (should not contribute) 
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 10,
                CigarString = "5M",
                DirectionString = "5S"
            };
            ExecuteTest(AlleleCategory.Deletion, null, readSummary);

            readSummary.ClipAdjustedStartPosition = 15;
            readSummary.ClipAdjustedEndPosition = 19;
            ExecuteTest(AlleleCategory.Deletion, null, readSummary);
            
            // mutant full span
            // Ref -------dddd-------
            //       ffffs----srrr  (stitched both sides - should be stitched) 
            //       ffsss----rrrr  (stitched on one side - should be reverse)
            //       fffff----ssrr  (stitched on one side - should be forward)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 18,
                CigarString = "5M4D4M",
                DirectionString = "4F:2S:3R"
            };
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:3S:4R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "5F:2S:2R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Forward, readSummary);

            // mutant partial span (capital letter = clipped)
            // note deletions can only be observed if there is soft clipping after
            // Ref -------dddd-------
            //       ffffs----SSSS  (stitched both sides - stitched) 
            //       fffff---FSRR  (forward)
            //       fffff---SRRR  (forward - this is a tricky one but i think still calling forward is right)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 18,
                CigarString = "5M4D4S",
                DirectionString = "4F:5S"
            };
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "5M3D5S";
            readSummary.DirectionString = "6F:1S:2R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "5F:1S:3R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Forward, readSummary);

            // other mutant?
            // Ref -----++dddd-------
            //       fs-----sssrr  (another deletion right next to it - stitched)
            //       fs--------rr  (bigger deletion spanning - reverse)
            //       fs--------sr  (bigger deletion spanning - stitched)
            //       fffff----ssr  (insertion next to deletion - forward)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 8,
                ClipAdjustedEndPosition = 17,
                CigarString = "2M3D5M",
                DirectionString = "1F:4S:2R"
            };
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "2M6D2M";
            readSummary.DirectionString = "1F:1S:2R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "1F:2S:1R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "5M4D3M";
            readSummary.DirectionString = "5F:2S:1R";
            ExecuteTest(AlleleCategory.Deletion, DirectionType.Forward, readSummary);
        }

        [Fact]
        public void MNV()
        {
            // full span
            // Ref -----mmmm---
            //        fssssssrr  (stitched both sides - should be stitched) 
            //        fsrrrrrrr  (stitched on one side - should be reverse)
            //        fssrrrrrr  (partial stitch span - should be stitched)
            //        ffffffsrr  (stitched on one side - should be forward)
            //        fffffssrr  (partial stitch span - should be stitched)
            var readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 8,
                ClipAdjustedEndPosition = 16,
                CigarString = "9M",
                DirectionString = "1F:6S:2R"
            };
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "1F:1S:7R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "1F:2S:6R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "6F:1S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "5F:2S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);


            // non spanning
            // Ref -------mmmm-------
            //       sssss          (should not contribute) 
            //                sssss (should not contribute) 
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 5,
                ClipAdjustedEndPosition = 9,
                CigarString = "5M",
                DirectionString = "5S"
            };
            ExecuteTest(AlleleCategory.Mnv, null, readSummary);

            readSummary.ClipAdjustedStartPosition = 14;
            readSummary.ClipAdjustedEndPosition = 18;
            ExecuteTest(AlleleCategory.Mnv, null, readSummary);

            // partial span 
            // Ref -------mmmm-------
            //        fffss         (stitched) 
            //        ffssr         (reverse) 
            //               fssrr  (forward) 
            //               sssrr  (stitched) 
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 6,
                ClipAdjustedEndPosition = 10,
                CigarString = "5M",
                DirectionString = "3F:2S"
            };
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "2F:2S:1R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Reverse, readSummary);

            readSummary.ClipAdjustedStartPosition = 13;
            readSummary.ClipAdjustedEndPosition = 17;
            readSummary.DirectionString = "1F:2S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "3S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            // partial span with softclipping (capital letter = clipped)
            // Ref -------mmmm-------
            //          fsSSSS  (stitched both sides - stitched) 
            //          ffFSRR  (forward)
            //          ffSRRR  (forward - this is a tricky one but i think still calling forward is right)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 8,
                ClipAdjustedEndPosition = 13,
                CigarString = "2M4S",
                DirectionString = "1F:5S"
            };
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.DirectionString = "3F:1S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Forward, readSummary);

            readSummary.DirectionString = "2F:1S:3R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Forward, readSummary);

            // other mutant?
            // Ref -----++mmmm-------
            //       fs-----sssrr  (deletion right next to it - stitched)
            //       fs--------rr  (fully spanning deletion - reverse)
            //       fs--------sr  (fully spanning deletion - stitched)
            //       fffff----ssr  (insertion next to deletion - forward)
            readSummary = new ReadCoverageSummary
            {
                ClipAdjustedStartPosition = 7,
                ClipAdjustedEndPosition = 16,
                CigarString = "2M3D5M",
                DirectionString = "1F:4S:2R"
            };
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "2M6D2M";
            readSummary.DirectionString = "1F:1S:2R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Reverse, readSummary);

            readSummary.DirectionString = "1F:2S:1R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Stitched, readSummary);

            readSummary.CigarString = "5M4D3M";
            readSummary.DirectionString = "5F:2S:1R";
            ExecuteTest(AlleleCategory.Mnv, DirectionType.Forward, readSummary);
        }

        private void ExecuteTest(AlleleCategory variantType, DirectionType? expectedDirection, ReadCoverageSummary readSummary, 
            int variantPosition = 10)
        {
            var calculator = new ExactCoverageCalculator();
            var allele = new CalledAllele(variantType) {ReferencePosition = variantPosition};

            switch (allele.Type)
            {
                case AlleleCategory.Insertion:
                    allele.ReferenceAllele = "A";
                    allele.AlternateAllele = "ATTTT";
                    break;
                case AlleleCategory.Deletion:
                    allele.AlternateAllele = "A";
                    allele.ReferenceAllele = "ATTTT";
                    break;
                default:
                    allele.ReferenceAllele = "AAAA";
                    allele.AlternateAllele = "TTTT";
                    break;
            }
            var source = new Mock<IAlleleSource>();
            source.Setup(s => s.GetSpanningReadSummaries(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new List<ReadCoverageSummary> { readSummary });

            calculator.Compute(allele, source.Object);
            
            for(var i = 0; i < allele.EstimatedCoverageByDirection.Length; i ++)
                Assert.Equal(expectedDirection.HasValue && i == (int)expectedDirection ? 1 : 0, allele.EstimatedCoverageByDirection[i]);
        }
    }
}
