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
        private readonly GenotypeCalculator _genotypeCalculator;
        private readonly ICoverageCalculator _coverageCalculator;

        public int TotalNumCollapsed { get { return _collapser == null ? 0 : _collapser.TotalNumCollapsed; } }
        public int TotalNumCalled { get; private set; }

        public AlleleCaller(VariantCallerConfig config, ChrIntervalSet intervalSet = null, 
            IVariantCollapser variantCollapser = null, ICoverageCalculator coverageCalculator = null)
        {
            _config = config;
            _intervalSet = intervalSet;
            _collapser = variantCollapser;
            _genotypeCalculator = new GenotypeCalculator(_config.GenotypeModel, _config.PloidyModel, 
                _config.MinFrequency, _config.DiploidThresholdingParameters);
            _coverageCalculator = coverageCalculator ?? new CoverageCalculator();
        }

        /// <summary>
        /// Returns a list of called alleles, sorted by position, reference alternate
        /// </summary>
        /// <param name="batchToCall"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public SortedList<int, List<BaseCalledAllele>> Call(ICandidateBatch batchToCall, IAlleleSource source)
        {
            return CallForPositions(batchToCall.GetCandidates(), source, batchToCall.MaxClearedPosition);
        }

        private SortedList<int, List<BaseCalledAllele>> CallForPositions(List<CandidateAllele> candidates, IAlleleSource source, int? maxPosition)
        {
            var calledAllelesByPosition = new SortedList<int, List<BaseCalledAllele>>();
            var failedMnvs = new List<BaseCalledAllele>();
            var callableAlleles = new List<BaseCalledAllele>();

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

            // need to re-process variants since they may have additional support
            foreach (var baseCalledAllele in callableAlleles)
            {
                ProcessVariant(source, baseCalledAllele);
                if (IsCallable(baseCalledAllele) && ShouldReport(baseCalledAllele))
                {
                    List<BaseCalledAllele> calledAtPosition;
                    if (!calledAllelesByPosition.TryGetValue(baseCalledAllele.Coordinate, out calledAtPosition))
                    {
                        calledAtPosition = new List<BaseCalledAllele>();
                        calledAllelesByPosition.Add(baseCalledAllele.Coordinate, calledAtPosition);
                    }

                    calledAtPosition.Add(baseCalledAllele);
                }
            }

            // re-process variants by loci to get GT (to potentially take into account multiple var alleles at same loci)
            // and prune allele lists as needed.
            foreach (var allelesAtPosition in calledAllelesByPosition.Values)
            {
                //pruning ref calls
                if (allelesAtPosition.Any(v => v is CalledVariant))
                    allelesAtPosition.RemoveAll(v => v is CalledReference);

                //prune any variant calls that exceed the ploidy model
                var allelesToPrune = _genotypeCalculator.SetGenotypes(allelesAtPosition);

                foreach (var alleleToPrune in allelesToPrune)
                    allelesAtPosition.Remove(alleleToPrune);
                
                foreach (var allele in allelesAtPosition)
                {
                    if (_config.PloidyModel == PloidyModel.Diploid)
                        allele.GenotypeQscore = GenotypeQualityCalculator.Compute(allele, _config.MinGenotypeQscore, _config.MaxGenotypeQscore);
                    else
                        allele.GenotypeQscore = allele.VariantQscore;
                    
                    if (_config.LowGTqFilter.HasValue && allele.GenotypeQscore < _config.LowGTqFilter)
                        allele.AddFilter(FilterType.LowGenotypeQuality);
                }


                allelesAtPosition.Sort((a1, a2) =>
                          {
                              var refCompare = a1.Reference.CompareTo(a2.Reference);
                              return refCompare == 0 ? a1.Alternate.CompareTo(a2.Alternate) : refCompare;
                          });
            }

            return calledAllelesByPosition;
        }


        public static Dictionary<int, int> GetRefSupportFromGappedMnvs(IEnumerable<BaseCalledAllele> callableAlleles)
        {
            var takenRefCounts = new Dictionary<int, int>();
            foreach (var allele in callableAlleles)
            {
                if (allele.Type != AlleleCategory.Mnv) continue; 

                for (var i = 0; i < allele.Reference.Length; i++)
                {
                    if (allele.Reference[i] != allele.Alternate[i]) continue;

                    var position = allele.Coordinate + i;
                    if (!takenRefCounts.ContainsKey(position))
                    {
                        takenRefCounts[position] = 0;
                    }
                    takenRefCounts[position] += allele.AlleleSupport;
                }
            }
            return takenRefCounts;
        }

        private void ProcessVariant(IAlleleSource source, BaseCalledAllele variant)
        {
            // determine metrics
            _coverageCalculator.Compute(variant, source);

            if (variant.AlleleSupport > 0)
            {
                VariantQualityCalculator.Compute(variant, _config.MaxVariantQscore, _config.EstimatedBaseCallQuality);

                StrandBiasCalculator.Compute(variant, variant.SupportByDirection, _config.EstimatedBaseCallQuality,
                    _config.StrandBiasFilterThreshold, _config.StrandBiasModel);
            }

            // set genotype, filter, etc
            AlleleProcessor.Process(variant, _config.GenotypeModel, _config.MinFrequency, _config.LowDepthFilter,
                _config.VariantQscoreFilterThreshold, _config.FilterSingleStrandVariants, _config.VariantFreqFilter, _config.LowGTqFilter, _config.IndelRepeatFilter, 
                _config.RMxNFilterMaxLengthRepeat, _config.RMxNFilterMinRepetitions, _config.ChrReference);
        }

        private bool IsCallable(BaseCalledAllele allele)
        {
            if (allele is CalledReference)
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

        private bool ShouldReport(BaseCalledAllele allele)
        {
            return _intervalSet == null ? true : _intervalSet.ContainsPosition(allele.Coordinate);
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
        public int EstimatedBaseCallQuality { get; set; }
        public float StrandBiasFilterThreshold { get; set; }
        public bool FilterSingleStrandVariants { get; set; }
        public StrandBiasModel StrandBiasModel { get; set; }
        public GenotypeModel GenotypeModel { get; set; }
        public PloidyModel PloidyModel { get; set; }
        public GenotypeCalculator.DiploidThresholdingParameters DiploidThresholdingParameters { get; set; }
        public float? VariantFreqFilter { get; set; }
        public float? LowGTqFilter { get; set; }
        public int? IndelRepeatFilter { get; set; }
        public int? LowDepthFilter { get; set; }
        public int? RMxNFilterMaxLengthRepeat { get; set; }
        public int? RMxNFilterMinRepetitions { get; set; }
        public ChrReference ChrReference { get; set; }
    }
}
