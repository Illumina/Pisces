using Alignment.Domain.Sequencing;
using Gemini.Logic;
using Xunit;

namespace Gemini.Tests
{
    public class StitchedRealignmentHelpersTests
    {
        [Fact]
        public void GetUpdatedXdForRealignedStitchedRead()
        {
            // Add a deletion in single-direction region
            // MMMMMMMM MM // Orig Cigar
            // FFSSSSSR RR // Orig Directions
            // MMMMMMMMDMM // New Cigar
            // FFSSSSSRRRR // New Directions
            TestNewStitchedDirections("10M", "2F5S3R", "8M1D2M", "2F5S4R");

            // Add a deletion in stitched region
            // MMMMMMMM MM // Orig Cigar
            // FFSSSSSS SR // Orig Directions
            // MMMMMMMMDMM // New Cigar
            // FFSSSSSSSSR // New Directions
            TestNewStitchedDirections("10M", "2F7S1R", "8M1D2M", "2F8S1R");

            // Add a deletion in border region
            // MMMMMMMM MM // Orig Cigar
            // FFSSSSSS RR // Orig Directions
            // MMMMMMMMDMM // New Cigar
            // FFSSSSSS?RR // New Directions
            // TODO not clear what the right thing to do is here, we're just going with the later one for now
            TestNewStitchedDirections("10M", "2F6S2R", "8M1D2M", "2F6S3R");

            // Add a deletion in border region - early border
            // MM MMMMMMMM // Orig Cigar
            // FF SSSSSSRR // Orig Directions
            // MMDMMMMMMMM // New Cigar
            // FF?SSSSSSRR // New Directions
            // TODO - we just take the direction after the deletion and apply it to the entire deletion. There may be a more nuanced answer? Unclear, and fairly low priority
            TestNewStitchedDirections("10M", "2F6S2R", "2M1D8M", "2F7S2R");

            // Add a deletion in border region
            // MMMMMMMM MM // Orig Cigar
            // FFFFFFFS RR // Orig Directions
            // MMMMMMMMDMM // New Cigar
            // FFFFFFFS?RR // New Directions
            // TODO - we just take the direction after the deletion and apply it to the entire deletion. There may be a more nuanced answer? Unclear, and fairly low priority
            TestNewStitchedDirections("10M", "7F1S2R", "8M1D2M", "7F1S3R");

            // Add a deletion at the very end
            // MMMMMMMMMM // Orig Cigar
            // FFSSSSSSRR // Orig Directions
            // MMMMMMMMMMDD // New Cigar
            // FFSSSSSSRRRR // New Directions
            TestNewStitchedDirections("10M", "2F6S2R", "10M2D", "2F6S4R");

            // Add a deletion at the very beginning
            //   MMMMMMMMMM // Orig Cigar
            //   FFSSSSSSRR // Orig Directions
            // DDMMMMMMMMMM // New Cigar
            // FFFFSSSSSSRR // New Directions
            TestNewStitchedDirections("10M", "2F6S2R", "2D10M", "4F6S2R");

            // Remove a deletion in single-direction region
            // MMMMMMMMDMM // Orig Cigar
            // FFSSSSSRRRR // Orig Directions
            // MMMMMMMM MM // New Cigar
            // FFSSSSSR RR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F5S4R", "10M", "2F5S3R");

            // Remove a deletion in stitched region
            // MMMMMMMMDMM // Orig Cigar
            // FFSSSSSSSSR // Orig Directions
            // MMMMMMMM MM // New Cigar
            // FFSSSSSS SR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F8S1R", "10M", "2F7S1R");

            // Remove a deletion in border region
            // MMMMMMMMDMM // Orig Cigar
            // FFSSSSSSRRR // Orig Directions
            // MMMMMMMM MM // New Cigar
            // FFSSSSSS RR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F6S3R", "10M", "2F6S2R");

            // Remove a deletion at the very end
            // MMMMMMMMMMDD // Orig Cigar
            // FFSSSSSSRRRR // Orig Directions
            // MMMMMMMMMM // New Cigar
            // FFSSSSSSRR // New Directions
            TestNewStitchedDirections("10M2D", "2F6S4R", "10M", "2F6S2R");

            // Remove a deletion at the very beginning
            // DDMMMMMMMMMM // Orig Cigar
            // FFFFSSSSSSRR // Orig Directions
            //   MMMMMMMMMM // New Cigar
            //   FFSSSSSSRR // New Directions
            TestNewStitchedDirections("2D10M", "4F6S2R", "10M", "2F6S2R");

            // Move deletion
            // MMMMM MMMDMM // Orig Cigar
            // FFSSS SSSRRR // Orig Directions
            // MMMMMDMMM MM // New Cigar
            // FFSSSSSSS RR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F6S3R", "5M1D5M", "2F7S2R");

            // Same deletion
            // MMMMMMMMDMM // Orig Cigar
            // FFSSSSSSRRR // Orig Directions
            // MMMMMMMMDMM // New Cigar
            // FFSSSSSSRRR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F6S3R", "8M1D2M", "2F6S3R");

            // Deletion at same spot but different length - gets longer
            // MMMMMMMMD MM // Orig Cigar
            // FFSSSSSSR RR // Orig Directions
            // MMMMMMMMDDMM // New Cigar
            // FFSSSSSSRRRR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F6S3R", "8M2D2M", "2F6S4R");

            // Deletion at same spot but different length - gets shorter
            // MMMMMMMMDDMM // Orig Cigar
            // FFSSSSSSRRRR // Orig Directions
            // MMMMMMMMD MM // New Cigar
            // FFSSSSSSR RR // New Directions
            TestNewStitchedDirections("8M2D2M", "2F6S4R", "8M1D2M", "2F6S3R");

            // Insertion instead
            // MMMMMMMMDMM // Orig Cigar
            // FFSSSSSSRRR // Orig Directions
            // MMMMMMMM IM // New Cigar
            // FFSSSSSS RR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F6S3R", "9M1I", "2F6S2R");

            // Add multiple deletions
            // MMMM MMMM MM // Orig Cigar
            // FFSS SSSR RR // Orig Directions
            // MMMMDMMMMDMM // New Cigar
            // FFSSSSSSRRRR // New Directions
            TestNewStitchedDirections("10M", "2F5S3R", "4M1D4M1D2M", "2F6S4R");

            // Add deletion in addition to one already there
            // MMMM MMMMDMM // Orig Cigar
            // FFSS SSSRRRR // Orig Directions
            // MMMMDMMMMDMM // New Cigar
            // FFSSSSSSRRRR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F5S4R", "4M1D4M1D2M", "2F6S4R");

            // Add a multi-base deletion in single-direction region
            // MMMMMMMM      MM // Orig Cigar
            // FFSSSSSR      RR // Orig Directions
            // MMMMMMMMDDDDDDMM // New Cigar
            // FFSSSSSRRRRRRRRR // New Directions
            TestNewStitchedDirections("10M", "2F5S3R", "8M6D2M", "2F5S9R");

            // Remove multiple deletions
            // MMMMDMMMMDMM // Orig Cigar
            // FFSSSSSSRRRR // Orig Directions
            // MMMM MMMM MM // New Cigar
            // FFSS SSSR RR // New Directions
            TestNewStitchedDirections("4M1D4M1D2M", "2F6S4R", "10M", "2F5S3R");

            // Remove only one of multiple deletions
            // MMMMDMMMMDMM // Orig Cigar
            // FFSSSSSSRRRR // Orig Directions
            // MMMMDMMMM MM // New Cigar
            // FFSSSSSSR RR // New Directions
            TestNewStitchedDirections("4M1D4M1D2M", "2F6S4R", "4M1D6M", "2F6S3R");

            // Start with multiple deletions, change size of one
            // MMMMDMMMMD  MM // Orig Cigar
            // FFSSSSSSRR  RR // Orig Directions
            // MMMMDMMMMDDDMM // New Cigar
            // FFSSSSSSRRRRRR // New Directions
            TestNewStitchedDirections("4M1D4M1D2M", "2F6S4R", "4M1D4M3D2M", "2F6S6R");

            // Add multi-base deletion on border of different regions
            // MMMMMMMM   MM // Orig Cigar
            // FFSSSSSS   RR // Orig Directions
            // MMMMMMMMDDDMM // New Cigar
            // FFSSSSSSRRRRR // New Directions
            TestNewStitchedDirections("10M", "2F6S2R", "8M3D2M", "2F6S5R");

            // Lengthen deletion on border of different regions
            // MMMMMMMMD  MM // Orig Cigar
            // FFSSSSSSS  RR // Orig Directions
            // MMMMMMMMDDDMM // New Cigar
            // FFSSSSSSRRRRR // New Directions
            TestNewStitchedDirections("8M1D2M", "2F7S2R", "8M3D2M", "2F6S5R");

            // Lengthen multi-base deletion spanning different regions
            // MMMMMMMMDDMM // Orig Cigar
            // FFSSSSSSSRRR // Orig Directions
            // MMMMMMMMDDDMM // New Cigar
            // FFSSSSSSRRRRR // New Directions 
            // FFSSSSSSSRRRR // New Directions - other option?
            // TODO - we just take the right-most one and apply it to the entire deletion. There may be a better answer? Maybe just apply it to the part that is extended? Unclear, and low priority
            //TestNewStitchedDirections("8M2D2M", "2F7S3R", "8M3D2M", "2F7S4R");
            TestNewStitchedDirections("8M2D2M", "2F7S3R", "8M3D2M", "2F6S5R");

            // Remove multi-base deletion spanning different regions
            // MMMMMMMMDDDMM // Orig Cigar
            // FFSSSSSSSRRRR // Orig Directions
            // MMMMMMMM   MM // New Cigar
            // FFSSSSSS   RR // New Directions 
            TestNewStitchedDirections("8M3D2M", "2F7S4R", "10M", "2F6S2R");

            // Shorten multi-base deletion spanning different regions so that it only spans one?
            // MMMMMMMMDDDMM // Orig Cigar
            // FFSSSSSSSRRRR // Orig Directions
            // MMMMMMMMD  MM // New Cigar
            // FFSSSSSSS  RR // New Directions 
            // TODO - we just take the right-most one and apply it to the entire deletion. There may be a better answer? Maybe just trim down the part that is "removed"? Unclear, and low priority
            TestNewStitchedDirections("8M3D2M", "2F7S4R", "8M1D2M", "2F6S3R");

            // For read-spanning stuff - insertion, softclip, match - directions should stay the same
            // Assumes same number of bases... but it should never be possible to be a diff number of bases
            TestNewStitchedDirections("10M", "2F5S3R", "9M1I", "2F5S3R");
            TestNewStitchedDirections("10M", "2F5S3R", "1S3M1I5M1S", "2F5S3R");
            TestNewStitchedDirections("9M1I", "2F5S3R", "10M", "2F5S3R");
            TestNewStitchedDirections("1S3M1I5M1S", "2F5S3R", "10M", "2F5S3R");

            // Real example
            var origRead = TestHelpers.CreateBamAlignment(
                "ATTGACATTAACTTAATTGTTTTTACTGACATTCTTAATTGCTTTTTGGAATTCATTAGCTGGTATAATACTAAAGTAATAAATACGTTGTGTTTTTCTAAAGGCATCTGAAATAGTGGAGCTAAATACTAAAACTGGGATAAAAATAATGGTAATTTTAGCTTACAAATAAACA",
                66214327, 0, 30, false, cigar: new CigarAlignment("175M"));
            TagUtils.ReplaceOrAddStringTag(ref origRead.TagData, "XD", "74F25S76R");

            var realignedRead = TestHelpers.CreateBamAlignment(
                "ATTGACATTAACTTAATTGTTTTTACTGACATTCTTAATTGCTTTTTGGAATTCATTAGCTGGTATAATACTAAAGTAATAAATACGTTGTGTTTTTCTAAAGGCATCTGAAATAGTGGAGCTAAATACTAAAACTGGGATAAAAATAATGGTAATTTTAGCTTACAAATAAACA",
                66214327, 0, 30, false, cigar: new CigarAlignment("170M6D5M"));

            var newXd = StitchedRealignmentHelpers.GetUpdatedXdForRealignedStitchedRead(origRead, realignedRead);
            Assert.Equal("74F25S82R", newXd);


        }

        private void TestNewStitchedDirections(string origCigar1, string origDirections, string newCigar,
            string expectedNewDirections)
        {
            var origRead = TestHelpers.CreateBamAlignment(
                "ATTTACGGGC",
                66214327, 0, 30, false, cigar: new CigarAlignment(origCigar1));
            TagUtils.ReplaceOrAddStringTag(ref origRead.TagData, "XD", origDirections);

            var realignedRead = TestHelpers.CreateBamAlignment(
                "ATTTACGGGC",
                66214327, 0, 30, false, cigar: new CigarAlignment(newCigar));

            var newXd = StitchedRealignmentHelpers.GetUpdatedXdForRealignedStitchedRead(origRead, realignedRead);
            Assert.Equal(expectedNewDirections, newXd);
        }
    }
}