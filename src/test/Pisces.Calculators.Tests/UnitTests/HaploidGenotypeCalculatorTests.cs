using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Calculators.Tests
{
	public class HaploidGenotypeCalculatorTests
	{
		private float _minorVf = 0.20f;
		private float _majorVf = 0.70f;

		private int _minCalledVariantDepth = 100;
		private int _minGQscore = 0;
		private int _maxGQscore = 100;

		private void ExecuteHaploidGenotypeTest(
			Genotype expectedGenotype, int expectedNumAllelesToPrune,
			float? refFrequency, List<float> altFrequencies, List<FilterType> filters, int coverage)
		{
			var alleles = new List<CalledAllele>();
			

			if (refFrequency != null)
			{
				var variant = TestHelper.CreatePassingVariant(true);
				variant.AlleleSupport = (int)(refFrequency * coverage);
				variant.TotalCoverage = (int)coverage;
				variant.ReferenceSupport = variant.AlleleSupport;
				alleles.Add(variant);
			}


			var refFreq = refFrequency ?? 1.0 - altFrequencies.Sum();
			

			foreach (float vf in altFrequencies)
			{
				var variant = TestHelper.CreatePassingVariant(false);
				variant.AlleleSupport = (int) (vf*coverage);
				variant.TotalCoverage = (int) coverage;
				variant.ReferenceSupport = (int) (refFreq*coverage);
				alleles.Add(variant);
			}

			//set filters for at least one allele. they should affect all results.
			alleles[0].Filters = filters;

			var GTC = new HaploidGenotyeCalculator(_minCalledVariantDepth,_minGQscore,_maxGQscore,_minorVf,_majorVf);

			var allelesToPrune = GTC.SetGenotypes(alleles);

			Assert.Equal(expectedNumAllelesToPrune, allelesToPrune.Count);
			foreach (var allele in alleles)
			{
				Assert.Equal(expectedGenotype, allele.Genotype);
			}
		}


		[Fact]
		public void HemizygousRefTest()
		{
			ExecuteHaploidGenotypeTest(Genotype.HemizygousRef, 2, 0.80f, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
		}

		[Fact]
		public void NoCallDueToRefMajorVf()
		{
			ExecuteHaploidGenotypeTest(Genotype.HemizygousNoCall, 2, 0.70f, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
		}

		[Fact]
		public void NoCallDueToRefMinorVf()
		{
			ExecuteHaploidGenotypeTest(Genotype.HemizygousNoCall, 2, 0.22f, new List<float> { 0.75f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);
		}

		[Fact]
		public void NoCallDueToCoverge()
		{
			ExecuteHaploidGenotypeTest(Genotype.HemizygousNoCall, 2, 0.80f, new List<float> { 0.01f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 10);
		}

		[Fact]
		public void HemizygousAlt()
		{
			ExecuteHaploidGenotypeTest(Genotype.HemizygousAlt, 1, 0.10f, new List<float> { 0.75f, 0.01f }, new List<FilterType> { FilterType.LowDepth }, 1000);

		}

	}


}