using System.IO;
using System.Linq;
using Pisces.Domain.Options;
using Common.IO.Utility;
using Pisces.IO;
using CallVariants.Logic.Processing;
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
            //Assert.True(false, "Fix this! The multiprocessor currently causes stack over flow horror.");

            // works without specified output folder
            ExecuteTest(2);
            ExecuteTest(8);
            ExecuteTest(1);

            // works with specified output folder
            var outputFolder = Path.Combine(TestPaths.LocalScratchDirectory, "MultiProcessOut");
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
            var sourcePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var otherTestDirectory = Path.Combine(TestPaths.LocalScratchDirectory, "MultiProcessIn");
            var bamFilePath1 = Stage(sourcePath, "In1", otherTestDirectory + "1");
            var bamFilePath2 = Stage(sourcePath, "In2", otherTestDirectory + "2");

            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath1, bamFilePath2 },
                GenomePaths = new[] { genomePath },
                OutputDirectory = outputFolder,              
                CommandLineArguments = string.Format("-B {0},{1} -g {2}{3} -gVCF false", bamFilePath1, bamFilePath2, genomePath, string.IsNullOrEmpty(outputFolder) ? string.Empty : " -OutFolder " + outputFolder).Split(' '),
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = true
                }
            };

            options.SetIODirectories("Pisces");
            var factory = new Factory(options);
            foreach (var workRequest in factory.WorkRequests)
            {
                if (File.Exists(workRequest.OutputFilePath))
                    File.Delete(workRequest.OutputFilePath);
            }

            Logger.OpenLog(options.LogFolder, options.LogFileName, true);

            var processor = new GenomeProcessor(factory, factory.GetReferenceGenome(options.GenomePaths[0]), false, true);

            processor.Execute(numberOfThreads);

            Logger.CloseLog();

            foreach (var workRequest in factory.WorkRequests)
            {
                using (var reader = new AlleleReader(workRequest.OutputFilePath))
                {
                    Assert.True(reader.HeaderLines.Any());
                    var variants = reader.GetVariants().ToList();

                    Assert.Equal(251, variants.Count());
                    Assert.Equal("chr17", variants.First().Chromosome);
                    Assert.Equal("chr19", variants.Last().Chromosome);
                }
            }

            Assert.True(Directory.GetFiles(options.LogFolder, options.LogFileNameBase).Any());

        }
    }
}
