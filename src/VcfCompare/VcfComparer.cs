using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;

namespace VcfCompare
{
    class VcfComparer
    {
        private static Program.VariantType GetVariantType(VcfVariant variant)
        {
            if (variant.ReferenceAllele.Length > variant.VariantAlleles.First().Length) return Program.VariantType.Deletion;
            if (variant.ReferenceAllele.Length < variant.VariantAlleles.First().Length) return Program.VariantType.Insertion;
            if (variant.ReferenceAllele.Length == variant.VariantAlleles.First().Length && variant.ReferenceAllele.Length > 1) return Program.VariantType.Mnv;
            return Program.VariantType.Snv;
        }

        public static void BaselineVcfs(string baselineVcfPath, string testVcfPath, VcfComparisonConfig config, VcfRegion region=null, bool verbose = false)
        {
            var logger = new Logger(verbose);

            Console.WriteLine("Configuration Options");
            Console.WriteLine("------------------------------");
            Console.WriteLine(config);
            Console.WriteLine("==============================" + Environment.NewLine);
            Console.WriteLine("Comparing {0} to {1}", baselineVcfPath, testVcfPath);

            var numblocks = region.Chromosome!=null ? 1 + ((region.End - region.Start) / config.BlockSize) : 1;

            var comparisonKeys = config.GetKeys();

            if (config.OutputFile != null)
            {
                using (StreamWriter sw = File.CreateText(config.OutputFile))
                {
                    sw.WriteLine("Variant\tInBaseline\tInTest," + string.Join("\t", comparisonKeys.Select(x => string.Join("\t", new List<string>() { x + "_1", x + "_2", x + "_OK" }))));
                }
            }
            if (config.SummaryFile != null && !File.Exists(config.SummaryFile))
            {
                using (StreamWriter sw = File.CreateText(config.SummaryFile))
                {
                    sw.WriteLine("BaselineVcf,TestVcf,SharedDiffs,BaselineOnly,TestOnly");
                }
            }


            for (int i = 0; i < numblocks; i++)
            {
                bool hitEndOfChromosome;

                var subRegion = new VcfRegion();
                if (region.Chromosome != null)
                {
                    subRegion.Chromosome = region.Chromosome;
                    subRegion.Start = region.Start + i*config.BlockSize;
                    if (region.End>0) subRegion.End = Math.Min(region.Start + (i + 1)*config.BlockSize, region.End);

                    Console.WriteLine("==============================");
                    Console.WriteLine("PROCESSING FOR REGION: " + subRegion.ToString());
                }

                // Get baseline variants
                Dictionary<string, List<VcfVariant>> baselineVariantsDict;
                var alleles = VcfParser.GetAlleles(baselineVcfPath, subRegion, logger, out hitEndOfChromosome);
                var baselineVariants = ProcessVariants(config, alleles, out baselineVariantsDict);

                // Get test variants
                var testAlleles = VcfParser.GetAlleles(testVcfPath, subRegion, logger, out hitEndOfChromosome);
                Dictionary<string, List<VcfVariant>> testVariantsDict;
                var testVariants = ProcessVariants(config, testAlleles, out testVariantsDict);

                // Compare baseline vs test
                var allDiffs = Compare(config, baselineVariants, testVariants, baselineVariantsDict, testVariantsDict);

                if (config.SummaryFile != null)
                {
                    FlushSummary(config.SummaryFile, allDiffs, baselineVcfPath, testVcfPath);                    
                }

                if (allDiffs.Any() && config.OutputFile!=null)
                {
                    foreach (var diff in allDiffs.OrderBy(d=> d.Variant.ReferencePosition))
                    {
                        FlushDiff(config.OutputFile, diff.GetEntry(comparisonKeys));
                    }

                }

                if (hitEndOfChromosome) break;
            }
        }

        private static List<VcfVariant> ProcessVariants(VcfComparisonConfig config, List<VcfVariant> testAlleles, out Dictionary<string, List<VcfVariant>> testVariantsDict)
        {
            var testVariants = new List<VcfVariant>();
            testVariantsDict = new Dictionary<string, List<VcfVariant>>();
            var variantCalls = config.ConsiderRefs ? testAlleles : testAlleles.Where(a => a.VariantAlleles[0] != ".").ToList();

            foreach (var variantCall in variantCalls)
            {
                var type = GetVariantType(variantCall);
                if ((config.CheckSnv && type == Program.VariantType.Snv) || (config.CheckMnv && type == Program.VariantType.Mnv) ||
                    (config.CheckDeletions && type == Program.VariantType.Deletion) ||
                    (config.CheckInsertions && type == Program.VariantType.Insertion))
                {
                    testVariants.Add(variantCall);
                    if (!testVariantsDict.ContainsKey(variantCall.ReferenceName))
                    {
                        testVariantsDict[variantCall.ReferenceName] = new List<VcfVariant>();
                    }
                    testVariantsDict[variantCall.ReferenceName].Add(variantCall);
                }
            }
            return testVariants;
        }

        private static List<Comparison> Compare(VcfComparisonConfig config, List<VcfVariant> baselineVariants,
            List<VcfVariant> testVariants, Dictionary<string, List<VcfVariant>> baselineVariantsDict, Dictionary<string, List<VcfVariant>> testVariantsDict)
        {
            var numBaselines = baselineVariants.Count(v => !config.PassingOnly || v.Filters == "PASS");
            var numTestVars = testVariants.Count(v => !config.PassingOnly || v.Filters == "PASS");

            Console.WriteLine("Baseline Variants : " + numBaselines);
            Console.WriteLine("Test Variants : " + numTestVars);

            // Check for variants missing from either baseline or test
            var baselineVariantsMissingInTest = new List<VcfVariant>();
            var testVariantsMissingInBaseline = new List<VcfVariant>();
            var sharedVariants = new List<Tuple<VcfVariant, VcfVariant>>();

            foreach (var testVariant in testVariants)
            {
                var variantInOtherVcf = FindVariantInOtherVcf(testVariant, baselineVariantsDict, config.PassingOnly);
                if (variantInOtherVcf == null)
                {
                    testVariantsMissingInBaseline.Add(testVariant);
                }
            }

            foreach (var baselineVariant in baselineVariants)
            {
                var variantInOtherVcf = FindVariantInOtherVcf(baselineVariant, testVariantsDict, config.PassingOnly);
                if (variantInOtherVcf == null)
                {
                    baselineVariantsMissingInTest.Add(baselineVariant);
                }
                else if (variantInOtherVcf.ReferenceName != null)
                {
                    sharedVariants.Add(new Tuple<VcfVariant, VcfVariant>(baselineVariant, variantInOtherVcf));
                }
            }

            Console.WriteLine(Environment.NewLine + "------------------------------");
            Console.WriteLine("Baseline Missing In Test : {0} ({1}%)", baselineVariantsMissingInTest.Count(),
                Math.Round(100*(float) baselineVariantsMissingInTest.Count()/numBaselines, 2));
            Console.WriteLine("------------------------------");
            if (baselineVariantsMissingInTest.Any())
            {
                PrintVariants(baselineVariantsMissingInTest);
                Console.WriteLine("------------------------------" + Environment.NewLine);
            }

            Console.WriteLine("Test Missing In Baseline : {0} ({1}%)", testVariantsMissingInBaseline.Count(),
                Math.Round(100*(float) testVariantsMissingInBaseline.Count()/numTestVars, 2));
            Console.WriteLine("------------------------------");
            if (testVariantsMissingInBaseline.Any())
            {
                PrintVariants(testVariantsMissingInBaseline);
                Console.WriteLine("------------------------------" + Environment.NewLine);
            }

            var allDiffs = new List<Comparison>();
            if (!config.HideSharedDiffs)
            {
                Console.WriteLine("Comparison of Shared Variants ({0})", sharedVariants.Count());
                Console.WriteLine("------------------------------");

                // Compare shared variants
                allDiffs = CompareSharedVariants(config, sharedVariants);
                Console.WriteLine("------------------------------");
            }

            allDiffs.AddRange(baselineVariantsMissingInTest.Select(variant => new Comparison(variant, true, false)));
            allDiffs.AddRange(testVariantsMissingInBaseline.Select(variant => new Comparison(variant, false, true)));

            return allDiffs;
        }

        private static List<Comparison> CompareSharedVariants(VcfComparisonConfig config, List<Tuple<VcfVariant, VcfVariant>> sharedVariants)
        {
            var allDiffs = new List<Dictionary<string, string>>();
            var allComparisons = new List<Comparison>();

            foreach (var sharedVariant in sharedVariants)
            {
                if (sharedVariant.Item1.ToString() == sharedVariant.Item2.ToString())
                {
                    //these are exactly the same.
                    continue;
                }

                var okResult = "OK";

                bool allOk = true;

                var comparison = new Comparison(sharedVariant.Item1, true, true);

                if (config.CheckVF)
                {
                    var result = okResult;
                    var vf1 = GetVF(sharedVariant.Item1);
                    var vf2 = GetVF(sharedVariant.Item2);
                    var vfsEqual = ApproximatelyEqual(vf1, vf2, config.MarginOfError);
                    if (!vfsEqual)
                    {
                        allOk = false;
                        result = ((float) (vf2)/vf1).ToString();
                    }
                    comparison.AddResult(new ComparisonResult("VF",vf1,vf2,vfsEqual, result));
                }
                if (config.CheckDP)
                {
                    var result = okResult;
                    var dp1 = GetDP(sharedVariant.Item1);
                    var dp2 = GetDP(sharedVariant.Item2);
                    var dpsEqual = ApproximatelyEqual(dp1, dp2, config.MarginOfError);
                    if (!dpsEqual)
                    {
                        allOk = false;
                        result = ((float) (dp2)/dp1).ToString();
                    }
                    comparison.AddResult(new ComparisonResult("DP",dp1, dp2, dpsEqual, result));
                }
                if (config.CheckGT)
                {
                    var result = okResult;
                    var gt1 = GetGT(sharedVariant.Item1);
                    var gt2 = GetGT(sharedVariant.Item2);
                    var gtsEqual = gt1 == gt2;
                    if (!gtsEqual)
                    {
                        allOk = false;
                        result = "False";
                    }
                    comparison.AddResult(new ComparisonResult("GT",gt1, gt2, gtsEqual));
                }
                if (config.CheckFilter)
                {
                    var result = okResult;
                    var filtersEqual = sharedVariant.Item1.Filters == sharedVariant.Item2.Filters;
                    if (!filtersEqual)
                    {
                        allOk = false;
                        result = "False";
                    }
                    comparison.AddResult(new ComparisonResult("Filters",sharedVariant.Item1.Filters, sharedVariant.Item2.Filters, filtersEqual, result));
                }
                if (config.CheckSB)
                {
                    var result = okResult;
                    var sb1 = GetSB(sharedVariant.Item1);
                    var sb2 = GetSB(sharedVariant.Item2);
                    var sbsEqual = ApproximatelyEqual(sb1, sb2, config.MarginOfError);
                    if (!sbsEqual)
                    {
                        allOk = false;
                        result = ((float) (sb2)/sb1).ToString();
                    }
                    comparison.AddResult(new ComparisonResult("SB",sb1, sb2, sbsEqual, result));
                }
                if (config.CheckAD)
                {
                    var result = okResult;
                    var ad1 = GetAD(sharedVariant.Item1);
                    var ad2 = GetAD(sharedVariant.Item2);
                    var adsEqual = ad1 == ad2;
                    if (!adsEqual)
                    {
                        var ad1split = ad1.Split(',');
                        var ad2split = ad2.Split(',');
                        allOk = false;
                        result = "RefDepthRatio:" + ((float)(int.Parse(ad2split[0])) / int.Parse(ad1split[0])).ToString() +";AltDepthRatio:" + (ad1split.Length>1 && ad2split.Length>1 ? ((float)(int.Parse(ad2split[1])) / int.Parse(ad1split[1])).ToString() : "NA");
                    }
                    comparison.AddResult(new ComparisonResult("AD",ad1, ad2, adsEqual, result));
                }
                if (config.CheckQual)
                {
                    var result = okResult;
                    var qualsEqual = ApproximatelyEqual(sharedVariant.Item1.Quality, sharedVariant.Item2.Quality);
                    if (!qualsEqual)
                    {
                        allOk = false;
                        result = ((float) sharedVariant.Item2.Quality/sharedVariant.Item1.Quality).ToString();
                    }
                    comparison.AddResult(new ComparisonResult("Qual",sharedVariant.Item1.Quality, sharedVariant.Item2.Quality, qualsEqual, result));
                }

                if (allOk && config.Exact)
                {
                    if (sharedVariant.Item1.ToString() != sharedVariant.Item2.ToString())
                    {
                        comparison.AddResult(new ComparisonResult("Exact", sharedVariant.Item1.ToString(), sharedVariant.Item2.ToString(), false));
                        allOk = false;
                    }
                }

                if (!allOk)
                {
                    Console.WriteLine(comparison.Variant + "\t" + string.Join("\t", comparison.GetDiffs()));
                    allComparisons.Add(comparison);
                }
            }

            return allComparisons;

        }

        private static void FlushSummary(string outputFile, List<Comparison> allDiffs, string baselinePath, string testPath)
        {
            using (StreamWriter sw = File.AppendText(outputFile))
            {
                sw.WriteLine("{0},{1},{2},{3},{4}",baselinePath,testPath,allDiffs.Count(d=>d.InBaseline && d.InTest), allDiffs.Count(d=>d.InBaseline && !d.InTest), allDiffs.Count(d=>!d.InBaseline && d.InTest));
            }
        }

        private static void FlushDiff(string outputFile, List<string> results)
        {
            using (StreamWriter sw = File.AppendText(outputFile))
            {
                sw.WriteLine(string.Join("\t", results));
            }        
        }

        private static void PrintVariants(IEnumerable<VcfVariant> variants)
        {
            foreach (var vcfVariant in variants)
            {
                var vf = vcfVariant.Genotypes[0]["VF"];
                Console.WriteLine("{0}  {1} {2} {3} {4} {5} {6}",vcfVariant.ReferenceName, vcfVariant.ReferencePosition, vcfVariant.ReferenceAllele, vcfVariant.VariantAlleles.First(), vcfVariant.Filters, vcfVariant.Quality, vf);
            }
            
        }

        private static double GetDP(VcfVariant variant)
        {
            return double.Parse(variant.InfoFields["DP"]);
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

        private static VcfVariant FindVariantInOtherVcf(VcfVariant variant, Dictionary<string,List<VcfVariant>> otherVcfDict, bool passingOnly = true)
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
            //Already assume these are from the same chromosome
            return variant1.ReferencePosition == variant2.ReferencePosition &&
                   variant1.VariantAlleles[0] == variant2.VariantAlleles[0] &&
                   variant1.ReferenceAllele == variant2.ReferenceAllele;
        }
    }
}