using System.IO;
using ReformatVcf;
using TestUtilities;
using Xunit;

namespace Reformat.Tests
{  
    public class ReformatTests
    {
        [Fact]
        public void ReformatTest()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "UncrushOutDir");
            var inputDir = Path.Combine(TestPaths.LocalTestDataDirectory);
            var testVcf = Path.Combine(inputDir, "CrushedExample.vcf");
            var inputFile = Path.Combine(outDir, "CrushedExample.vcf");

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            if (File.Exists(inputFile))
                File.Delete(inputFile);

            File.Copy(testVcf, inputFile);

            var crushedOutFile = Path.Combine(outDir, "crushedexample.crushed.vcf");
            var uncrushedOutFile = Path.Combine(outDir, "crushedexample.uncrushed.vcf");


            //ouput uncrushed
            ReformatVcf.Reformat.DoReformating(inputFile, false);

            //output crushed
            ReformatVcf.Reformat.DoReformating(inputFile, true);

            //check files
            TestHelper.CompareFiles(crushedOutFile, Path.Combine(TestPaths.LocalTestDataDirectory, "expected.crushed.vcf"));
            TestHelper.CompareFiles(uncrushedOutFile, Path.Combine(TestPaths.LocalTestDataDirectory, "expected.uncrushed.vcf"));
        }

    }
}
