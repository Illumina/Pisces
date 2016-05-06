using Xunit;

namespace Pisces.Tests.UnitTests.Alignment
{
    public class StitcherTests
    {        

        [Fact]
        public void TryStitch_RejectInvalidCigar()
        {
            //TODO revisit this when we come to stitching. We are currently allowing insertions on the edges of reads but looks like it was causing problems in stitching!!
            // 1234...   1 - 2 3 4 5 6 - - 7 8 9 0
            // Read1     X X X X X X X X - - - - -
            // Read1     M I M M M M M I - - - - -
            // Read2     - - - X X X X X X X X - -
            // Read2     - - - M M M M I I M M - -

            //Without rejecting, throws exception in copying the qualities. Also consensus sequence is messed up.
            //The issues don't really present themselves til you get to stitching,
            //but may as well do the error checking early on when we're doing other cigar validation

            //var read1Softclip = new Read("chr1", "ATCGATCGNNN", 12341);
            //var ex = Assert.Throws<Exception>(() => read1Softclip.CigarData = new CigarAlignment("7M1I3S"));
            //Assert.Contains("invalid cigar", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one

            //var read2Softclip = new Read("chr1", "NNNATTT", 12341);
            //ex = Assert.Throws<Exception>(() => read2Softclip.CigarData = new CigarAlignment("3S1I3M"));
            //Assert.Contains("invalid cigar", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one

            //var read1NoSoftclip = new Read("chr1", "ATCGATCG", 12341);
            //ex = Assert.Throws<Exception>(() => read1NoSoftclip.CigarData = new CigarAlignment("7M1I"));
            //Assert.Contains("invalid cigar", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one

            //var read2NoSoftclip = new Read("chr1", "ATTT", 12341);
            //ex = Assert.Throws<Exception>(() => read2NoSoftclip.CigarData = new CigarAlignment("3S1I3M"));
            //Assert.Contains("invalid cigar", ex.Message, StringComparison.InvariantCultureIgnoreCase); // This is brittle but since a variety of exceptions can happen in this process want to make sure it's this specific one
        }


    }
}