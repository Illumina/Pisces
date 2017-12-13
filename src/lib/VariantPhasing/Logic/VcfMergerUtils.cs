using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace VariantPhasing.Logic
{
    public static class VcfMergerUtils
    {
        public static List<CalledAllele> AdjustForcedAllele(List<CalledAllele> allelesReadyToWrite)
        {
            var forcedPositions = allelesReadyToWrite.Where(x => x.Filters.Contains(FilterType.ForcedReport)).Select(x=>x.ReferencePosition).ToImmutableHashSet();

            if (!forcedPositions.Any()) return allelesReadyToWrite;

            var allelesInForcedPosition = new Dictionary<int,IEnumerable<CalledAllele>>();
            foreach (var forcedPosition in forcedPositions)
            {
                var nonForcedAllele = allelesReadyToWrite.Where(x => x.ReferencePosition == forcedPosition && !x.Filters.Contains(FilterType.ForcedReport)).ToList();
                var forcedAllele = allelesReadyToWrite.Where(x => x.ReferencePosition == forcedPosition && x.Filters.Contains(FilterType.ForcedReport)).ToList();
                var allelesToAdd = AdjustForcedAlleleInPosition(nonForcedAllele, forcedAllele);
                allelesInForcedPosition[forcedPosition] = allelesToAdd;
            }

            var allelesAfterProcess = new List<CalledAllele>();

            foreach (var calledAllele in allelesReadyToWrite)
            {
                var currentPos = calledAllele.ReferencePosition;
                if (!forcedPositions.Contains(currentPos))
                {
                    allelesAfterProcess.Add(calledAllele);
                    continue;
                }
                if (allelesInForcedPosition.ContainsKey(currentPos))
                {
                    allelesAfterProcess.AddRange(allelesInForcedPosition[currentPos]);
                    allelesInForcedPosition.Remove(currentPos);
                }
            }
            

            return allelesAfterProcess;
        }

        private static IEnumerable<CalledAllele> AdjustForcedAlleleInPosition(List<CalledAllele> nonForcedAlleles,
            List<CalledAllele> forcedAlleles)
        {
            var allelesToAdd = new List<CalledAllele>();

            allelesToAdd.AddRange(nonForcedAlleles);

            if (!nonForcedAlleles.Any() || nonForcedAlleles.All(x => x.IsRefType))
            {
                allelesToAdd.AddRange(forcedAlleles);

                return allelesToAdd;
            }
                


            var nonForcedAlleleSet =
                nonForcedAlleles.Select(x => Tuple.Create(x.ReferenceAllele, x.AlternateAllele)).ToImmutableHashSet();

            foreach (var calledAllele in forcedAlleles)
            {
                if(nonForcedAlleleSet.Contains(Tuple.Create(calledAllele.ReferenceAllele,calledAllele.AlternateAllele)))
                    continue;

                allelesToAdd.Add(calledAllele);

            }

            return allelesToAdd;
        }
        
    }
}