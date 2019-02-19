using Pisces.Genotyping;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Genotyping.Tests
{
    public class GenotypeCreatorTests
    {

        [Fact]
        public void ChrMGenotypeCalculatorTests()
        {
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, null, null, null, 0, 100, 0.05f, 0.02f, "chrM");

            Assert.True(genotypeCaculator is SomaticGenotyper);
            Assert.Equal(0.02f, genotypeCaculator.MinVarFrequency);
            genotypeCaculator.SetMinFreqFilter(0.05f);
            Assert.Equal(0.05f, genotypeCaculator.MinVarFrequencyFilter);

            genotypeCaculator.SetMinFreqFilter(0.01f);
            Assert.Equal(0.02f, genotypeCaculator.MinVarFrequencyFilter);
        }

        [Fact]
        public void ChrXFemaleGenotypeCalculatorTests()
        {
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, diploidPars, diploidPars, null, 0, 100, 0.05f, 0.02f, "chrX", false);

            Assert.True(genotypeCaculator is DiploidThresholdingGenotyper);
            Assert.Equal(0.2f, genotypeCaculator.MinVarFrequency);
            genotypeCaculator.SetMinFreqFilter(0.05f);
            Assert.Equal(0.2f, genotypeCaculator.MinVarFrequencyFilter);

            genotypeCaculator.SetMinFreqFilter(0.3f);
            Assert.Equal(0.3f, genotypeCaculator.MinVarFrequencyFilter);

        }

        [Fact]
        public void ChrXMaleGenotypeCalculatorTests()
        {
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, diploidPars, diploidPars, null, 0, 100, 0.05f, 0.02f, "chrX", true);

            Assert.True(genotypeCaculator is HaploidGenotyper);
            Assert.Equal(0.2f, genotypeCaculator.MinVarFrequency);
            genotypeCaculator.SetMinFreqFilter(0.05f);
            Assert.Equal(0.2f, genotypeCaculator.MinVarFrequencyFilter);

            genotypeCaculator.SetMinFreqFilter(0.3f);
            Assert.Equal(0.3f, genotypeCaculator.MinVarFrequencyFilter);

        }

        [Fact]
        public void ChrXUnknownGenderTests()
        {
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, diploidPars, diploidPars, null, 0, 100, 0.05f, 0.02f, "chrX");

            Assert.True(genotypeCaculator is DiploidThresholdingGenotyper);

            Assert.Equal(PloidyModel.DiploidByThresholding, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByThresholding, null, "chrX"));

        }

        [Fact]
        public void ChrYUnknownGenderTests_thresholding()
        {
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, diploidPars, diploidPars, null, 0, 100, 0.05f, 0.02f, "chrY");

            Assert.True(genotypeCaculator is DiploidThresholdingGenotyper);

            Assert.Equal(PloidyModel.DiploidByThresholding, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByThresholding, null, "chrY"));

        }

        [Fact]
        public void ChrYMaleTests_thresholding()
        {
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByThresholding, 0.05f, 30, diploidPars, diploidPars, null,
                0, 100, 0.05f, 0.02f, "chrY", true);

            Assert.True(genotypeCaculator is HaploidGenotyper);

            Assert.Equal(PloidyModel.Haploid, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByThresholding, true, "chrY"));

        }

        [Fact]
        public void ChrYUnknownGenderTests_adaptive()
        {
            var adaptiveGTparams = new AdaptiveGenotypingParameters();
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByAdaptiveGT, 0.05f, 30, diploidPars, diploidPars, adaptiveGTparams, 0, 100, 0.05f, 0.02f, "chrY");

            Assert.True(genotypeCaculator is DiploidAdaptiveGenotyper);

            Assert.Equal(PloidyModel.DiploidByAdaptiveGT, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByAdaptiveGT, null, "chrY"));

        }

        [Fact]
        public void ChrYMaleTests_adaptive()
        {
            var adaptiveGTparams = new AdaptiveGenotypingParameters();
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByAdaptiveGT, 0.05f, 30, diploidPars, diploidPars, adaptiveGTparams, 
                0, 100, 0.05f, 0.02f, "chrY", true);

            Assert.True(genotypeCaculator is HaploidGenotyper);

            Assert.Equal(PloidyModel.Haploid, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByAdaptiveGT, true, "chrY"));

        }

        [Fact]
        public void Chr2MaleTests_adaptive()
        {
            var adaptiveGTparams = new AdaptiveGenotypingParameters();
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByAdaptiveGT, 0.05f, 30, diploidPars, diploidPars, adaptiveGTparams,
                0, 100, 0.05f, 0.02f, "chr2", true);

            Assert.True(genotypeCaculator is DiploidAdaptiveGenotyper);

            Assert.Equal(PloidyModel.DiploidByAdaptiveGT, GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByAdaptiveGT, true, "chr2"));
        }


        [Fact]
        public void ChrMMaleTests_adaptive()
        {
            var adaptiveGTparams = new AdaptiveGenotypingParameters();
            var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
            var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.DiploidByAdaptiveGT, 0.05f, 30, diploidPars, diploidPars, adaptiveGTparams,
                0, 100, 0.05f, 0.02f, "chrM", true);

            Assert.True(genotypeCaculator is SomaticGenotyper);

            Assert.Equal(PloidyModel.Somatic,GenotypeCreator.GetPloidyForThisChr(PloidyModel.DiploidByAdaptiveGT, true, "chrM"));
        }
    }
}