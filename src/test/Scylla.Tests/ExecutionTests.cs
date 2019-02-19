using System.IO;
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

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-ncfilter", "1" };
            CheckIt(inBam, inVcf, expVcf, outVcf, args);

        }

        [Fact]
        public void TestSomaticExecution()
        {
            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "small_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
            var expSomaticVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.out.somatic.genome.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "SomaticExecution");
            var outVcf = Path.Combine(outDir, "small_S1.phased.genome.vcf");

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-ploidy", "somatic", "-ncfilter", "1" };
            CheckIt(inBam, inVcf, expSomaticVcf, outVcf, args);

        }

        [Fact]
        public void TestDiploidExecution()
        {
            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "small_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
            var expDiploidVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.out.diploid.genome.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "DiploidExecution");
            var outVcf = Path.Combine(outDir, "small_S1.phased.genome.vcf");

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-crushvcf", "true",
                "-ploidy", "diploid", "-diploidINDELgenotypeparameters", "0.20,0.70,0.80",
                "-diploidSNVgenotypeparameters", "0.20,0.70,0.80", "-ncfilter", "1" };
            CheckIt(inBam, inVcf, expDiploidVcf, outVcf, args);

        }


        [Fact]
        public void TestSomaticOnBugNoGenomeExecution()
        {
            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.vcf");
            var inGenome = Path.Combine(TestPaths.SharedGenomesDirectory, "Bacillus_cereus", "Sequence", "WholeGenomeFasta");
            var expectedPhasedVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.out.Rs.phased.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "BugNoGenomeExecution");
            var outVcf = Path.Combine(outDir, "Bcereus_S4.phased.vcf");

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir};
            CheckIt(inBam, inVcf, expectedPhasedVcf, outVcf, args);
            
        }

        [Fact]
        public void TestSomaticOnBugWithGenomeExecution()
        {

            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.vcf");
            var inGenome = Path.Combine(TestPaths.SharedGenomesDirectory, "Bacillus_cereus", "Sequence", "WholeGenomeFasta");
            var expectedPhasedVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.out.phased.vcf");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "BugWithGenomeExecution");
            var outVcf = Path.Combine(outDir, "Bcereus_S4.phased.vcf");

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            string[] args = new string[] { "-bam", "myBam", "-vcf", "myVcf", "-out", outDir, "-g", inGenome };
            CheckIt(inBam, inVcf, expectedPhasedVcf, outVcf, args);
        }

        private static void CheckIt(string inBam, string inVcf, string expVcf, string outVcf, string[] args)
        {
            args[1] = inBam;
            args[3] = inVcf;
           
            Program.Main(args);

            TestUtilities.TestHelper.CompareFiles(outVcf, expVcf);

            if (File.Exists(outVcf))
                File.Delete(outVcf);
        }


    }
}