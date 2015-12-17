using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Calculators;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using CoverageCalculator = CallSomaticVariants.Logic.Calculators.CoverageCalculator;

namespace CallSomaticVariants.Logic.VariantCalling
{
    public class AlleleCaller : IAlleleCaller
    {
        private readonly VariantCallerConfig _config;
        private readonly ChrIntervalSet _intervalSet;

        public AlleleCaller(VariantCallerConfig config, ChrIntervalSet intervalSet = null)
        {
            _config = config;
            _intervalSet = intervalSet;
        }

        public IEnumerable<BaseCalledAllele> Call(ICandidateBatch batchToCall, IStateManager source)
        {
            return CallForPositions(batchToCall.GetCandidates(), source, batchToCall.MaxClearedPosition);
        }

        private IEnumerable<BaseCalledAllele> CallForPositions(IEnumerable<CandidateAllele> candidates, IStateManager source, int? maxPosition)
        {
            var calledAlleles = new List<BaseCalledAllele>();
            var failedMnvs = new List<BaseCalledAllele>();
            var callableAlleles = new List<BaseCalledAllele>();

            foreach (var candidate in candidates)
            {
                var variant = Map(candidate);

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
            source.AddCandidates(leftoversInNextBlock.Select(Map));

            source.AddGappedMnvRefCount(GetRefSupportFromGappedMnvs(callableAlleles));

            // need to re-process variants since they may have additional support
            foreach (var baseCalledAllele in callableAlleles)
            {
                ProcessVariant(source,baseCalledAllele);
                if (IsCallable(baseCalledAllele) && ShouldReport(baseCalledAllele))
                    calledAlleles.Add(baseCalledAllele);
            }

            // prune reference call if variants were found
            var allPositions = calledAlleles.Select(c => c.Coordinate).Distinct().ToList();
            foreach (var position in allPositions)
            {
                var allelesAtPosition = calledAlleles.Where(x => x.Coordinate == position);
                var referenceCall = allelesAtPosition.FirstOrDefault(v => v is CalledReference);
                if (referenceCall != null && allelesAtPosition.Any(v => v is CalledVariant))
                    calledAlleles.Remove(referenceCall);                
            }

            return calledAlleles;
        }

        private bool ShouldReport(BaseCalledAllele allele)
        {
            return _intervalSet == null || _intervalSet.Intervals.Any(i => i.ContainsPosition(allele.Coordinate));
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

        private void ProcessVariant(IStateManager source, BaseCalledAllele variant)
        {
            // determine metrics
            CoverageCalculator.Compute(variant, source);
            QualityCalculator.Compute(variant, _config.MaxVariantQscore, _config.EstimatedBaseCallQuality);
            StrandBiasCalculator.Compute(variant, variant.SupportByDirection, _config.EstimatedBaseCallQuality,
                _config.StrandBiasFilterThreshold, _config.StrandBiasModel);

            // set genotype, filter, etc
            AlleleProcessor.Process(variant, _config.GenotypeModel, _config.MinFrequency, _config.MinCoverage,
                _config.VariantQscoreFilterThreshold, _config.FilterSingleStrandVariants);
        }

        private CandidateAllele Map(BaseCalledAllele called)
        {
            var candidateAllele = new CandidateAllele(called.Chromosome, called.Coordinate, called.Reference,
                called.Alternate, called.Type);

            Array.Copy(called.SupportByDirection, candidateAllele.SupportByDirection, called.SupportByDirection.Length);

            return candidateAllele;
        }

        private BaseCalledAllele Map(CandidateAllele candidate)
        {
            var calledAllele = candidate.Type == AlleleCategory.Reference
                ? (BaseCalledAllele) new CalledReference()
                : new CalledVariant(candidate.Type);

            calledAllele.Alternate = candidate.Alternate;
            calledAllele.Reference = candidate.Reference;
            calledAllele.Chromosome = candidate.Chromosome;
            calledAllele.Coordinate = candidate.Coordinate;
            calledAllele.AlleleSupport = candidate.Support;
            Array.Copy(candidate.SupportByDirection, calledAllele.SupportByDirection, candidate.SupportByDirection.Length);

            return calledAllele;
        }

        private bool IsCallable(BaseCalledAllele allele)
        {
            if (allele is CalledReference)  // reference calls always get emitted
                return true; 

            // determine if we should discard variant
            if (allele.TotalCoverage < _config.MinCoverage && !_config.IncludeReferenceCalls)
                return false; // if gvcf, call but filter later

            if (allele.TotalCoverage != 0 && allele.Frequency < _config.MinFrequency) 
                return false; 

            if (allele.Qscore < _config.MinVariantQscore)
                return false; 

            return true;
        }
    }

    public class VariantCallerConfig
    {
        public bool IncludeReferenceCalls { get; set; }
        public int MinCoverage { get; set; }
        public float MinFrequency { get; set; }
        public int MaxVariantQscore { get; set; }
        public int MinVariantQscore { get; set; }
        public int? VariantQscoreFilterThreshold { get; set; }
        public int EstimatedBaseCallQuality { get; set; }
        public float StrandBiasFilterThreshold { get; set; }
        public bool FilterSingleStrandVariants { get; set; }
        public StrandBiasModel StrandBiasModel { get; set; }
        public GenotypeModel GenotypeModel { get; set; }
    }
}
