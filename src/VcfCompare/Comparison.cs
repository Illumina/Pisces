using System.Collections.Generic;
using System.Linq;
using SequencingFiles;

namespace VcfCompare
{
    public class Comparison
    {
        public VcfVariant Variant { get; private set; }
        public bool InBaseline { get; private set; }
        public bool InTest { get; private set; }
        public Dictionary<string, ComparisonResult> ComparisonResults { get; private set; }

        public Comparison(VcfVariant variant, bool inBaseline, bool inTest)
        {
            Variant = variant;
            InBaseline = inBaseline;
            InTest = inTest;
            ComparisonResults = new Dictionary<string, ComparisonResult>();
        }

        public void AddResult(ComparisonResult result)
        {
            ComparisonResults.Add(result.Key, result);    
        }

        public string GetDiffs()
        {
            var diffs = ComparisonResults.Values.Where(x => !x.OK);
            return string.Join("\t", diffs);
        }

        public List<string> GetEntry(List<string> resultKeys)
        {
            var resultsList = new List<string>();
            resultsList.Add(VariantString(Variant));
            resultsList.Add(InBaseline.ToString());
            resultsList.Add(InTest.ToString());
            foreach (var allDiffKey in resultKeys)
            {
                if (InBaseline && InTest)
                {
                    resultsList.Add(ComparisonResults[allDiffKey].BaselineValue);
                    resultsList.Add(ComparisonResults[allDiffKey].TestValue);
                    resultsList.Add(ComparisonResults[allDiffKey].OK.ToString());
                }
                else
                {
                    resultsList.AddRange(new List<string>() { "NA", "NA", "NA" });
                }
            }

            return resultsList;
        }

        private static string VariantString(VcfVariant variant)
        {
            return string.Format("{0}  {1} {2} {3}", variant.ReferenceName,
                variant.ReferencePosition, variant.ReferenceAllele,
                variant.VariantAlleles.First());
        }


    }
}