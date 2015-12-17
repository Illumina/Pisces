using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Utility;
using Moq;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Processing
{
    public class GenomeProcessorTests
    {
        /// <summary>
        /// These tests mock out the variant calling.  See SomaticVariantCaller_MultipleChr for this same scenario but with live calling.
        /// </summary>
        [Fact]
        [Trait("ReqID", "SDS-51")]
        public void Execute()
        {
            ExecuteTest(1, 1);
            ExecuteTest(2, 2);
            ExecuteTest(3, 2); // only 2 bams
        }

        private void ExecuteTest(int numberOfThreads, int expectedNumberOfThreads)
        {
            var bamFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.bam");
            var bamFilePath2 = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35_removedSQlines.bam");
            var vcfFilePath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.vcf");
            var vcfFilePath2 = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35_removedSQlines.vcf");
            var genomePath = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");

            var options = new ApplicationOptions
            {
                BAMPaths = new[] { bamFilePath, bamFilePath2 },
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
            var processor = new GenomeProcessor(factory, factory.GetReferenceGenome(genomePath));

            processor.Execute(numberOfThreads);

            Assert.True(File.Exists(vcfFilePath));
            Assert.True(File.Exists(vcfFilePath2));

            Logger.TryCloseLog();

            var chrCheck = new Dictionary<string, Tuple<int, bool>>();
            chrCheck["chr17"] = new Tuple<int, bool>(0, false);
            chrCheck["chr19"] = new Tuple<int, bool>(0, false);

            var startedChr19 = false;

            using (var reader = new StreamReader(logFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    foreach(var chr in chrCheck.Keys.ToList())
                        if (line.Contains("Start processing chr " + chr))
                        {
                            var chrState = chrCheck[chr];
                            chrCheck[chr] = new Tuple<int, bool>(chrState.Item1 + 1, true);
                        }

                    foreach (var chr in chrCheck.Keys.ToList())
                        if (line.Contains("Completed processing chr " + chr) && chrCheck[chr].Item2)
                        {
                            var chrState = chrCheck[chr];
                            Assert.Equal(expectedNumberOfThreads, chrState.Item1);

                            chrCheck[chr] = new Tuple<int, bool>(0, false);
                        }

                    // make sure chr 17 fully completes before 19 starts
                    if (line.Contains("Processing chromosome 'chr19'")) startedChr19 = true;
                    Assert.False(line.Contains("Processing chromosome 'chr17'") && startedChr19);
                }
            }
        }
    }
}
