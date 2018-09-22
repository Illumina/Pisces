using System.Reflection;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Logic.VariantCalling;
using Xunit;

namespace Pisces.Tests.UnitTests.VariantCalling
{
	public class VariantCallerInChrMXYTests
	{
		[Fact]
		public void ChrMGenotypeCalculatorTests()
		{
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, null, null, 0, 100, 0.05f, 0.02f, "chrM");
			
			Assert.True( genotypeCaculator is SomaticGenotypeCalculator);
			Assert.Equal(0.02f,genotypeCaculator.MinVarFrequency);
			genotypeCaculator.SetMinFreqFilter(0.05f);
			Assert.Equal(0.05f, genotypeCaculator.MinVarFrequencyFilter);

			genotypeCaculator.SetMinFreqFilter(0.01f);
			Assert.Equal(0.02f, genotypeCaculator.MinVarFrequencyFilter);
		}

		[Fact]
		public void ChrXFemaleGenotypeCalculatorTests()
		{
			var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, diploidPars, diploidPars, 0, 100, 0.05f, 0.02f, "chrX",false);

			Assert.True(genotypeCaculator is DiploidGenotypeCalculator);
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
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, diploidPars, diploidPars, 0, 100, 0.05f, 0.02f, "chrX", true);

			Assert.True(genotypeCaculator is HaploidGenotyeCalculator);
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
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, diploidPars, diploidPars, 0, 100, 0.05f, 0.02f, "chrX");

			Assert.True(genotypeCaculator is DiploidGenotypeCalculator);
		}

		[Fact]
		public void ChrYUnknownGenderTests()
		{
			var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, diploidPars, diploidPars, 0, 100, 0.05f, 0.02f, "chrY");

			Assert.True(genotypeCaculator is DiploidGenotypeCalculator);
		}

		[Fact]
		public void ChrYMaleTests()
		{
			var diploidPars = new DiploidThresholdingParameters(new float[] { 0.2f, 0.7f, 0.8f });
			var genotypeCaculator = GenotypeCreator.CreateGenotypeCalculator(PloidyModel.Diploid, 0.05f, 30, diploidPars, diploidPars, 0, 100, 0.05f, 0.02f, "chrY",true);

			Assert.True(genotypeCaculator is HaploidGenotyeCalculator);
		}


	}
}