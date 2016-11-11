using System.IO;
using System.Linq;
using System.Threading;
using Pisces.Interfaces;
using Pisces.Tests.MockBehaviors;
using CallVariants.Logic.Processing;
using Moq;
using TestUtilities;
using Pisces.Processing.Utility;
using Pisces.IO.Sequencing;
using Xunit;

namespace Pisces.Tests.UnitTests.Processing
{
    public class GenomeProcessorTests
    {
        /// <summary>
        /// These tests mock out the variant calling.  See SomaticVariantCaller_MultipleChr for this same scenario but with live calling.
        /// </summary>

        [Fact]
        [Trait("ReqID", "SDS-52")]
        public void Execute()
        {
            ExecuteChromosomeThreadingTest(1, 1);
            ExecuteChromosomeThreadingTest(2, 2);
            ExecuteChromosomeThreadingTest(3, 2); // bam only has 2 chromosomes
        }

        private void ExecuteChromosomeThreadingTest(int numberOfThreads, int expectedNumberOfThreads)
        {
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19.bam");
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19.vcf");
            var genomePath = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var options = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                GenomePaths = new[] { genomePath },
            };

            var logFile = Path.Combine(options.LogFolder, options.LogFileName);
            if (File.Exists(logFile))
                File.Delete(logFile);

            Logger.TryOpenLog(options.LogFolder, options.LogFileName);

            var factory = new MockFactoryWithDefaults(options);
            factory.MockSomaticVariantCaller = new Mock<ISomaticVariantCaller>();
            factory.MockSomaticVariantCaller.Setup(s => s.Execute()).Callback(() =>
            {
                Thread.Sleep(500);
            });
            var processor = new GenomeProcessor(factory, factory.GetReferenceGenome(genomePath), false);

            processor.Execute(numberOfThreads);

            Assert.False(File.Exists(vcfFilePath + "_chr17"));
            Assert.False(File.Exists(vcfFilePath + "_chr19"));
            Assert.True(File.Exists(vcfFilePath));

            Logger.TryCloseLog();

            var threadsSpawnedBeforeFirstCompleted = 0;

            using (var reader = new StreamReader(logFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("Completed processing chr")) break;

                    if (line.Contains("Start processing chr"))
                        threadsSpawnedBeforeFirstCompleted++;
                }
            }

            Assert.Equal(expectedNumberOfThreads, threadsSpawnedBeforeFirstCompleted); 
        }

        [Fact]
        public void EmptyIntervals()
        {
            ExecuteEmptyIntervalsTest(false);
            ExecuteEmptyIntervalsTest(true);
        }

        private void ExecuteEmptyIntervalsTest(bool throttle)
        {
            // ----------------------
            // test when one bam has intervals and the other is empty
            // ----------------------

            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19.bam");
            var bamFilePath2 = Path.Combine(UnitTestPaths.TestDataDirectory, "Chr17Chr19_removedSQlines.bam");
            var genomePath = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");
            var validIntervals = Path.Combine(UnitTestPaths.TestDataDirectory, "chr17only.picard");
            var emptyIntervals = Path.Combine(UnitTestPaths.TestDataDirectory, "empty.picard");
            var outputFolder = Path.Combine(UnitTestPaths.TestDataDirectory, "EmptyIntervalsTest_Mixed");

            var options = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath, bamFilePath2 },
                IntervalPaths = new [] { validIntervals, emptyIntervals },
                GenomePaths = new[] { genomePath },
                OutputFolder = outputFolder,
                OutputgVCFFiles = true
            };

            var factory = new Factory(options);
            var processor = new GenomeProcessor(factory, factory.GetReferenceGenome(genomePath), throttle);

            processor.Execute(2);

            // first vcf file should have been processed regularly
            using (var reader = new VcfReader(factory.WorkRequests.First().OutputFilePath))
            {
                var variants = reader.GetVariants();
                Assert.Equal(11, variants.Count());
            }

            // second vcf file should be empty
            using (var reader = new VcfReader(factory.WorkRequests.Last().OutputFilePath))
            {
                var variants = reader.GetVariants();
                Assert.Equal(0, variants.Count());
            }

            // ----------------------
            // try again but with both bams using empty intervals
            // ----------------------

            options.IntervalPaths = new[] {emptyIntervals};
            options.OutputFolder = Path.Combine(UnitTestPaths.TestDataDirectory, "EmptyIntervalsTest_All");

            factory = new Factory(options);
            processor = new GenomeProcessor(factory, factory.GetReferenceGenome(genomePath), throttle);

            processor.Execute(2);

            foreach (var workRequest in factory.WorkRequests)
            {
                // both vcf file should be empty
                using (var reader = new VcfReader(workRequest.OutputFilePath))
                {
                    var variants = reader.GetVariants();
                    Assert.Equal(0, variants.Count());
                }
            }
        }
    }
}
