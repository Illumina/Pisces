using System.IO;
using Common.IO.Sequencing;
using Xunit;

namespace Common.IO.Tests
{
    public class IGenomesReferencePathTests
    {
        [Fact]
        public void PathIssues()
        {
           var genomeFolder = TestPaths.SharedGenomesDirectory;

            var bacillusFasta = Path.Combine(genomeFolder, "Genomes","Bacillus_cereus","Sequence","WholeGenomeFasta", "genome.fa");

            //TJD: this is strange behaviour. This error handling seems to be of little benefit.
            Assert.Null(IGenomesReferencePath.GetReferenceFromFastaPath(null));
            Assert.Null(IGenomesReferencePath.GetReferenceFromFastaPath("test"));
            Assert.Null(IGenomesReferencePath.GetReferenceFromFastaPath("file:test"));

            var tempFile = Path.GetTempFileName();
            var myPath = IGenomesReferencePath.GetReferenceFromFastaPath(tempFile);
            Assert.Null(myPath);

            //uses a real dir
            var bacillusPath1 = IGenomesReferencePath.GetReferenceFromFastaPath(bacillusFasta);
            Assert.NotNull(bacillusPath1.Species);
            Assert.Equal("Genomes", bacillusPath1.Species);
        }
    }
}
