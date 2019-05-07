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
        public static List<Tuple<CalledAllele, string>> AdjustForcedAllele(List<Tuple<CalledAllele, string>> alleleTuplesReadyToWriteList)
        {
            var forcedPositions = alleleTuplesReadyToWriteList.Where(x => x.Item1.Filters.Contains(FilterType.ForcedReport)).Select(x => x.Item1.ReferencePosition).ToImmutableHashSet();
            
            if (!forcedPositions.Any()) return alleleTuplesReadyToWriteList;

            var allelesTuplesInForcedPositionDictionary = new Dictionary<int, List<Tuple<CalledAllele, string>>>();

            foreach (var forcedPosition in forcedPositions)
            {
                var alleleTuplesNonForcedList = alleleTuplesReadyToWriteList.Where(x => x.Item1.ReferencePosition == forcedPosition && !x.Item1.Filters.Contains(FilterType.ForcedReport)).ToList();
                var alleleTuplesForcedList = alleleTuplesReadyToWriteList.Where(x => x.Item1.ReferencePosition == forcedPosition && x.Item1.Filters.Contains(FilterType.ForcedReport)).ToList();
                var alleleTuplesToAddList = AdjustForcedAlleleInPosition(alleleTuplesNonForcedList, alleleTuplesForcedList);
                allelesTuplesInForcedPositionDictionary[forcedPosition] = alleleTuplesToAddList;
            }

            var alleleTuplesAfterProcessList = new List<Tuple<CalledAllele, string>>();

            foreach (var alleleTupleReadyToWrite in alleleTuplesReadyToWriteList)
            {
                var currentPos = alleleTupleReadyToWrite.Item1.ReferencePosition;

                if (!forcedPositions.Contains(currentPos))
                {
                    alleleTuplesAfterProcessList.Add(alleleTupleReadyToWrite);
                    continue;
                }

                if (allelesTuplesInForcedPositionDictionary.ContainsKey(currentPos))
                {
                    alleleTuplesAfterProcessList.AddRange(allelesTuplesInForcedPositionDictionary[currentPos]);
                    allelesTuplesInForcedPositionDictionary.Remove(currentPos);
                }
            }
            
            return alleleTuplesAfterProcessList;
        }

        private static List<Tuple<CalledAllele, string>> AdjustForcedAlleleInPosition(
            List<Tuple<CalledAllele, string>> alleleTuplesNonForcedList, List<Tuple<CalledAllele, string>> alleleTuplesForcedList)
        {
            var alleleTuplesToAddList = new List<Tuple<CalledAllele, string>>();

            alleleTuplesToAddList.AddRange(alleleTuplesNonForcedList);

            if (!alleleTuplesNonForcedList.Any() || alleleTuplesNonForcedList.All(x => x.Item1.IsRefType))
            {
                alleleTuplesToAddList.AddRange(alleleTuplesForcedList);

                return alleleTuplesToAddList;
            }
            
            var nonForcedAlleleSet =
                alleleTuplesNonForcedList.Select(x => Tuple.Create(x.Item1.ReferenceAllele, x.Item1.AlternateAllele)).ToImmutableHashSet();

            foreach (var alleleTupleForced in alleleTuplesForcedList)
            {
                if(nonForcedAlleleSet.Contains(Tuple.Create(alleleTupleForced.Item1.ReferenceAllele,alleleTupleForced.Item1.AlternateAllele)))
                    continue;

                alleleTuplesToAddList.Add(alleleTupleForced);
            }

            return alleleTuplesToAddList;
        }
        
    }
}