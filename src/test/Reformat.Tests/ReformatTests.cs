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

            //For added challenge, this is a none-pisces vcf we are parsing and reformatting.
            var inputDir = Path.Combine(TestPaths.LocalTestDataDirectory);
            var testVcf = Path.Combine(inputDir, "CrushedExample.vcf");
            var inputFile = Path.Combine(outDir, "CrushedExample.vcf");

            TestHelper.RecreateDirectory(outDir);

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            if (File.Exists(inputFile))
                File.Delete(inputFile);

            File.Copy(testVcf, inputFile);

            var crushedOutFile = Path.Combine(outDir, "crushedexample.crushed.vcf");
            var uncrushedOutFile = Path.Combine(outDir, "crushedexample.uncrushed.vcf");
            var options = new ReformatOptions();
            options.VcfPath = inputFile;
            options.VariantCallingParams.AmpliconBiasFilterThreshold = null; //just to keep vcf header the same as before we added this filter.

            //ouput uncrushed
            options.VcfWritingParams.ForceCrush = false;
            ReformatVcf.Reformat.DoReformating(options);

            //output crushed
            options.VcfWritingParams.ForceCrush = true;
            ReformatVcf.Reformat.DoReformating(options);

            //check files
            TestHelper.CompareFiles(crushedOutFile, Path.Combine(TestPaths.LocalTestDataDirectory, "expected.crushed.vcf"));
            TestHelper.CompareFiles(uncrushedOutFile, Path.Combine(TestPaths.LocalTestDataDirectory, "expected.uncrushed.vcf"));
        }

    }
}
