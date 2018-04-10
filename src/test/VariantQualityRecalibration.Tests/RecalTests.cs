using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using Common.IO.Utility;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class RecalTests
    {

        [Fact]
        public void RecalibrateDirtyVcf()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "TestWithArtifacts.vcf");
            var countsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Dirty.counts");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "RecalibrateDirtyVcf");
            var outFile = Path.Combine(outDir, "TestWithArtifacts.vcf.recal");
            var expectedFile = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedDirty.vcf.recal");
            var quotedCmdLineString = "\"-vcf TestWithArtifacts.vcf\"";

            if (File.Exists(outFile))
                File.Delete(outFile);

            Logger.OpenLog(outDir, "RecalibrateDirtyVcfLog.txt", true);
            
            QualityRecalibration.Recalibrate(vcfPath, countsPath, outDir, 30, 0, 66, -1, quotedCmdLineString);

            Logger.CloseLog();

            Assert.True(File.Exists(outFile));

            var observedLines = File.ReadAllLines(outFile);
            var expectedLines = File.ReadAllLines(expectedFile);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
                Assert.Equal(expectedLines[i], observedLines[i]);

            //redirect log incase any other thread is logging here.
            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt", true);
            Logger.CloseLog();

            //delete our log
            File.Delete(outFile);
        }


        [Fact]
        public void RecalibrateCleanVcf()
        {
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Test.vcf");
            var countsPath = Path.Combine(TestPaths.LocalTestDataDirectory, "Clean.counts");
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "RecalibrateDirtyVcf");
            var outFile = Path.Combine(outDir, "Test.vcf.recal");
            var expectedFile = Path.Combine(TestPaths.LocalTestDataDirectory, "ExpectedTest.vcf.recal");

            if (File.Exists(outFile))
                File.Delete(outFile);

            Logger.OpenLog(TestPaths.LocalScratchDirectory, "RecalibrateCleanVcfLog.txt", true);

            QualityRecalibration.Recalibrate(vcfPath, countsPath, outDir, 30, 0,66, -1,null);

            Logger.CloseLog();

           //no recalibration should occur for a clean file.
            Assert.True(!File.Exists(outFile));

            //redirect log incase any other thread is logging here.
            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt", true);
            Logger.CloseLog();
        }

        [Fact]
        public void UpdateVariant()
        {
            var v = SetUpVariant();
            var catalog = new Dictionary<MutationCategory, int>();
            catalog.Add(MutationCategory.CtoA, 12);

            Assert.Equal(666, v.Quality);

            QualityRecalibration.UpdateVariant(100, 5, catalog, v, MutationCategory.CtoA);

            Assert.Equal("12", v.Genotypes[0]["NL"]);
            Assert.Equal("10", v.Genotypes[0]["GQ"]);
            Assert.Equal("10", v.Genotypes[0]["GQX"]);
            Assert.Equal(10, v.Quality);
            Assert.Equal("PASS", v.Filters);

            v.Quality = 666;
            QualityRecalibration.UpdateVariant(100, 30, catalog, v, MutationCategory.CtoA);
            Assert.Equal(10, v.Quality);
            Assert.Equal("q30", v.Filters);

            v.Quality = 666;
            v.Filters = "Snoopy";
            QualityRecalibration.UpdateVariant(100, 30, catalog, v, MutationCategory.CtoA);
            Assert.Equal(10, v.Quality);
            Assert.Equal("Snoopy;q30", v.Filters);

            v.Quality = 666;
            v.Filters = "q30";
            QualityRecalibration.UpdateVariant(100, 30, catalog, v, MutationCategory.CtoA);
            Assert.Equal(10, v.Quality);
            Assert.Equal("q30", v.Filters);

            v.Quality = 666;
            v.Filters = "Snoopy;q30";
            QualityRecalibration.UpdateVariant(100, 30, catalog, v, MutationCategory.CtoA);
            Assert.Equal(10, v.Quality);
            Assert.Equal("Snoopy;q30", v.Filters);
        
        }

        [Fact]
        public void InsertNewQ()
        {
            var v = SetUpVariant();

            Assert.Equal("20", v.Genotypes[0]["NL"]);
            Assert.Equal("72", v.Genotypes[0]["GQ"]);
            Assert.Equal("34", v.Genotypes[0]["GQX"]);
            Assert.Equal(666, v.Quality);


            var catalog = new Dictionary<MutationCategory, int>();
            catalog.Add(MutationCategory.CtoA, 12);

            QualityRecalibration.InsertNewQ(catalog, v, MutationCategory.CtoA, 42);

            Assert.Equal("12", v.Genotypes[0]["NL"]);
            Assert.Equal("42", v.Genotypes[0]["GQ"]);
            Assert.Equal("42", v.Genotypes[0]["GQX"]);
            Assert.Equal(42, v.Quality);
        }

        private static VcfVariant SetUpVariant()
        {
            var v = new VcfVariant();

            v.Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() };
            v.InfoFields = new Dictionary<string, string>();
            v.Filters = "PASS";
            v.InfoFields.Add("DP", "100");
            v.Genotypes[0].Add("AD", "90,10");
            v.Genotypes[0].Add("GQ", "72");
            v.Genotypes[0].Add("GQX", "34");
            v.Genotypes[0].Add("NL", "20");
            v.Quality = 666;

            return v;
        }

        [Fact]
        public void HaveInfoToUpdateQ()
        {
            var v = new VcfVariant();
            int depth;
            int callCount;
            Assert.Equal(false, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));

            v.Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() };
            v.InfoFields = new Dictionary<string,string>();

            v.InfoFields.Add("PX", "42");
            v.Genotypes[0].Add("blah", "??");
            Assert.Equal(false, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));
           
            v.InfoFields.Add("DP", "100");
            v.Genotypes[0].Add("AD", "90,10");
            Assert.Equal(true, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));
            Assert.Equal(100, depth);
            Assert.Equal(10, callCount);
        }
    }
}
