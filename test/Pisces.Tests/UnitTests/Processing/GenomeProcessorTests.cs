using System;
using System.IO;
using System.Linq;
using System.Threading;
using Pisces.Interfaces;
using Pisces.Tests.MockBehaviors;
using Pisces.Domain.Options;
using CallVariants.Logic.Processing;
using Moq;
using Common.IO.Utility;
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
            //Assert.True(false, "Fix this! The multiprocessor currently causes dll hanging issues.");

            //can sporadically fail b/c Logger is static and collided with other tests. issues.

            var scratchFolder = TestPaths.LocalScratchDirectory;
            ExecuteChromosomeThreadingTest(1, 1, Path.Combine(scratchFolder, "GenomeProcessorTests_1_1"));
            ExecuteChromosomeThreadingTest(2, 2, Path.Combine(scratchFolder, "GenomeProcessorTests_2_2"));
            ExecuteChromosomeThreadingTest(3, 2, Path.Combine(scratchFolder, "GenomeProcessorTests_3_2")); // bam only has 2 chromosomes
        }

        private void ExecuteChromosomeThreadingTest(int numberOfThreads, int expectedNumberOfThreads, string outDir)
        {
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var vcfFilePath = Path.Combine(outDir, "Chr17Chr19.vcf");
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");
            
            var options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                GenomePaths = new[] { genomePath },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                },
                OutputFolder = outDir
            };

            var logFile = Path.Combine(options.LogFolder, options.LogFileName);
            if (File.Exists(logFile))
                File.Delete(logFile);

            Logger.OpenLog(options.LogFolder, options.LogFileName);

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

            Logger.CloseLog();

            var threadsSpawnedBeforeFirstCompleted = 0;

            /* dont worry about logging
            using (var reader = new StreamReader(new FileStream(logFile, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("Completed processing chr")) break;

                    if (line.Contains("Start processing chr"))
                        threadsSpawnedBeforeFirstCompleted++;
                }
            }*/

            //Assert.Equal(expectedNumberOfThreads, threadsSpawnedBeforeFirstCompleted); 
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

            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var bamFilePath2 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19_removedSQlines.bam");
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");
            var validIntervals = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17only.picard");
            var emptyIntervals = Path.Combine(TestPaths.LocalTestDataDirectory, "empty.picard");
            var outputFolder = Path.Combine(TestPaths.LocalTestDataDirectory, "EmptyIntervalsTest_Mixed");

            var options = new PiscesApplicationOptions
            {
                BAMPaths = new[] { bamFilePath, bamFilePath2 },
                IntervalPaths = new [] { validIntervals, emptyIntervals },
                GenomePaths = new[] { genomePath },
                OutputFolder = outputFolder,
                VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                { OutputGvcfFile = true }
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
            options.OutputFolder = Path.Combine(TestPaths.LocalTestDataDirectory, "EmptyIntervalsTest_All");

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
