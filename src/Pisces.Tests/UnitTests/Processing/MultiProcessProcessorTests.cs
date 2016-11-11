using System.IO;
using System.Linq;
using System.Threading;
using CallVariants.Logic.Processing;
using Moq;
using Pisces.Interfaces;
using Pisces.Logic.Processing;
using Pisces.Processing.Utility;
using Pisces.Tests.MockBehaviors;
using Pisces.IO.Sequencing;
using TestUtilities;
using Xunit;

namespace Pisces.Tests.UnitTests.Processing
{
    public class MultiProcessProcessorTests
    {
        /// <summary>
        /// These tests mock out the variant calling.  See SomaticVariantCaller_MultipleChr for this same scenario but with live calling.
        /// </summary>

        [Fact]
        [Trait("ReqID", "SDS-52")]
        public void Execute()
        {
            // works without specified output folder
            ExecuteTest(2);
            ExecuteTest(8);
            ExecuteTest(1);

            // works with specified output folder
            var outputFolder = Path.Combine(UnitTestPaths.TestDataDirectory, "MultiProcessOut");
            ExecuteTest(2, outputFolder);
            ExecuteTest(8, outputFolder);
            ExecuteTest(1, outputFolder);
        }

        private string Stage(string sourcePath, string destPrefix, string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var destPath = Path.Combine(folder, destPrefix + "_" + Path.GetFileName(sourcePath));

            if (!File.Exists(destPath))
                File.Copy(sourcePath, destPath);
            if (!File.Exists(destPath + ".bai"))
                File.Copy(sourcePath + ".bai", destPath + ".bai");

            return destPath;
        }

        // tests two bams in different folders
        // expectations:
        // - if outputfolder is not specified, logs are in directory of first bam
        // - if outputfolder specified, logs are in output folder
        // - vcf files have header and both chromosomes, output is where normally expected
        private void ExecuteTest(int numberOfThreads, string outputFolder = null)
        {
            var sourcePath = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19.bam");

            var otherTestDirectory = Path.Combine(UnitTestPaths.TestDataDirectory, "MultiProcessIn");
            var bamFilePath1 = Stage(sourcePath, "In1", otherTestDirectory + "1");
            var bamFilePath2 = Stage(sourcePath, "In2", otherTestDirectory + "2");

            var genomePath = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var options = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath1, bamFilePath2 },
                GenomePaths = new[] { genomePath },
                OutputFolder = outputFolder,
                CommandLineArguments = string.Format("-B {0},{1} -g {2}{3}", bamFilePath1, bamFilePath2, genomePath, string.IsNullOrEmpty(outputFolder) ? string.Empty : " -OutFolder " + outputFolder).Split(' ')
            };

            var factory = new Factory(options);
            foreach (var workRequest in factory.WorkRequests)
            {
                if (File.Exists(workRequest.OutputFilePath))
                    File.Delete(workRequest.OutputFilePath);
            }

            var exePath = Path.Combine(UnitTestPaths.WorkingDirectory, "Pisces.exe");
            

            var processor = new MultiProcessProcessor(factory, factory.GetReferenceGenome(options.GenomePaths[0]), new JobManager(1), options.CommandLineArguments, options.OutputFolder, options.LogFolder, exePath: exePath);
            processor.Execute(numberOfThreads);

            foreach (var workRequest in factory.WorkRequests)
            {
                using (var reader = new VcfReader(workRequest.OutputFilePath))
                {
                    Assert.True(reader.HeaderLines.Any());
                    var variants = reader.GetVariants().ToList();

                    Assert.Equal(2, variants.Count());
                    Assert.Equal("chr17", variants.First().ReferenceName);
                    Assert.Equal("chr19", variants.Last().ReferenceName);
                }
            }

            Assert.True(Directory.GetFiles(options.LogFolder, "chr17_*" + ApplicationOptions.LogFileNameBase).Any());
            Assert.True(Directory.GetFiles(options.LogFolder, "chr19_*" + ApplicationOptions.LogFileNameBase).Any());
        }
    }
}
