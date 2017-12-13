using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;

namespace VennVcf
{
	public class ConsensusBuilder
	{
		private VennVcfWriter OutputFile;
        private VennVcfOptions _options;
        private string _consensusFilePath;


		public ConsensusBuilder(string consensusFilePath, VennVcfOptions options)
		{
			_consensusFilePath = consensusFilePath;
            _options = options;
		}

		/// <summary>
		/// Combine two variants.
		/// Variant B can be null.
		/// We never try to combine two different variant alleles.
		/// </summary>
		/// <param name="VariantsA"></param>
		/// <param name="VariantsB"></param>
		/// <param name="ComparisonCase"></param>
		/// <param name="Consensus"></param>
		public AggregateAllele CombineVariants(CalledAllele VariantA, CalledAllele VariantB,
			VariantComparisonCase ComparisonCase)
        {
        

            SampleAggregationParameters SampleAggregationOptions = _options.SampleAggregationParameters;
            var Consensus = new AggregateAllele(new List<CalledAllele> { VariantA, VariantB } );
            int DepthA = 0;
            int DepthB = 0;

            //(A) set the reference data.     
            //this should be the same for both.
            if (VariantA != null)
            {
                DoDefensiveGenotyping(VariantA);
                Consensus.Chromosome = VariantA.Chromosome;
                Consensus.ReferencePosition = VariantA.ReferencePosition;
                Consensus.ReferenceAllele = VariantA.ReferenceAllele;
                DepthA = VariantA.TotalCoverage;
            }
            if (VariantB != null)
            {
                DoDefensiveGenotyping(VariantB);
                Consensus.Chromosome = VariantB.Chromosome;
                Consensus.ReferencePosition = VariantB.ReferencePosition;
                Consensus.ReferenceAllele = VariantB.ReferenceAllele;
                DepthB = VariantB.TotalCoverage;
            }

            //normally the reference data is the same for both, no matter what the case.
            //but if we have one deletion and one not, we might have different ref alleles.
            //So we need to get this right.
            Consensus.ReferenceAllele = CombineReferenceAlleles(VariantA, VariantB, ComparisonCase);

            // (B) set the Alternate data
            Consensus.AlternateAllele = CombineVariantAlleles(VariantA, VariantB, ComparisonCase);

            // (C) set filters, etc.
            Consensus.Filters = CombineFilters(VariantA, VariantB);

            // (E) set GT data, includes calculating the probe-pool bias and quality scores
            RecalculateScoring(VariantA, VariantB, ComparisonCase, Consensus, SampleAggregationOptions, _options.VariantCallingParams);

            return Consensus;
        }

        public static Genotype DoDefensiveGenotyping(CalledAllele Variant)
        {
            //defensive programming until we upgrade consensus to deal with haploid and hemizygous nocalls:
            if ((Variant.Genotype == Genotype.AltAndNoCall) || (Variant.Genotype == Genotype.HemizygousAlt))
                Variant.Genotype = Genotype.HomozygousAlt;

            if ((Variant.Genotype == Genotype.RefAndNoCall) || (Variant.Genotype == Genotype.HemizygousRef))
                Variant.Genotype = Genotype.HomozygousRef;

            if (Variant.Genotype == Genotype.HemizygousNoCall)
                Variant.Genotype = Genotype.RefLikeNoCall;

            return Variant.Genotype;
        }

        #region helper methods for combining VcfVariant class memebers

        private static string CombineReferenceAlleles(CalledAllele VariantA, CalledAllele VariantB, VariantComparisonCase ComparisonCase)
        {
            if ((ComparisonCase == VariantComparisonCase.AgreedOnReference) || (ComparisonCase == VariantComparisonCase.AgreedOnAlternate))
                return VariantA.ReferenceAllele;

          
            if (ComparisonCase == VariantComparisonCase.OneReferenceOneAlternate)
            {
                CalledAllele NonRef = (VariantA.Type == AlleleCategory.Reference) ? VariantB : VariantA;
                return NonRef.ReferenceAllele;

            }

            if ((ComparisonCase == VariantComparisonCase.CanNotCombine))
            {
                //if we have two different alleles, then we shoud only be trying to combine one at once.
                if (VariantB == null)
                    return VariantA.ReferenceAllele;
                else
                    return VariantB.ReferenceAllele;

            }
            else
            {
                throw new ArgumentException("Option not supported");
            }
        }


        private static string CombineVariantAlleles(CalledAllele VariantA, CalledAllele VariantB, VariantComparisonCase ComparisonCase)
		{
            if (ComparisonCase == VariantComparisonCase.AgreedOnReference)
                return ".";

            if (ComparisonCase == VariantComparisonCase.AgreedOnAlternate)
                return VariantA.AlternateAllele;

			if (ComparisonCase == VariantComparisonCase.OneReferenceOneAlternate)
			{
				CalledAllele NonRef =  (VariantA.Type == AlleleCategory.Reference) ? VariantB : VariantA;
                return  NonRef.AlternateAllele;
              
			}

			if ((ComparisonCase == VariantComparisonCase.CanNotCombine))
			{
				//if we have two different alleles, then we shoud only be trying to combine one at once.
				if (VariantB == null)
                    return VariantA.AlternateAllele;
                else
                   return VariantB.AlternateAllele;
                
			}
			else
			{
				throw new ArgumentException("Option not supported");
			}
		}

        private static void RecalculateScoring(CalledAllele VariantA, CalledAllele VariantB,
            VariantComparisonCase Case,
            AggregateAllele ConsensusAllele, SampleAggregationParameters SampleAggregationOptions, VariantCallingParameters variantCallingParameters)
        {


            int RefCountB = 0, RefCountA = 0;
            int AltCountB = 0, AltCountA = 0;
            int DepthA = 0;
            int DepthB = 0;

            //1) first, calculate all the component values (variant frequency, etc...)
            if (VariantA != null)
            {
                RefCountA = VariantA.ReferenceSupport;
                AltCountA = (VariantA.IsRefType) ? 0 : VariantA.AlleleSupport;
                DepthA = VariantA.TotalCoverage;
            }

            if (VariantB != null)
            {
                RefCountB = VariantB.ReferenceSupport;
                AltCountB = (VariantB.IsRefType) ? 0 : VariantB.AlleleSupport;
                DepthB = VariantB.TotalCoverage;
            }


            int TotalDepth = DepthA + DepthB;
            int ReferenceDepth = RefCountA + RefCountB;
            int AltDepth = AltCountA + AltCountB;

            double VarFrequency = ((AltDepth == 0) || (TotalDepth == 0)) ? 0.0 : ((double)AltDepth) / ((double)(TotalDepth));
            double VarFrequencyA = ((AltCountA == 0) || (DepthA == 0)) ? 0.0 : ((double)AltCountA) / ((double)(DepthA));
            double VarFrequencyB = ((AltCountB == 0) || (DepthB == 0)) ? 0.0 : ((double)AltCountB) / ((double)(DepthB));

            ConsensusAllele.TotalCoverage = TotalDepth;
            ConsensusAllele.AlleleSupport = AltDepth;
            ConsensusAllele.ReferenceSupport = ReferenceDepth;

            var GT = GetGenotype(VariantA, VariantB, Case,
                TotalDepth, VarFrequency, VarFrequencyA, VarFrequencyB, SampleAggregationOptions, variantCallingParameters);

            ConsensusAllele.NoiseLevelApplied = GetCombinedNLValue(VariantA, VariantB, SampleAggregationOptions);
            ConsensusAllele.StrandBiasResults = GetCombinedSBValue(VariantA, VariantB, SampleAggregationOptions);

            //its possible the GTString went from var -> ref when we combined the results.
            //If that is the case we do not want to write "variant" anymore to the .vcf.
            //We also have to re-calculate the Q scores for a reference call.
            //They need to be based on a reference model, not a variant model.
            bool AltChangedToRef = PushThroughRamificationsOfGTChange(VariantA, VariantB,
                ConsensusAllele, RefCountA, RefCountB, DepthA, DepthB, GT,
                variantCallingParameters.MaximumVariantQScore, Case);

            ConsensusAllele.Genotype = GT;
            ConsensusAllele.PoolBiasResults = GetProbePoolBiasScore(Case, ConsensusAllele,
                SampleAggregationOptions.ProbePoolBiasThreshold, AltCountA, AltCountB, DepthA, DepthB, GT, AltChangedToRef);

            if (SampleAggregationOptions.HowToCombineQScore == SampleAggregationParameters.CombineQScoreMethod.TakeMin)
            {
                ConsensusAllele.VariantQscore = CombineQualitiesByTakingMinValue(VariantA, VariantB);
            }
            else //VariantCallingCombinePoolSettings.CombineQScoreMethod.CombinePoolsAndReCalculate
            {

                //where we apply the reference Q model:
                if (Case == VariantComparisonCase.AgreedOnReference)
                    ConsensusAllele.VariantQscore = CombineQualitiesByPoolingReads(ReferenceDepth, TotalDepth, ConsensusAllele.NoiseLevelApplied,
                        variantCallingParameters.MaximumVariantQScore);
                else if ((Case == VariantComparisonCase.OneReferenceOneAlternate) && (AltChangedToRef))
                    ConsensusAllele.VariantQscore = CombineQualitiesByPoolingReads(ReferenceDepth, TotalDepth, ConsensusAllele.NoiseLevelApplied,
                        variantCallingParameters.MaximumVariantQScore);
                else if ((Case == VariantComparisonCase.CanNotCombine) && (AltDepth == 0)) //so the only call we had must have been ref
                    ConsensusAllele.VariantQscore = CombineQualitiesByPoolingReads(ReferenceDepth, TotalDepth, ConsensusAllele.NoiseLevelApplied,
                      variantCallingParameters.MaximumVariantQScore);

                //where we apply the variant Q model. this is most cases
                else // cases are aggreed on alt, or one alt call.  in which case, apply variant Q model.
                    ConsensusAllele.VariantQscore = CombineQualitiesByPoolingReads(AltDepth, TotalDepth, ConsensusAllele.NoiseLevelApplied,
                        variantCallingParameters.MaximumVariantQScore);
            }

            //assuming this is only used on Somatic...
            ConsensusAllele.GenotypeQscore = ConsensusAllele.VariantQscore;
            ConsensusAllele.SetType();

            if (ConsensusAllele.IsRefType)
                ConsensusAllele.AlleleSupport = ConsensusAllele.ReferenceSupport;
        }

		private static bool PushThroughRamificationsOfGTChange(CalledAllele VariantA, CalledAllele VariantB,
			CalledAllele Consensus, int RefCountA, int RefCountB, int DepthA, int DepthB,
			Genotype GT, int MaxQScore, VariantComparisonCase Case)
		{

            int NoiseLevel = Consensus.NoiseLevelApplied;
            if (((GT == Genotype.HomozygousRef) || (GT == Genotype.RefLikeNoCall)) && (Case == VariantComparisonCase.OneReferenceOneAlternate))
			{
                Consensus.AlternateAllele = ".";
				Consensus.ReferenceAllele = Consensus.ReferenceAllele.Substring(0, 1);

				if (VariantA != null)
				{
                    VariantA.VariantQscore = VariantQualityCalculator.AssignPoissonQScore(
						RefCountA, DepthA, NoiseLevel, MaxQScore);
				}

				if (VariantB != null)
				{
                    VariantB.VariantQscore = VariantQualityCalculator.AssignPoissonQScore(
						RefCountB, DepthB, NoiseLevel, MaxQScore);
				}

                Consensus.AlleleSupport = Consensus.ReferenceSupport;
				return true;
			}

			return false;
		}

        private static BiasResults GetCombinedSBValue(CalledAllele VariantA, CalledAllele VariantB, SampleAggregationParameters SampleAggregationOptions)
        {
            BiasResults StrandBiasResults = new BiasResults();

            if (VariantA == null)
                return VariantB.StrandBiasResults;

            if (VariantB == null)
                return VariantA.StrandBiasResults;

            StrandBiasResults.GATKBiasScore = Math.Max(VariantA.StrandBiasResults.GATKBiasScore, VariantB.StrandBiasResults.GATKBiasScore);
            return StrandBiasResults;
         }
        private static int GetCombinedNLValue(CalledAllele VariantA, CalledAllele VariantB, SampleAggregationParameters SampleAggregationOptions)
        {
            if (VariantA == null)
                return VariantB.NoiseLevelApplied;

            if (VariantB == null)
                return VariantA.NoiseLevelApplied;

            if (SampleAggregationOptions.HowToCombineQScore == SampleAggregationParameters.CombineQScoreMethod.TakeMin)
                return Math.Min(VariantA.NoiseLevelApplied, VariantB.NoiseLevelApplied);
            else
                return CombineNoiseLevelsByTakingAvgP(VariantA.NoiseLevelApplied, VariantB.NoiseLevelApplied);
        }

		private static Genotype GetGenotype(CalledAllele VariantA, CalledAllele VariantB, VariantComparisonCase Case,
			int TotalDepth, double VarFrequency, double VarFrequencyA, double VarFrequencyB, SampleAggregationParameters SampleAggregationOptions, VariantCallingParameters variantCallingParameters)
		{

			var gtA = Genotype.RefLikeNoCall;
			var gtB = Genotype.RefLikeNoCall;
            var tempGT = Genotype.RefLikeNoCall;

            if (VariantB != null)
                gtB = VariantB.Genotype;
            if (VariantA != null)
                gtA = VariantA.Genotype;

			//cases:  {0/0 , 0/1,  1/1, ./.} , choose 2.

			//if (A == B)  GTString  = A;

			bool RefPresent = ((VariantA != null && VariantA.HasARefAllele) || (VariantB !=null && VariantB.HasARefAllele));
			bool AltPresent = ((VariantA != null && VariantA.HasAnAltAllele) || (VariantB != null && VariantB.HasAnAltAllele));

            if (!AltPresent && RefPresent)
            {
                tempGT = Genotype.HomozygousRef;
            }
            else if (AltPresent && RefPresent)
            {
                tempGT = Genotype.HeterozygousAltRef;
            }
            else if (AltPresent && !RefPresent)
            {
                tempGT = Genotype.HomozygousAlt;

                //todo, expand to cover nocalls and heterozygous calls.
            }
            else //(no alt and no reference detected.)
            {
                tempGT = Genotype.RefLikeNoCall;
            }

			//if its  no call, thats fine. we are done
			if (tempGT == Genotype.RefLikeNoCall)
				return tempGT;

			//if the merged GT implies a variant call,
			//it has to pass some minimal criteria, or it gets
			//re-classified as a ref type or a no-call.
			//So. now, check the combined result passed some minimum criteria:

			//First, never call it a Variant if the combined freq 
			//is smaller than the reporting threshold.
			//If the freq is low, we should call "0/0" or "./.".  , but not "1/1" or "0/1"
			//So change any "0/1"s or "1/1"s over to "./.".

			//if we would have called a variant... but...
			if (Case != VariantComparisonCase.AgreedOnReference)
			{
				// ifcombined freq <1% and both per-pool freq <3% -> 0/0
				// ifcombined freq <1% and a per-pool freq >3% -> ./.
				// ifcombined freq >1% and <3%      -> ./.

				//if combined freq <1%
				if (VarFrequency < variantCallingParameters.MinimumFrequency)
				{
					//if its < 3% in both pools but still <1% overall
					if ((VarFrequencyA < variantCallingParameters.MinimumFrequencyFilter)
						&& (VarFrequencyB < variantCallingParameters.MinimumFrequencyFilter))
					{
                        tempGT = Genotype.HomozygousRef;
                    }
					else //if its > 3% in at least one pool but still <1% overall
					{
						tempGT = Genotype.AltLikeNoCall;
                    }
				}
				else if (VarFrequency < variantCallingParameters.MinimumFrequencyFilter)
                {//if combined freq more than 1% but still < 3%
                    tempGT = Genotype.AltLikeNoCall;
                }

				//next - we have to clean up any multiple allelic sites.
			}
			// also, dont call it a variant *or* a reference 
			// if the combined Depth is less than the minimum.
			// (this case is defensive programing.  The SVC should already call
			// each pool variant as ".\." , due to indiviudal low depth,
			// so the combined results
			// shoud already be ".\." by default. )
			else if (TotalDepth < variantCallingParameters.MinimumCoverage)
			{
                // note, this could happen even though your input variants are one 'no call' and one 'var',
                //or even two variants-failing-filters.
                tempGT = Genotype.RefLikeNoCall;
            }
			return tempGT;
		}


		private static BiasResults GetProbePoolBiasScore(VariantComparisonCase Case, CalledAllele Consensus,
			float ProbePoolBiasThreshold, int AltCountA, int AltCountB, int DepthA, int DepthB, Genotype Genotype, bool AltChangeToRef)
		{
			double ProbePoolPScore = 0; //no bias;
			double ProbePoolGATKBiasScore = -100; //no bias;
            int NoiseLevel = Consensus.NoiseLevelApplied;
            BiasResults PB = new BiasResults();

            if ((AltChangeToRef) || (Case == VariantComparisonCase.AgreedOnReference))
			{
                PB.GATKBiasScore = ProbePoolGATKBiasScore;
                PB.BiasScore = ProbePoolPScore;
                return PB;
            }

			if ((Case == VariantComparisonCase.OneReferenceOneAlternate)
				|| (Case == VariantComparisonCase.CanNotCombine))
			{
				Consensus.Filters.Add(FilterType.PoolBias);
                PB.GATKBiasScore = 0;
                PB.BiasScore = 1;
                return PB;
            }

		    if (Case == VariantComparisonCase.AgreedOnAlternate)
		    {
		        int[] supportByPool = new int[]
		        {
		            AltCountA, AltCountB, 0
		        };
		        int[] covByPool = new int[]
		        {
		            DepthA, DepthB, 0
		        };

                BiasResults ProbePoolBiasResults = 
                    StrandBiasCalculator.CalculateStrandBiasResults(
                    covByPool,supportByPool,
                    NoiseLevel, ProbePoolBiasThreshold, StrandBiasModel.Extended);

				ProbePoolGATKBiasScore = Math.Min(0, ProbePoolBiasResults.GATKBiasScore); //just cap it at upperbound 0, dont go higher.
				ProbePoolGATKBiasScore = Math.Max(-100, ProbePoolGATKBiasScore); //just cap it at lowerbound -100, dont go higher.

				ProbePoolPScore = Math.Min(1, ProbePoolBiasResults.BiasScore);

                if (!ProbePoolBiasResults.BiasAcceptable)
                    Consensus.Filters.Add(FilterType.PoolBias);

			}
		
            PB.GATKBiasScore = ProbePoolGATKBiasScore;
            PB.BiasScore = ProbePoolPScore;
            return PB;
        }

		private static void SetProbePoolBias(VcfVariant Consensus, double Value)
		{
			if (!Consensus.Genotypes[0].ContainsKey("PB"))
			{
				List<string> ConsensusGenotypeTagOrder = new List<string>();
				foreach (string Key in Consensus.Genotypes[0].Keys)
				{
					ConsensusGenotypeTagOrder.Add(Key);
				}
				ConsensusGenotypeTagOrder.Add("PB");
				Consensus.GenotypeTagOrder = ConsensusGenotypeTagOrder.ToArray();
			}

			Consensus.Genotypes[0]["PB"] = Value.ToString("0.0000");
		}

		private static string AddFilter(string OldFilters, string NewFilter)
		{
			if (OldFilters == "PASS") return NewFilter;

			if (OldFilters.Contains(NewFilter)) return OldFilters;

			return string.Format("{0};{1}", OldFilters, NewFilter);
		}

		private static int CombineNoiseLevelsByTakingAvgP(int NL1, int NL2)
		{
			if (NL1 == NL2)
				return NL1;

			double p1 = MathOperations.QtoP(NL1);
			double p2 = MathOperations.QtoP(NL2);
			return ((int)Math.Round(MathOperations.PtoQ((p1 + p2) / 2.0)));

		}

		private static int CombineQualitiesByPoolingReads(int CallCount, int CovDepth, int EstimatedBaseCallQuality, int MaxQScore)
		{
            return VariantQualityCalculator.AssignPoissonQScore(CallCount, CovDepth, EstimatedBaseCallQuality, MaxQScore);
		}

		private static int CombineQualitiesByTakingMinValue(CalledAllele VariantA, CalledAllele VariantB)
		{
			if (VariantB == null)
				return VariantA.VariantQscore;

			if (VariantA == null)
				return VariantB.VariantQscore;

			return Math.Min(VariantA.VariantQscore, VariantB.VariantQscore);
		}

		public static List<FilterType> CombineFilters(CalledAllele VariantA, CalledAllele VariantB)
		{
            return VcfFormatter.MergeFilters(new List<CalledAllele> { VariantA, VariantB }).ToList();
        }

		private static string GetFilterName(string filterLine)
		{
			int position = filterLine.IndexOf("ID=") + 3;
			int position2 = filterLine.IndexOf(",", position);
			return filterLine.Substring(position, position2 - position);
		}

		public void CloseConsensusFile()
		{
			OutputFile.Dispose();
		}

		public void OpenConsensusFile(List<string> originalHeaderLines)
		{
            OutputFile =  new VennVcfWriter(_consensusFilePath,
                new VcfWriterConfig(_options.VariantCallingParams, _options.VcfWritingParams, 
                _options.BamFilterParams, _options.SampleAggregationParameters, false, false),
                new VcfWriterInputContext(), originalHeaderLines, _options.CommandLine, debugMode: _options.DebugMode);

            OutputFile.WriteHeader();     
		}

		public void WriteConsensusVariantsToFile(List<CalledAllele> ConsensusVariants)
		{
			OutputFile.Write(ConsensusVariants);
		}

		#endregion


	}
}
