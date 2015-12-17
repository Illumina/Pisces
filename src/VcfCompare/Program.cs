using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SequencingFiles;

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

        public VcfComparisonConfig()
        {
            PassingOnly = true;
            MarginOfError = 0.05;
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
                "Margin of Error: " + Math.Round(MarginOfError,2)
            };
            return string.Join(Environment.NewLine, configList);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var baselineVcfPath = args[0];
            var testVcfPath = args[1];

            var config = new VcfComparisonConfig();

            if (args.Length > 2)
            {
                config.Exact = args.Contains("-Exact");
                config.CheckGT = args.Contains("-GT");
                config.CheckDP = args.Contains("-DP");
                config.CheckVF = args.Contains("-VF");
                config.CheckSB = args.Contains("-SB");
                config.CheckFilter = args.Contains("-Filter");
                config.CheckQual = args.Contains("-Qual");
                config.PassingOnly = !args.Contains("-AllVars");
                config.ConsiderRefs = args.Contains("-Refs");
                config.CheckAD = args.Contains("-AD");
            }

            double marginOfError = 0;
            var configMargin = ConfigurationManager.AppSettings["MarginOfError"];
            if (configMargin != "N/A")
            {
                config.MarginOfError = float.Parse(configMargin);
            };
            var variantTypes = ConfigurationManager.AppSettings["VariantTypes"].Split(',');
            config.CheckDeletions = variantTypes.Contains("Del");
            config.CheckInsertions = variantTypes.Contains("Ins");
            config.CheckSnv = variantTypes.Contains("Snv");
            config.CheckMnv = variantTypes.Contains("Mnv");

            Console.WriteLine();
            Console.WriteLine(string.Join(" ",args));
            Console.WriteLine("==============================" + Environment.NewLine);
            Console.WriteLine("Variant Types: "+string.Join(",",variantTypes));

            BaselineVcfs(baselineVcfPath,testVcfPath, config, args.Contains("-AllComparisons"));
        }

        private static VariantType GetVariantType(VcfVariant variant)
        {
            if (variant.ReferenceAllele.Length > variant.VariantAlleles.First().Length) return VariantType.Deletion;
            if (variant.ReferenceAllele.Length < variant.VariantAlleles.First().Length) return VariantType.Insertion;
            if (variant.ReferenceAllele.Length == variant.VariantAlleles.First().Length && variant.ReferenceAllele.Length > 1) return VariantType.Mnv;
            return VariantType.Snv;
        }

        public enum VariantType
        {
            Snv,
            Mnv,
            Insertion,
            Deletion
        }

        public static void BaselineVcfs(string baselineVcfPath, string testVcfPath, VcfComparisonConfig config, bool showAllComparisons)
        {
            Console.WriteLine("Configuration Options");
            Console.WriteLine("------------------------------");
            Console.WriteLine(config);
            Console.WriteLine("==============================" + Environment.NewLine);
            Console.WriteLine("Comparing {0} to {1}", baselineVcfPath, testVcfPath);

            var baselineVariantsMissingInTest = new List<VcfVariant>();
            var testVariantsMissingInBaseline = new List<VcfVariant>();
            var sharedVariants = new List<Tuple<VcfVariant, VcfVariant>>();

            var baselineVariants = new List<VcfVariant>();
            var baselineVariantsDict = new Dictionary<string, List<VcfVariant>>();

            using (var reader = new VcfReader(baselineVcfPath))
            {
                var alleles = reader.GetVariants().ToList();

                var variantCalls = config.ConsiderRefs ? alleles :  alleles.Where(a => a.VariantAlleles[0] != ".").ToList();

                foreach (var variantCall in variantCalls)
                {
                    var type = GetVariantType(variantCall);
                    if ((config.CheckSnv && type == VariantType.Snv) || (config.CheckMnv && type == VariantType.Mnv) ||
                        (config.CheckDeletions && type == VariantType.Deletion) ||
                        (config.CheckInsertions && type == VariantType.Insertion))
                    {
                        baselineVariants.Add(variantCall);
                        if (!baselineVariantsDict.ContainsKey(variantCall.ReferenceName))
                        {
                            baselineVariantsDict[variantCall.ReferenceName] = new List<VcfVariant>();
                        }
                        baselineVariantsDict[variantCall.ReferenceName].Add(variantCall);
                    }
                }
            }

            var testVariants = new List<VcfVariant>();
            var testVariantsDict = new Dictionary<string, List<VcfVariant>>();
            using (var reader = new VcfReader(testVcfPath))
            {
                var alleles = reader.GetVariants().ToList();

                var variantCalls = config.ConsiderRefs ? alleles : alleles.Where(a => a.VariantAlleles[0] != ".").ToList();

                foreach (var variantCall in variantCalls)
                {
                    var type = GetVariantType(variantCall);
                    if ((config.CheckSnv && type == VariantType.Snv) || (config.CheckMnv && type == VariantType.Mnv) ||
                        (config.CheckDeletions && type == VariantType.Deletion) ||
                        (config.CheckInsertions && type == VariantType.Insertion))
                    {
                        testVariants.Add(variantCall);
                        if (!testVariantsDict.ContainsKey(variantCall.ReferenceName))
                        {
                            testVariantsDict[variantCall.ReferenceName] = new List<VcfVariant>();
                        }
                        testVariantsDict[variantCall.ReferenceName].Add(variantCall);
                    }
                }
            }

            var numBaselines = baselineVariants.Count(v => !config.PassingOnly || v.Filters == "PASS");
            var numTestVars = testVariants.Count(v => !config.PassingOnly || v.Filters == "PASS");

            Console.WriteLine("Baseline Variants : " + numBaselines);
            Console.WriteLine("Test Variants : " + numTestVars);

            foreach (var testVariant in testVariants)
            {
                var variantInOtherVcf = FindVariantInOtherVcf(testVariant, baselineVariants, baselineVariantsDict, config.PassingOnly);
                if (variantInOtherVcf == null)
                {
                    testVariantsMissingInBaseline.Add(testVariant);
                }
            }

            foreach (var baselineVariant in baselineVariants)
            {
                var variantInOtherVcf = FindVariantInOtherVcf(baselineVariant, testVariants, testVariantsDict, config.PassingOnly);
                if (variantInOtherVcf == null)
                {
                    baselineVariantsMissingInTest.Add(baselineVariant);
                }
                else if (variantInOtherVcf.ReferenceName!=null)
                {
                    sharedVariants.Add(new Tuple<VcfVariant, VcfVariant>(baselineVariant,variantInOtherVcf));

                }
            }

            Console.WriteLine(Environment.NewLine + "------------------------------" + Environment.NewLine);

            Console.WriteLine("Baseline Missing In Test : {0} ({1}%)",baselineVariantsMissingInTest.Count(), Math.Round(100*(float)baselineVariantsMissingInTest.Count()/numBaselines,2));
            Console.WriteLine("------------------------------");
            PrintVariants(baselineVariantsMissingInTest);

            Console.WriteLine(Environment.NewLine + "------------------------------" + Environment.NewLine);

            Console.WriteLine("Test Missing In Baseline : {0} ({1}%)", testVariantsMissingInBaseline.Count(),Math.Round(100*(float)testVariantsMissingInBaseline.Count() / numTestVars,2));
            Console.WriteLine("------------------------------");
            PrintVariants(testVariantsMissingInBaseline);

            Console.WriteLine(Environment.NewLine + "------------------------------" + Environment.NewLine);

            Console.WriteLine("Comparison of Shared Variants ({0})",sharedVariants.Count());
            Console.WriteLine("------------------------------");

            var allDiffs = new List<Dictionary<string, string>>();

            foreach (var sharedVariant in sharedVariants)
            {
                if (sharedVariant.Item1.ToString() == sharedVariant.Item2.ToString())
                {
                    //these are exactly the same.
                    continue;
                }

                var matchDict = new Dictionary<string, string>();
                var okResult = "OK";

                var matchStrings = new List<string>();
                bool allOk = true;
                var variantString = string.Format("{0}  {1} {2} {3}", sharedVariant.Item1.ReferenceName, sharedVariant.Item1.ReferencePosition, sharedVariant.Item1.ReferenceAllele, sharedVariant.Item1.VariantAlleles.First());

                matchStrings.Add(variantString);
                matchDict["Variant"] = variantString;

                if (config.CheckVF)
                {
                    var result = okResult;
                    var vf1 = GetVF(sharedVariant.Item1);
                    var vf2 = GetVF(sharedVariant.Item2);
                    var vfsEqual = ApproximatelyEqual(vf1, vf2, config.MarginOfError);
                    if (!vfsEqual)
                    {
                        matchStrings.Add(string.Format("VF: ({0} vs {1})", vf1, vf2));
                        allOk = false;
                        result = ((float) (vf2)/vf1).ToString();
                    }
                    matchDict["VF"] = result;
                }
                if (config.CheckDP)
                {
                    var result = okResult;
                    var dp1 = GetDP(sharedVariant.Item1);
                    var dp2 = GetDP(sharedVariant.Item2);
                    var dpsEqual = ApproximatelyEqual(dp1, dp2, config.MarginOfError);
                    if (!dpsEqual)
                    {
                        matchStrings.Add(string.Format("DP: ({0} vs {1})", dp1, dp2));
                        allOk = false;
                        result = ((float) (dp2)/dp1).ToString();
                    }
                    matchDict["DP"] = result;
                }
                if (config.CheckGT)
                {
                    var result = okResult;
                    var gt1 = GetGT(sharedVariant.Item1);
                    var gt2 = GetGT(sharedVariant.Item2);
                    var gtsEqual = gt1 == gt2;
                    if (!gtsEqual)
                    {
                        matchStrings.Add(string.Format("GT: ({0} vs {1})", gt1, gt2));
                        allOk = false;
                    }
                    matchDict["GT"] = result;
                }
                if (config.CheckFilter)
                {
                    var result = okResult;
                    var filtersEqual = sharedVariant.Item1.Filters == sharedVariant.Item2.Filters;
                    if (!filtersEqual)
                    {
                        matchStrings.Add(string.Format("Filters: ({0} vs {1})", sharedVariant.Item1.Filters,
                            sharedVariant.Item2.Filters));
                        allOk = false;
                        result = "False";
                    }
                    matchDict["Filters"] = result;
                }
                if (config.CheckSB)
                {
                    var result = okResult;
                    var sb1 = GetSB(sharedVariant.Item1);
                    var sb2 = GetSB(sharedVariant.Item2);
                    var sbsEqual = ApproximatelyEqual(sb1, sb2, config.MarginOfError);
                    if (!sbsEqual)
                    {
                        matchStrings.Add(string.Format("SB: ({0} vs {1})", sb1, sb2));
                        allOk = false;
                        result = ((float)(sb2)/sb1).ToString();
                    }
                    matchDict["SB"] = result;
                }
                if (config.CheckAD)
                {
                    var result = okResult;
                    var ad1 = GetAD(sharedVariant.Item1);
                    var ad2 = GetAD(sharedVariant.Item2);
                    var adsEqual = ad1 == ad2;
                    //var adsEqual = ApproximatelyEqual(ad1, ad2, config.MarginOfError);
                    if (!adsEqual)
                    {
                        matchStrings.Add(string.Format("AD: ({0} vs {1})", ad1, ad2));
                        allOk = false;
                        result = "False";
                    }
                    matchDict["AD"] = result;

                    var ad1split = ad1.Split(',');
                    var ad2split = ad2.Split(',');
                    matchDict["RefDepthRatio"] = ((float)(int.Parse(ad2split[0])) / int.Parse(ad1split[0])).ToString();
                    matchDict["AltDepthRatio"] = ((float)(int.Parse(ad2split[1])) / int.Parse(ad1split[1])).ToString();
                }
                if (config.CheckQual)
                {
                    var result = okResult;
                    var qualsEqual = ApproximatelyEqual(sharedVariant.Item1.Quality, sharedVariant.Item2.Quality);
                    if (!qualsEqual)
                    {
                        matchStrings.Add(string.Format("Qual: ({0} vs {1})", sharedVariant.Item1.Quality,
                            sharedVariant.Item2.Quality));
                        allOk = false;
                        result = ((float)sharedVariant.Item2.Quality/sharedVariant.Item1.Quality).ToString();
                    }
                    matchDict["Qual"] = result;
                }

                if (allOk|| config.Exact)
                {
                    if (sharedVariant.Item1.ToString() != sharedVariant.Item2.ToString())
                    {
                        matchStrings.Add(
                            string.Format(
                                Environment.NewLine + "{0}" + Environment.NewLine + "vs" + Environment.NewLine + "{1}" + Environment.NewLine,
                                sharedVariant.Item1.ToString(), sharedVariant.Item2.ToString()));
                        allOk = false;
                    }
                }
                if (!allOk && !showAllComparisons)
                    Console.WriteLine(string.Join("\t",matchStrings));

                if (!allOk)
                allDiffs.Add(matchDict);
            }

            if (showAllComparisons && allDiffs.Any())
            {
                var allDiffKeys = allDiffs.First().Keys;
                Console.WriteLine(string.Join(",",allDiffKeys));

                foreach (var allDiff in allDiffs)
                {
                    var resultsList = new List<string>();
                    foreach (var allDiffKey in allDiffKeys)
                    {
                        resultsList.Add(allDiff[allDiffKey]);
                    }
                    Console.WriteLine(string.Join(",",resultsList));
                }
                
            }
        }

        private static void PrintVariants(IEnumerable<VcfVariant> variants)
        {
            foreach (var vcfVariant in variants)
            {
                string vf;
                vcfVariant.TryGetGenotypeField(1, "VF", out vf);
                vf = vcfVariant.Genotypes[0]["VF"];
                Console.WriteLine("{0}  {1} {2} {3} {4} {5} {6}",vcfVariant.ReferenceName, vcfVariant.ReferencePosition, vcfVariant.ReferenceAllele, vcfVariant.VariantAlleles.First(), vcfVariant.Filters, vcfVariant.Quality, vf);
            }
            
        }

        private static double GetDP(VcfVariant variant)
        {
            double dp;
            variant.TryParseInfoDouble("DP", out dp);
            return dp;
        }
        private static string GetAD(VcfVariant variant)
        {
            return variant.Genotypes[0]["AD"];
        }

        private static string GetGT(VcfVariant variant)
        {
            return variant.Genotypes[0]["GT"];
        }
        private static float GetSB(VcfVariant variant)
        {
            return float.Parse(variant.Genotypes[0]["SB"]);
        }

        private static float GetVF(VcfVariant variant)
        {
            return float.Parse(variant.Genotypes[0]["VF"]);   
        }

        private static bool ApproximatelyEqual(double baseValue, double testValue, double marginOfError = 0.05)
        {
            if (baseValue==testValue) return true;
            return Math.Abs((baseValue - testValue) / (.5 * (baseValue + testValue))) <= marginOfError;
        }

        private static VcfVariant FindVariantInOtherVcf(VcfVariant variant, IEnumerable<VcfVariant> otherVcf, Dictionary<string,List<VcfVariant>> otherVcfDict, bool passingOnly = true)
        {
            if (passingOnly && variant.Filters != "PASS")
                return new VcfVariant();

            if (otherVcfDict.ContainsKey(variant.ReferenceName))
            {
                try
                {
                    return otherVcfDict[variant.ReferenceName].First(x => VariantsMatch(x, variant));
                }
                catch
                {
                    return null;
                }

            }
            return null;
        }

        private static bool VariantsMatch(VcfVariant variant1, VcfVariant variant2)
        {
            return variant1.ReferenceName == variant2.ReferenceName &&
                   variant1.ReferencePosition == variant2.ReferencePosition &&
                   variant1.ReferenceAllele == variant2.ReferenceAllele &&
                   variant1.VariantAlleles.First() == variant2.VariantAlleles.First();
        }

    }
}
