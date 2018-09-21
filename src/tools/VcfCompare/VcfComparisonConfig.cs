using System;
using System.Collections.Generic;

namespace VcfCompare
{
    public class VcfComparisonConfig
    {
        public bool CheckGT { get; set; }
        public bool CheckDP { get; set; }
        public bool CheckVF { get; set; }
        public bool CheckSB { get; set; }
        public bool CheckAD { get; set; }
        public bool CheckFilter { get; set; }
        public bool CheckQual { get; set; }
        public bool ConsiderRefs { get; set; }
        public bool PassingOnly { get; set; }
        public double MarginOfError { get; set; }
        public bool Exact { get; set; }
        public bool CheckDeletions { get; set; }
        public bool CheckInsertions { get; set; }
        public bool CheckSnv { get; set; }
        public bool CheckMnv { get; set; }
        public string OutputFile { get; set; }
        public string SummaryFile { get; set; }
        public int BlockSize { get; set; }
        public bool HideSharedDiffs { get; set; }

        public VcfComparisonConfig()
        {
            PassingOnly = true;
            MarginOfError = 0.05;
        }

        public void AllCheck(bool doCheckAll)
        {
            if (doCheckAll)
            {
                CheckGT = true;
                CheckDP = true;
                CheckVF = true;
                CheckSB = true;
                CheckAD = true;
                CheckFilter = true;
                CheckQual = true;
            }
        }

        public override string ToString()
        {
            var configList = new List<string>()
            {
                "EXACT: " + Exact,
                "Check GT: " + CheckGT,
                "Check DP: " + CheckDP,
                "Check VF: " + CheckVF,
                "Check SB: " + CheckSB,
                "Check ADs: " + CheckAD,
                "Check Filters: " + CheckFilter,
                "Check Qual: " + CheckQual,
                "Consider Refs: " + ConsiderRefs,
                "Passing Only: " + PassingOnly,
                "Margin of Error: " + Math.Round(MarginOfError,2),
                "Output file:" + OutputFile
            };
            return string.Join(Environment.NewLine, configList);
        }

        public List<string> GetKeys()
        {
            var keys = new List<string>();
            if (CheckGT) keys.Add("GT");
            if (CheckDP) keys.Add("DP");
            if (CheckVF) keys.Add("VF");
            if (CheckSB) keys.Add("SB");
            if (CheckAD) keys.Add("AD");
            if (CheckFilter) keys.Add("Filters");
            if (CheckQual) keys.Add("Qual");
            return keys;
        }
    }
}