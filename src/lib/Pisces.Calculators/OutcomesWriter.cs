using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.IO.Utility;
using ReadRealignmentLogic.Models;

namespace Gemini
{
    public class OutcomesWriter : IOutcomesWriter
    {
        private readonly string _outDir;
        private readonly IGeminiDataOutputFactory _outputFactory;

        public OutcomesWriter(string outDir, IGeminiDataOutputFactory outputFactory)
        {
            _outDir = outDir;
            _outputFactory = outputFactory;
        }

        public void WriteIndelOutcomesFile(ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup)
        {
            using (var sw = _outputFactory.GetTextWriter(Path.Combine(_outDir, "IndelOutcomes.csv")))
            {
                sw.WriteLine(
                    "Indel,Success,Failure,Rank,NumIndels,AddedAsMulti,ConfirmedExisting,AcceptedRealignment,OtherAccepted");
                foreach (var kvp in masterOutcomesLookup.OrderBy(x => x.Key.StringRepresentation + (x.Key.InMulti ? "|"+x.Key.OtherIndel : "")))
                {
                    sw.WriteLine(kvp.Key.StringRepresentation + (kvp.Key.InMulti ? "|" + kvp.Key.OtherIndel : "") + "," + string.Join(",", kvp.Value));
                }
            }
        }

        public void WriteIndelsFile(ConcurrentDictionary<HashableIndel, int> masterFinalIndels)
        {
            const char separator = ',';
            var masterLookup = masterFinalIndels.Keys.ToList();
            var outFilePath = Path.Combine(_outDir, "FinalIndels.csv");

            using (var sw = _outputFactory.GetTextWriter(outFilePath))
            {
                sw.WriteLine(string.Join(separator, "StringRepresentation", "Score",
                    "InMulti", "Type", "Length", "Chromosome", "ReferencePosition", "ReferenceAllele", "AlternateAllele",
                    "AllowMismatchingInsertions", "OtherIndel", "IsRepeat",
                    "RepeatUnit", "IsDuplication", "IsUntrustworthyInRepeatRegion", "NumBasesInReferenceSuffixBeforeUnique", "NumApproxDupsLeft", "NumApproxDupsRight", "NumRepeatsNearby", "RefPrefix", "RefSuffix"));
                foreach (var hashableIndel in masterLookup.OrderBy(x => x.ReferencePosition).ThenBy(x => x.ReferenceAllele).ThenBy(x => x.AlternateAllele).ThenBy(x => x.InMulti).ThenBy(x => x.OtherIndel))
                {
                    sw.WriteLine(string.Join(separator, hashableIndel.StringRepresentation + (hashableIndel.InMulti ? "|" + hashableIndel.OtherIndel : ""), hashableIndel.Score,
                        hashableIndel.InMulti, hashableIndel.Type, hashableIndel.Length, hashableIndel.Chromosome, hashableIndel.ReferencePosition, hashableIndel.ReferenceAllele, hashableIndel.AlternateAllele,
                        hashableIndel.AllowMismatchingInsertions, hashableIndel.OtherIndel, hashableIndel.IsRepeat,
                        hashableIndel.RepeatUnit, hashableIndel.IsDuplication, hashableIndel.IsUntrustworthyInRepeatRegion, 
                        hashableIndel.NumBasesInReferenceSuffixBeforeUnique, hashableIndel.NumApproxDupsLeft, 
                        hashableIndel.NumApproxDupsRight, hashableIndel.NumRepeatsNearby, hashableIndel.RefPrefix, hashableIndel.RefSuffix));
                }
            }
        }

        public void CategorizeProgressTrackerAndWriteCategoryOutcomesFile(ConcurrentDictionary<string, int> progressTracker)
        {
            var perCategoryResults = new Dictionary<string, Dictionary<string, int>>();
            var allOutcomeTypes = new HashSet<string>();
            foreach (var item in progressTracker.Keys.OrderBy(x => x))
            {
                Logger.WriteToLog($"{item}: {progressTracker[item]}");

                var splitted = item.Split(":");

                if (splitted.Length > 1)
                {
                    if (!perCategoryResults.ContainsKey(splitted[0]))
                    {
                        perCategoryResults.Add(splitted[0], new Dictionary<string, int>());
                    }

                    perCategoryResults[splitted[0]][splitted[1]] = progressTracker[item];
                    allOutcomeTypes.Add(splitted[1]);
                }
            }

            WriteCategoryOutcomesFile(allOutcomeTypes, perCategoryResults);
        }

        private void WriteCategoryOutcomesFile(HashSet<string> allOutcomeTypes, Dictionary<string, Dictionary<string, int>> perCategoryResults)
        {
            using (var sw = _outputFactory.GetTextWriter(Path.Combine(_outDir, "CategoryOutcomes.csv")))
            {
                var outcomeTypesSorted = allOutcomeTypes.ToList().OrderBy(x => x).ToList();
                sw.WriteLine("Category," + string.Join(",", outcomeTypesSorted));
                foreach (var category in perCategoryResults.Keys)
                {
                    var results = new List<int>();

                    foreach (var outcomeType in outcomeTypesSorted)
                    {
                        if (perCategoryResults[category].ContainsKey(outcomeType))
                        {
                            results.Add(perCategoryResults[category][outcomeType]);
                        }
                        else
                        {
                            results.Add(0);
                        }
                    }

                    sw.WriteLine(category + "," + string.Join(",", results));
                }
            }
        }

    }
}