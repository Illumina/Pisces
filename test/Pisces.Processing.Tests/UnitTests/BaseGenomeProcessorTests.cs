using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.IO;
using Pisces.Processing.Logic;
using Common.IO.Utility;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class BaseGenomeProcessorTests
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
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");
            var bamFilePath2 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19_removedSQlines.bam");
            var vcfFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.vcf");
            var vcfFilePath2 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19_removedSQlines.vcf");
            var genomePath = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");

            var logFile = "CurrentLog_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt";
            var logFullPath = Path.Combine(TestPaths.LocalTestDataDirectory, logFile);

            if (File.Exists(logFullPath))
                File.Delete(logFullPath);

            Logger.OpenLog(TestPaths.LocalTestDataDirectory, logFile, true);
     
            var processor = new TestGenomeProcessor(new List<BamWorkRequest>
            {
                new BamWorkRequest { BamFilePath = bamFilePath, GenomeDirectory = genomePath, OutputFilePath = vcfFilePath},
                new BamWorkRequest { BamFilePath = bamFilePath2, GenomeDirectory = genomePath, OutputFilePath = vcfFilePath2}
            }, new Genome(genomePath, new List<string>() { "chr17", "chr19" }));

            processor.Execute(numberOfThreads);

            Logger.CloseLog();

            var chrCheck = new Dictionary<string, Tuple<int, bool>>();
            chrCheck["chr17"] = new Tuple<int, bool>(0, false);
            chrCheck["chr19"] = new Tuple<int, bool>(0, false);

            var startedChr19 = false;

            using (var reader = new StreamReader(File.OpenRead(logFullPath)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    foreach (var chr in chrCheck.Keys.ToList())
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

    public class TestGenomeProcessor : BaseGenomeProcessor
    {
        public TestGenomeProcessor(List<BamWorkRequest> workRequests, IGenome genome) : base(workRequests, genome)
        {

        }

        protected override void Finish()
        {
            // do nothing
        }

        protected override void Initialize()
        {
            // do nothing
        }

        protected override void Process(BamWorkRequest workRequest, ChrReference chrReference)
        {
            // do nothing
        }
    }
}
