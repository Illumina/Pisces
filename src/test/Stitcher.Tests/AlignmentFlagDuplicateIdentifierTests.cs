using Alignment.Domain.Sequencing;
using Xunit;
using Common.IO.Utility;

namespace Stitcher.Tests
{
    public class AlignmentFlagDuplicateIdentifierTests
    {
        [Fact]
        public void IsDuplicate()
        {
            var duplicateIdentifier = new AlignmentFlagDuplicateIdentifier();

            var alignment = new BamAlignment();
            alignment.SetIsDuplicate(true);

            Assert.True(duplicateIdentifier.IsDuplicate(alignment));

            alignment.SetIsDuplicate(false);
            Assert.False(duplicateIdentifier.IsDuplicate(alignment));
        }
    }

}
