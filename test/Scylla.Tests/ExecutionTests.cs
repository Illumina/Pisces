using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Common.IO.Utility;
using Xunit;

namespace Scylla.Tests
{
    public class ExecutionTests
    {

        [Fact]
        public void TestExecution()
        {
            var inBam = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.genome.vcf");
            var expVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.out.genome.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "GeneralExecution");
            var outVcf = Path.Combine(outDir, "chr21_11085587_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, };
            CheckIt(inBam, inVcf, expVcf, outVcf, args);

        }

        [Fact]
        public void TestSomaticExecution()
        {
            var inBam = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
            var expSomaticVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.out.somatic.genome.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "SomaticExecution");
            var outVcf = Path.Combine(outDir, "small_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-ploidy", "somatic" };
            CheckIt(inBam, inVcf, expSomaticVcf, outVcf, args);

        }

        [Fact]
        public void TestDiploidExecution()
        {
            var inBam = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
            var expDiploidVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.out.diploid.genome.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "DiploidExecution");
            var outVcf = Path.Combine(outDir, "small_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-crushvcf", "true", "-ploidy", "diploid", };
            CheckIt(inBam, inVcf, expDiploidVcf, outVcf, args);

        }

        private static void CheckIt(string inBam, string inVcf, string expVcf, string outVcf, string[] args)
        {
            args[1] = inBam;
            args[3] = inVcf;

            //Logger.OpenLog(args[5], "ScyllaTestLog.txt", true);

            var application = new Program(args);
            application.Execute();

            Logger.CloseLog();

            var outFileLines = File.ReadAllLines(outVcf);
            var expFileLines = File.ReadAllLines(expVcf);

            Assert.Equal(outFileLines.Length, expFileLines.Length);
            for (int i = 0; i < expFileLines.Length; i++)
            {
                //let command lines differ
                if (expFileLines[i].StartsWith("##Scylla_cmdline"))
                {
                    Assert.True(outFileLines[i].StartsWith("##Scylla_cmdline"));
                    continue;
                }

                //let version numbers differ
                if (expFileLines[i].StartsWith("##VariantPhaser=Scylla"))
                {
                    Assert.True(outFileLines[i].StartsWith("##VariantPhaser=Scylla"));
                    continue;
                }
                Assert.Equal(expFileLines[i], outFileLines[i]);
            }

            if (File.Exists(outVcf))
                File.Delete(outVcf);
        }


    }
}