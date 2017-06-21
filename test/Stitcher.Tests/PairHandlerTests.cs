using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Xunit;
using Moq;
using StitchingLogic;
using StitchingLogic.Tests;

namespace Stitcher.Tests
{
    public class PairHandlerTests
    {
        [Fact]
        public void ExtractReadsWithUmis()
        {
            ExtractReads(true);
        }
        [Fact]
        public void ExtractReadsWithoutUmis()
        {
            ExtractReads(false);
        }
        private void ExtractReads(bool addUmiTags)
        {
            var refIdMapping = new Dictionary<int, string>() { { 1, "chr1" } };

			var stitcher = StitcherTestHelpers.GetStitcher(10, false); 

			var readStatusCounter = new ReadStatusCounter();

            var pairHandler = new PairHandler(refIdMapping, stitcher, true, readStatusCounter);

            var alignment1 = new BamAlignment()
            {
                AlignmentFlag = 0,
                Bases = "ABCF",
                Bin = 4,
                CigarData = new CigarAlignment("2S2M"),
                FragmentLength = 42,
                MapQuality = 1,
                MatePosition = 2,
                MateRefID = 43,
                Name = "Read1",
                Position = 1,
                Qualities = new byte[4],
                RefID = 1,
                TagData = new byte[0]
            };

			var tagUtils = new TagUtils(); 
			tagUtils.AddStringTag("BC","14");
			tagUtils.AddIntTag("SM",40);
            if (addUmiTags)
            {
                tagUtils.AddIntTag("XV", 1);
                tagUtils.AddIntTag("XW", 2);
                tagUtils.AddStringTag("XU", "ABBA-ZABBA");
            }

            alignment1.AppendTagData(tagUtils.ToBytes());

			var alignment2 = new BamAlignment()
            {
                AlignmentFlag = 0,
                Bases = "ABCF",
                Bin = 4,
                CigarData = new CigarAlignment("2S2M"),
                FragmentLength = 42,
                MapQuality = 1,
                MatePosition = 2,
                MateRefID = 43,
                Name = "Read1",
                Position = 1,
                Qualities = new byte[4],
                RefID = 1,
                TagData = new byte[0]
            };

			var tagUtils2 = new TagUtils();
			tagUtils2.AddIntTag("NM", 5);
			tagUtils2.AddStringTag("BC", "14");
			tagUtils2.AddIntTag("SM", 20);
            if (addUmiTags)
            {
                tagUtils2.AddIntTag("XV", 1);
                tagUtils2.AddIntTag("XW", 2);
                tagUtils2.AddStringTag("XU", "ABBA-ZABBA");
            }
            alignment2.AppendTagData(tagUtils2.ToBytes());

			var readPair = new ReadPair(alignment1);
            readPair.AddAlignment(alignment2);

            var alignmentResults = pairHandler.ExtractReads(readPair);

	        Assert.Equal(1, alignmentResults.Count);
	        var alignment = alignmentResults[0];


	        Assert.NotNull(alignment.GetStringTag("XD"));
	        Assert.Null(alignment.GetIntTag("NM"));
	        Assert.Null(alignment.GetStringTag("BC"));
	        Assert.Null(alignment.GetIntTag("SM"));
            if (addUmiTags)
            {
                Assert.Equal("ABBA-ZABBA", alignment.GetStringTag("XU"));
                Assert.Equal(1, alignment.GetIntTag("XV"));
                Assert.Equal(2, alignment.GetIntTag("XW"));
            }



        }
    }

}
