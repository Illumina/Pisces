using System.Collections.Generic;
using System.IO;
using Pisces.Domain.Types;
using Pisces.Domain.Models.Alleles;
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

            if (File.Exists(outFile))
                File.Delete(outFile);

            Logger.OpenLog(outDir, "RecalibrateDirtyVcfLog.txt", true);

            VQROptions options = new VQROptions
            {
                CommandLineArguments = new string[] { "-vcf", "TestWithArtifacts.vcf" },
                ZFactor = 0,
                MaxQScore = 66,
                //BaseQNoise = 30,
                VcfPath = vcfPath,
                OutputDirectory = outDir
            };

            options.VariantCallingParams.MinimumVariantQScoreFilter = 0;
            options.BamFilterParams.MinimumBaseCallQuality = 30;
            options.VariantCallingParams.AmpliconBiasFilterThreshold = null;

            SignatureSorterResultFiles resultFiles = new SignatureSorterResultFiles(countsPath,"foo.txt", "foo.txt");


            QualityRecalibration.Recalibrate(resultFiles, options);

            Logger.CloseLog();

            Assert.True(File.Exists(outFile));

           TestUtilities.TestHelper.CompareFiles(outFile, expectedFile);

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
            VQROptions options = new VQROptions
            {
                CommandLineArguments = null ,
      //          FilterQScore = -1,
                ZFactor = 0,
                MaxQScore = 66,
      //          BaseQNoise = 30,
                OutputDirectory = outDir
            };

            options.VariantCallingParams.MinimumVariantQScoreFilter = -1;
            options.BamFilterParams.MinimumBaseCallQuality = 30;
            SignatureSorterResultFiles resultFiles = new SignatureSorterResultFiles(countsPath, "foo.txt", "foo.txt");

            QualityRecalibration.Recalibrate(resultFiles, options);

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
            var v = SetUpCalledAllele();
            
            var catalog = new Dictionary<MutationCategory, int>();
            catalog.Add(MutationCategory.CtoA, 12);

            Assert.Equal(666, v.VariantQscore);

            QualityRecalibration.UpdateVariantQScoreAndRefilter(100, 5, catalog, v, MutationCategory.CtoA, false);

            Assert.Equal(12, v.NoiseLevelApplied);
            Assert.Equal(10, v.GenotypeQscore);
            Assert.Equal(10, v.VariantQscore);
            Assert.Equal(0, v.Filters.Count);

            v.VariantQscore = 666;
            QualityRecalibration.UpdateVariantQScoreAndRefilter(100, 30, catalog, v, MutationCategory.CtoA, false);
            Assert.Equal(10, v.VariantQscore);
            Assert.Equal(1, v.Filters.Count);
            Assert.Equal(FilterType.LowVariantQscore, v.Filters[0]);

            v.VariantQscore = 666;
            v.Filters.Add(FilterType.OffTarget);//any filter will do to test, that isnt the q30 one.
            QualityRecalibration.UpdateVariantQScoreAndRefilter(100, 30, catalog, v, MutationCategory.CtoA, false);
            Assert.Equal(10, v.VariantQscore);
            Assert.Equal(2, v.Filters.Count);
            Assert.Equal(FilterType.LowVariantQscore, v.Filters[0]);
            Assert.Equal(FilterType.OffTarget, v.Filters[1]);

            v.VariantQscore = 666;
            v.Filters = new List<FilterType>() { FilterType.LowVariantQscore};
            QualityRecalibration.UpdateVariantQScoreAndRefilter(100, 30, catalog, v, MutationCategory.CtoA, false);
            Assert.Equal(10, v.VariantQscore);
            Assert.Equal(1, v.Filters.Count);
            Assert.Equal(FilterType.LowVariantQscore, v.Filters[0]);

            v.VariantQscore = 666;
            v.Filters = new List<FilterType>() { FilterType.LowVariantQscore , FilterType.OffTarget };
            QualityRecalibration.UpdateVariantQScoreAndRefilter(100, 30, catalog, v, MutationCategory.CtoA, false);
            Assert.Equal(10, v.VariantQscore);
            Assert.Equal(FilterType.LowVariantQscore, v.Filters[0]);
            Assert.Equal(FilterType.OffTarget, v.Filters[1]);

        }

        [Fact]
        public void InsertNewQ()
        {
            var v = SetUpCalledAllele();

            Assert.Equal(20, v.NoiseLevelApplied);
            Assert.Equal(72, v.GenotypeQscore);
            Assert.Equal(666, v.VariantQscore);

            var catalog = new Dictionary<MutationCategory, int>();
            catalog.Add(MutationCategory.CtoA, 12);

            QualityRecalibration.InsertNewQ(catalog, v, MutationCategory.CtoA, 42, true);

            Assert.Equal(12, v.NoiseLevelApplied);
            Assert.Equal(42, v.GenotypeQscore);
            Assert.Equal(42, v.VariantQscore);
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

        private static CalledAllele SetUpCalledAllele()
        {
            var v = new CalledAllele();     
            v .TotalCoverage=100;
            v.ReferenceSupport = 90;
            v.AlleleSupport = 10;
            v.GenotypeQscore = 72;
            v.NoiseLevelApplied = 20;
            v.VariantQscore = 666;
            return v;
        }

        [Fact]
        public void HaveInfoToUpdateQ()
        {
            var v = SetUpCalledAllele(); //(by default, this is reference type)
            double depth;
            double callCount;
            Assert.Equal(true, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));

            v.Type = AlleleCategory.Unsupported;
            Assert.Equal(false, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));

            v.Type = AlleleCategory.Deletion;
            Assert.Equal(true, QualityRecalibration.HaveInfoToUpdateQ(v, out depth, out callCount));
            Assert.Equal(100, depth);
            Assert.Equal(10, callCount);
        }
    }
}
