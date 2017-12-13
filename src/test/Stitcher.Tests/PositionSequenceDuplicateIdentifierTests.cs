using Alignment.Domain.Sequencing;
using Xunit;

namespace Stitcher.Tests
{
    public class PositionSequenceDuplicateIdentifierTests
    {
        private BamAlignment GetAlignment()
        {
            var alignment = new BamAlignment();
            alignment.Bases = "ATCG";
            alignment.Position = 123;
            alignment.RefID = 1;
            alignment.CigarData = new CigarAlignment("4M");

            return alignment;
        }

        [Fact]
        public void IsDuplicate()
        {
            var duplicateIdentifier = new PositionSequenceDuplicateIdentifier();

            var alignment = GetAlignment();

            // First time seeing it: not duplicate
            Assert.False(duplicateIdentifier.IsDuplicate(alignment));
            // Pass exact same read through: duplicate
            Assert.True(duplicateIdentifier.IsDuplicate(alignment));

            var diffSeqAlignment = GetAlignment();
            diffSeqAlignment.Bases = "TTTT";
            // First time seeing it: not duplicate
            Assert.False(duplicateIdentifier.IsDuplicate(diffSeqAlignment));
            // Pass exact same read through: duplicate
            Assert.True(duplicateIdentifier.IsDuplicate(diffSeqAlignment)); 

            var diffCigarAlignment = GetAlignment();
            diffCigarAlignment.CigarData = new CigarAlignment("2S2M");
            // First time seeing it: not duplicate
            Assert.False(duplicateIdentifier.IsDuplicate(diffCigarAlignment));
            // Pass exact same read through: duplicate
            Assert.True(duplicateIdentifier.IsDuplicate(diffCigarAlignment));

            var diffPositionAlignment = GetAlignment();
            diffPositionAlignment.Position = 456;
            // First time seeing it: not duplicate
            Assert.False(duplicateIdentifier.IsDuplicate(diffPositionAlignment));
            // Pass exact same read through: duplicate
            Assert.True(duplicateIdentifier.IsDuplicate(diffPositionAlignment));

            var diffChromosomeAlignment = GetAlignment();
            diffChromosomeAlignment.RefID = 2;
            // First time seeing it: not duplicate
            Assert.False(duplicateIdentifier.IsDuplicate(diffChromosomeAlignment));
            // Pass exact same read through: duplicate
            Assert.True(duplicateIdentifier.IsDuplicate(diffChromosomeAlignment));

            // TODO - should we be setting, ignoring, etc. the duplicate flags if we are calculating duplicate-ness de novo?
        }
    }
}