using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alignment.Domain.Sequencing;
using Xunit;

namespace Alignment.Domain.Tests
{
    public class ReadPairTests
    {
        private BamAlignment CreateAlignment(string name = "", int position = 1, bool isMapped = true, bool isSecondary = false, string cigar = "4M", bool reverse = false, bool isSupplementary = false, string supplementary = null)
        {
            var alignment = new BamAlignment();
            alignment.Name = name;
            alignment.Position = position;
            alignment.RefID = 1;
            if (isSupplementary)
            {
                alignment.AlignmentFlag = 2048;
            }
            alignment.SetIsUnmapped(!isMapped);
            alignment.SetIsSecondaryAlignment(isSecondary);
            if (supplementary != null)
            {
                var tagUtils = new TagUtils();
                tagUtils.AddStringTag("SA", supplementary);
                alignment.TagData = tagUtils.ToBytes();
            }
            if (reverse)
            {
                alignment.SetIsReverseStrand(true);
            }

            alignment.CigarData = new CigarAlignment(cigar);

            alignment.Bases = new string('A', (int)alignment.CigarData.GetReadSpan());
            alignment.Qualities = new byte[alignment.Bases.Length];
            alignment.SetIsProperPair(true);
            return alignment;
        }

        [Fact]
        public void GetRead1Alignments()
        {
            var alignment = CreateAlignment();
            var readpair = new ReadPair(alignment);
            Assert.Equal(1, readpair.Read1Alignments.Count);

            var secondary = CreateAlignment();
            secondary.SetIsSecondaryAlignment(true);
            readpair.AddAlignment(secondary, ReadNumber.Read1);
            Assert.Equal(2, readpair.Read1Alignments.Count);

            var secondaryR2 = CreateAlignment();
            secondaryR2.IsSupplementaryAlignment();
            readpair.AddAlignment(secondaryR2, ReadNumber.Read2);
            Assert.Equal(2, readpair.Read1Alignments.Count);
        }

        [Fact]
        public void GetRead2Alignments()
        {
            var alignment = CreateAlignment();
            var readpair = new ReadPair(alignment, readNumber:ReadNumber.Read2);
            Assert.Equal(1, readpair.Read2Alignments.Count);

            var secondary = CreateAlignment();
            secondary.SetIsSecondaryAlignment(true);
            readpair.AddAlignment(secondary, ReadNumber.Read2);
            Assert.Equal(2, readpair.Read2Alignments.Count);

            var secondaryR2 = CreateAlignment();
            secondaryR2.IsSupplementaryAlignment();
            readpair.AddAlignment(secondaryR2, ReadNumber.Read1);
            Assert.Equal(2, readpair.Read2Alignments.Count);
        }

        [Fact]
        public void IsComplete()
        {
            // Primary alignments do not indicate presence of any supplementaries and read pair does not have supplementaries
            var alignment = CreateAlignment();
            var readpair = new ReadPair(alignment);
            Assert.False(readpair.IsComplete(false));

            var alignment2 = CreateAlignment();
            readpair.AddAlignment(alignment2, ReadNumber.Read2);
            Assert.True(readpair.IsComplete(false));
            Assert.True(readpair.IsComplete(true));

            // Primary alignment indicates presence of supplementaries but read pair does not have them
            readpair = new ReadPair(alignment);
            var alignmentWithSupplementary = CreateAlignment(supplementary: "chr1,100,+,3M,50,1");
            readpair.AddAlignment(alignmentWithSupplementary, ReadNumber.Read2);
            Assert.False(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));

            // Does not expect supplementaries, does have supplementaries
            readpair = new ReadPair(alignment);
            readpair.AddAlignment(alignment2, ReadNumber.Read2);
            var supplementaryAlignment = CreateAlignment(isSupplementary: true);
            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read2);
            Assert.True(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));

            // Does expect supplementaries, does have them
            readpair = new ReadPair(alignment);
            readpair.AddAlignment(alignmentWithSupplementary, ReadNumber.Read2);
            Assert.False(readpair.IsComplete(true));

            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read2);
            Assert.True(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));

            // Does expect supplementaries, only has one
            readpair = new ReadPair(alignmentWithSupplementary);
            readpair.AddAlignment(alignmentWithSupplementary, ReadNumber.Read2);
            Assert.False(readpair.IsComplete(true));

            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read2);
            Assert.False(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));

            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read2);
            Assert.False(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));

            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read1);
            Assert.True(readpair.IsComplete(true));
            Assert.True(readpair.IsComplete(false));
        }

        [Fact]
        public void GetAlignments()
        {
            var alignment = CreateAlignment();
            var alignmentWithSupplementary = CreateAlignment(supplementary: "chr1,100,+,3M,50,1");
            var supplementaryAlignment = CreateAlignment(isSupplementary: true);

            var readpair = new ReadPair(alignment);
            readpair.AddAlignment(alignmentWithSupplementary, ReadNumber.Read2);
            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read2);
            readpair.AddAlignment(supplementaryAlignment, ReadNumber.Read1);

            Assert.Equal(4, readpair.GetAlignments().Count());

            readpair = new ReadPair(alignment);
            readpair.AddAlignment(alignmentWithSupplementary, ReadNumber.Read2);

            Assert.Equal(2, readpair.GetAlignments().Count());

        }

    }
}
