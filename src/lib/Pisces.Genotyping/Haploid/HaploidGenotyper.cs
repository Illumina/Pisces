using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Genotyping
{
	public class HaploidGenotyper:IGenotypeCalculator
	{

		private PloidyModel _ploidyModel = PloidyModel.DiploidByThresholding; //NOTE: we still use the parameters for diploid mode
		public PloidyModel PloidyModel => _ploidyModel;

		public int MinGQScore { get; set; }
		public int MaxGQScore { get; set; }
		public int MinDepthToGenotype { get; set; }
		public float MinVarFrequency { get; set; }
		public float MinVarFrequencyFilter { get; set; }
		public void SetMinFreqFilter(float minFreqFilter)
		{
			MinVarFrequencyFilter = minFreqFilter > MinVarFrequency ? minFreqFilter : MinVarFrequency;
		}

		private readonly float _minorVF;
		private readonly float _majorVF; 

		public HaploidGenotyper(int minCalledVariantDepth, int minGQscore, int maxGQscore,float minorVF=0.70f,float majorVF = 0.70f)
		{
			MinGQScore = minGQscore;
			MaxGQScore = maxGQscore;
			MinDepthToGenotype = minCalledVariantDepth;
			_minorVF = minorVF;
			_majorVF = majorVF;
			MinVarFrequency = _minorVF;
		}

		public List<CalledAllele> SetGenotypes(IEnumerable<CalledAllele> alleles)
		{
			var allelesToPrune = new List<CalledAllele>();


			var singleGTForLoci = CalculateHaploidGenotype(alleles, MinDepthToGenotype, out allelesToPrune);

			foreach (var allele in alleles)
			{
				allele.Genotype = singleGTForLoci;
				allele.GenotypeQscore = HaploidGenotypeQualityCalculator.Compute(allele, MinGQScore, MaxGQScore);
			}

			return allelesToPrune;
		}





		private  Genotype CalculateHaploidGenotype(IEnumerable<CalledAllele> alleles,
			int minDepthToGenotype, out List<CalledAllele> allelesToPrune)
		{
			allelesToPrune = new List<CalledAllele>();
			var singleGTForLoci = Genotype.HemizygousNoCall;
			var orderedVariants = GenotypeCalculatorUtilities.FilterAndOrderAllelesByFrequency(alleles, allelesToPrune, _minorVF);
			var referenceFrequency = GenotypeCalculatorUtilities.GetReferenceFrequency(alleles, _minorVF);
			var refExists = (referenceFrequency >= _minorVF);
			var depthIssue =GenotypeCalculatorUtilities.CheckForDepthIssue(alleles, minDepthToGenotype);
			bool refCall = ((orderedVariants.Count == 0) || (orderedVariants[0].Frequency < _minorVF));


			if(!depthIssue && refCall && refExists && referenceFrequency > _majorVF)
				singleGTForLoci = Genotype.HemizygousRef;

			if(!depthIssue && !refCall&& !refExists && orderedVariants[0].Frequency >_majorVF)
				singleGTForLoci = Genotype.HemizygousAlt;

			//if (!diploidModelFail)
			allelesToPrune = GenotypeCalculatorUtilities.GetAllelesToPruneBasedOnGTCall(singleGTForLoci, orderedVariants, allelesToPrune);

			return singleGTForLoci;
		}




	}
}
