using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TestUtilities;
using Xunit;

namespace Scylla.Tests
{
    public class ExecutionTests
    {

        [Fact]
        public void TestExecution()
        {
            var inBam = Path.Combine(UnitTestPaths.TestDataDirectory, "chr21_11085587_S1.bam");
            var inVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "chr21_11085587_S1.genome.vcf");
            var expVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "chr21_11085587_S1.out.genome.vcf");
            var outVcf = Path.Combine(UnitTestPaths.WorkingDirectory, "chr21_11085587_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", "myOut", };
            CheckIt(inBam, inVcf, expVcf, outVcf, args);

        }

        [Fact]
        public void TestSomaticExecution()
        {
            var inBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.bam");
            var inVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.genome.vcf");
            var expSomaticVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.out.somatic.genome.vcf");
            var outVcf = Path.Combine(UnitTestPaths.WorkingDirectory, "small_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", "myOut", "-ploidy", "somatic"};
            CheckIt(inBam, inVcf, expSomaticVcf, outVcf, args);

        }

        [Fact]
        public void TestDiploidExecution()
        {
            var inBam = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.bam");
            var inVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.genome.vcf");
            var expDiploidVcf = Path.Combine(UnitTestPaths.TestDataDirectory, "small_S1.out.diploid.genome.vcf");
            var outVcf = Path.Combine(UnitTestPaths.WorkingDirectory, "small_S1.phased.genome.vcf");

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", "myOut", "-crushvcf", "true", "-ploidy", "diploid", };
            CheckIt(inBam, inVcf, expDiploidVcf, outVcf, args);

        }

        private static void CheckIt(string inBam, string inVcf, string expVcf, string outVcf, string[] args)
        {
            args[1] = inBam;
            args[3] = inVcf;
            args[5] = UnitTestPaths.WorkingDirectory;

            var application = new Program(args);
            application.Execute();

            var outFileLines = File.ReadAllLines(outVcf);
            var expFileLines = File.ReadAllLines(expVcf);

            Assert.Equal(outFileLines.Length, expFileLines.Length);
            for (int i = 0; i < expFileLines.Length; i++)
            {
	            if (expFileLines[i].StartsWith("##Scylla_cmdline"))
	            {
		            Assert.True(outFileLines[i].StartsWith("##Scylla_cmdline"));
					continue;
	            }

                if (expFileLines[i].StartsWith("##VariantPhaser="))
                {
                    Assert.True(outFileLines[i].StartsWith("##VariantPhaser="));
                    continue;
                }

                Assert.Equal(expFileLines[i], outFileLines[i]);
            }

            if (File.Exists(outVcf))
                File.Delete(outVcf);
        }

    
    }
}