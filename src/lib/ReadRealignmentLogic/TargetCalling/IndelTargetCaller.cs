using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Utility;
using ReadRealignmentLogic.Interfaces;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic.TargetCalling
{
    public class IndelTargetCaller : ITargetCaller
    {
        public float _frequencyCutoff;
        private ICoverageCalculator _coverageCalculator;

        public IndelTargetCaller(float frequencyCutoff, ICoverageCalculator coverageCalculator = null)
        {
            _frequencyCutoff = frequencyCutoff;
            _coverageCalculator = coverageCalculator ?? new CoverageCalculator();
        }

        public List<CandidateIndel> Call(List<CandidateIndel> candidateIndels, IAlleleSource alleleSource)
        {
            var calledIndels = new List<CandidateIndel>();

            foreach (var candidate in candidateIndels)
            {
                if (IsCallable(candidate, alleleSource))
                    calledIndels.Add(candidate);
            }

            return calledIndels;
        }

        private bool IsCallable(CandidateIndel indel, IAlleleSource alleleSource)
        {
            // set frequency
            var callable = AlleleHelper.Map(indel);
            _coverageCalculator.Compute(callable, alleleSource);
            indel.Frequency = callable.Frequency;

            return indel.IsKnown || callable.Frequency >= _frequencyCutoff;
        }
    }
}
