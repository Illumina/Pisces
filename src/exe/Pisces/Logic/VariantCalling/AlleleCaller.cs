using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Pisces.Processing.Interfaces;
using CandidateAllele = Pisces.Domain.Models.Alleles.CandidateAllele;

namespace Pisces.Logic.VariantCalling
{

        public class AlleleCaller : IAlleleCaller
    {
        private readonly VariantCallerConfig _config;
        private readonly ChrIntervalSet _intervalSet;
        private readonly IVariantCollapser _collapser;
        private readonly IGenotypeCalculator _genotypeCalculator;
        private readonly ICoverageCalculator _coverageCalculator;
		public HashSet<Tuple<string, int, string, string>> ForcedGtAlleles;

        private readonly ILocusProcessor _locusProcessor;


		public int TotalNumCollapsed { get { return _collapser == null ? 0 : _collapser.TotalNumCollapsed; } }
        public int TotalNumCalled { get; private set; }

        public AlleleCaller(VariantCallerConfig config, ChrIntervalSet intervalSet = null, 
            IVariantCollapser variantCollapser = null, ICoverageCalculator coverageCalculator = null) 
        {
            _config = config;
            _intervalSet = intervalSet;
            _collapser = variantCollapser;
            _coverageCalculator = coverageCalculator ?? new CoverageCalculator();
            _genotypeCalculator = config.GenotypeCalculator;
            _locusProcessor = config.LocusProcessor;
        }

        /// <summary>
        /// Returns a list of called alleles, sorted by position, reference alternate
        /// </summary>
        /// <param name="batchToCall"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public SortedList<int, List<CalledAllele>> Call(ICandidateBatch batchToCall, IAlleleSource source)
        {
	        return CallForPositions(batchToCall.GetCandidates(), source, batchToCall.MaxClearedPosition);
        }

	    public void AddForcedGtAlleles(HashSet<Tuple<string, int, string, string>> forcedGtAlleles)
		{
			ForcedGtAlleles = forcedGtAlleles;
		}

        private SortedList<int, List<CalledAllele>> CallForPositions(List<CandidateAllele> candidates, IAlleleSource source, int? maxPosition)
        {
            var failedMnvs = new List<CalledAllele>();
            var callableAlleles = new List<CalledAllele>();

            if (_collapser != null)
                candidates = _collapser.Collapse(candidates.ToList(), source, maxPosition);

            foreach (var candidate in candidates)
            {
                var variant = AlleleHelper.Map(candidate);

                if (variant.Type == AlleleCategory.Mnv)
                {
                    ProcessVariant(source, variant);
                    if (IsCallable(variant))
                    {
                        callableAlleles.Add(variant);
                    }
                    else
                    {
                        failedMnvs.Add(variant);
                    }
                }

                else
                {
                    callableAlleles.Add(variant);
                }
            }

            var leftoversInNextBlock = MnvReallocator.ReallocateFailedMnvs(failedMnvs, callableAlleles, maxPosition);
            source.AddCandidates(leftoversInNextBlock.Select(AlleleHelper.Map));

            source.AddGappedMnvRefCount(GetRefSupportFromGappedMnvs(callableAlleles));

            var calledAllelesByPosition = new SortedList<int, List<CalledAllele>>(); //

            // need to re-process variants since they may have additional support
            foreach (var baseCalledAllele in callableAlleles)
            {
                ProcessVariant(source, baseCalledAllele);
                if (IsForcedAllele(baseCalledAllele) && !(IsCallable(baseCalledAllele) && ShouldReport(baseCalledAllele)))
                {
                    baseCalledAllele.IsForcedToReport = true;
                    baseCalledAllele.AddFilter(FilterType.ForcedReport);
                }

                if ((IsCallable(baseCalledAllele) && ShouldReport(baseCalledAllele)) || IsForcedAllele(baseCalledAllele))
                {

                    List<CalledAllele> calledAtPosition;
                    if (!calledAllelesByPosition.TryGetValue(baseCalledAllele.ReferencePosition, out calledAtPosition))
                    {
                        calledAtPosition = new List<CalledAllele>();
                        calledAllelesByPosition.Add(baseCalledAllele.ReferencePosition, calledAtPosition);
                    }

                    calledAtPosition.Add(baseCalledAllele);
                }
            }

            // re-process variants by loci to get GT (to potentially take into account multiple var alleles at same loci)
            // and prune allele lists as needed.
            foreach (var allelesAtPosition in calledAllelesByPosition.Values)
            {
                ComputeGenotypeAndFilterAllele(allelesAtPosition);
                _locusProcessor.Process(allelesAtPosition);
            }

            return calledAllelesByPosition;
        }

        private void ComputeGenotypeAndFilterAllele(List<CalledAllele> allelesAtPosition)
        {
            //pruning ref calls
            if (allelesAtPosition.Any(v => v.Type != AlleleCategory.Reference && !v.IsForcedToReport))//(v => v is BaseCalledAllele))
                allelesAtPosition.RemoveAll(v => (v.Type == AlleleCategory.Reference));

            //set GT and GT score, and prune any variant calls that exceed the ploidy model
            var allelesToPrune = _genotypeCalculator.SetGenotypes(allelesAtPosition.Where(x => !x.IsForcedToReport).ToList());



            foreach (var alleleToPrune in allelesToPrune)
            {
                var allele = new Tuple<string, int, string, string>(alleleToPrune.Chromosome,
                 alleleToPrune.ReferencePosition, alleleToPrune.ReferenceAllele, alleleToPrune.AlternateAllele);


                if (ForcedGtAlleles == null || !ForcedGtAlleles.Contains(allele))
                    allelesAtPosition.Remove(alleleToPrune);
            }


            foreach (var allele in allelesAtPosition)
            {
                if (_config.LowGTqFilter.HasValue && allele.GenotypeQscore < _config.LowGTqFilter)
                    allele.AddFilter(FilterType.LowGenotypeQuality);
            }


            allelesAtPosition.Sort((a1, a2) =>
            {
                var refCompare = a1.ReferenceAllele.CompareTo(a2.ReferenceAllele);
                return refCompare == 0 ? a1.AlternateAllele.CompareTo(a2.AlternateAllele) : refCompare;
            });
        }

        private bool IsForcedAllele(CalledAllele baseCalledAllele)
	        {
		        if (ForcedGtAlleles == null) return false;
		        var allele = new Tuple<string,int,string,string>(baseCalledAllele.Chromosome,baseCalledAllele.ReferencePosition,baseCalledAllele.ReferenceAllele,baseCalledAllele.AlternateAllele);
		        return ForcedGtAlleles.Contains(allele);
	        }

	        public static Dictionary<int, int> GetRefSupportFromGappedMnvs(IEnumerable<CalledAllele> callableAlleles)
        {
            var takenRefCounts = new Dictionary<int, int>();
            foreach (var allele in callableAlleles)
            {
                if (allele.Type != AlleleCategory.Mnv) continue; 

                for (var i = 0; i < allele.ReferenceAllele.Length; i++)
                {
                    if (allele.ReferenceAllele[i] != allele.AlternateAllele[i]) continue;

                    var position = allele.ReferencePosition + i;
                    if (!takenRefCounts.ContainsKey(position))
                    {
                        takenRefCounts[position] = 0;
                    }
                    takenRefCounts[position] += allele.AlleleSupport;
                }
            }
            return takenRefCounts;
        }

        private void ProcessVariant(IAlleleSource source, CalledAllele variant)
        {
            // determine metrics
            _coverageCalculator.Compute(variant, source);

            if (variant.AlleleSupport > 0)
            {
				if (_config.NoiseModel == NoiseModel.Window)
				{
					VariantQualityCalculator.Compute(variant, _config.MaxVariantQscore, (int)MathOperations.PtoQ(variant.SumOfBaseQuality / variant.TotalCoverage));
				}
				else
				{
					VariantQualityCalculator.Compute(variant, _config.MaxVariantQscore, _config.NoiseLevelUsedForQScoring);
				}

				StrandBiasCalculator.Compute(variant, variant.SupportByDirection, _config.NoiseLevelUsedForQScoring,
                    _config.StrandBiasFilterThreshold, _config.StrandBiasModel);
            }

            // set genotype, filter, etc
            AlleleProcessor.Process(variant, _config.MinFrequency, _config.LowDepthFilter,
                _config.VariantQscoreFilterThreshold, _config.FilterSingleStrandVariants, _config.VariantFreqFilter, _config.LowGTqFilter, _config.IndelRepeatFilter, 
                _config.RMxNFilterSettings, _config.ChrReference, source.ExpectStitchedReads);
        }

        private bool IsCallable(CalledAllele allele)
        {
            if (allele.Type== AlleleCategory.Reference)
                // reference calls always get emitted
                // intervals have already been applied to ref calls - performance improvement not to reapply
            {
                TotalNumCalled++;
                return true;
            }

            // determine if we should discard variant
            if (allele.TotalCoverage < _config.MinCoverage && !_config.IncludeReferenceCalls)
                return false; // if gvcf, call but filter later

            if (allele.TotalCoverage != 0 && allele.Frequency < _config.MinFrequency) 
                return false; 

            if (allele.VariantQscore < _config.MinVariantQscore)
                return false;

            TotalNumCalled ++;
            return true;
        }

        private bool ShouldReport(CalledAllele allele)
        {
            return _intervalSet == null ? true : _intervalSet.ContainsPosition(allele.ReferencePosition);
        }
    }

    public class VariantCallerConfig
    {
        public bool IncludeReferenceCalls { get; set; }
        public int MinCoverage { get; set; }
        public float MinFrequency { get; set; }
        public int MaxGenotypeQscore { get; set; }
        public int MinGenotypeQscore { get; set; }
        public int MaxVariantQscore { get; set; }
        public int MinVariantQscore { get; set; }
        public int? VariantQscoreFilterThreshold { get; set; }
        public int NoiseLevelUsedForQScoring { get; set; }
        public float StrandBiasFilterThreshold { get; set; }
        public bool FilterSingleStrandVariants { get; set; }
        public StrandBiasModel StrandBiasModel { get; set; }
        public IGenotypeCalculator GenotypeCalculator { get; set; }
        public ILocusProcessor LocusProcessor { get; set; }
        public float? VariantFreqFilter { get; set; }
        public float? LowGTqFilter { get; set; }
        public int? IndelRepeatFilter { get; set; }
        public int? LowDepthFilter { get; set; }
        public RMxNFilterSettings RMxNFilterSettings { get; set; }
        public ChrReference ChrReference { get; set; }
		public NoiseModel NoiseModel { get; set; }
    }

}
