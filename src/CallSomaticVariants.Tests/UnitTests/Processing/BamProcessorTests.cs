using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Utility;
using Moq;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Processing
{
    public class BamProcessorTests
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
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.bam");
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.vcf");
            var genomePath = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var options = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath },
                GenomePaths = new[] { genomePath },
            };

            var logFile = Path.Combine(options.LogFolder, ApplicationOptions.LogFileName);
            if (File.Exists(logFile))
                File.Delete(logFile);

            Logger.TryOpenLog(options.LogFolder, ApplicationOptions.LogFileName);

            var factory = new MockFactoryWithDefaults(options);
            factory.MockSomaticVariantCaller = new Mock<ISomaticVariantCaller>();
            factory.MockSomaticVariantCaller.Setup(s => s.Execute()).Callback(() =>
            {
                Thread.Sleep(500);
            });
            var processor = new BamProcessor(factory, factory.GetReferenceGenome(genomePath));

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
    }
}
