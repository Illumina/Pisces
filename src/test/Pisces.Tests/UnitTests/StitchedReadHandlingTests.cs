using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TestUtilities;
using Alignment.Domain.Sequencing;
using Pisces.Logic;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Options;
using Xunit.Extensions;
using Xunit;

namespace Pisces.Tests.UnitTests
{

    public class StitchedReadHandlingTests
    {
        static string GenomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        static string OutputFolder = Path.Combine(TestPaths.LocalTestDataDirectory, "ExcelStitchingTests");
        static string LoadingSummaryFileName = "StitchReadLoadingSummary.csv";
        static string StrandBiasSummaryFileName = "StitchReadStrandBiasSummary.csv";
        static string RefGenomeSequence = new string('A', 100);

        /// <summary>
        /// Static variable requisite for data-driven Theory setup. 
        /// </summary>
        public static IEnumerable<object[]> ScenarioData
        {
            get
            {
                string inputDirectory = TestPaths.SharedStitcherData;

                if (!Directory.Exists(OutputFolder))
                    Directory.CreateDirectory(OutputFolder);

                var loadingResultsSummary = Path.Combine(OutputFolder, LoadingSummaryFileName);
                var biasResultsSummary = Path.Combine(OutputFolder, StrandBiasSummaryFileName);

                using (StreamWriter sw = new StreamWriter(new FileStream(loadingResultsSummary, FileMode.OpenOrCreate)))
                {
                    sw.WriteLine(string.Join(",", "day", "time", "scenario", "ID",
                        "expectedVarLoading", "variantReadLoadResult",
                        "expectedRefLoading", "refReadLoadResult",
                        "expectedVarCallDirection", "VarCallDirectionResult",
                        "expectedRefCallDirection", "RefCallDirectionResult", "Agreement?"));
                }


                using (StreamWriter sw1 = new StreamWriter(new FileStream(biasResultsSummary, FileMode.OpenOrCreate)))
                {
                    sw1.WriteLine(string.Join(",", "day", "time", "scenario", "ID",
                        "expectedNumVariants", "numVariantsDetected",
                        "expectedFreq", "observedFreq",
                        "expectedSB", "observedSB", "Agreement?"));
                }

                return StitchingScenarioParser.ParseScenariosFromDirectory(inputDirectory, "Stitching_Results.csv");
            }
        }


        public static ChrReference ChrInfo
        {
            get
            {
                return (new ChrReference()
                {
                    Name = "chr7",
                    Sequence = RefGenomeSequence
                });
            }

        }

        public static PiscesApplicationOptions Options
        {
            get
            {
                return (new PiscesApplicationOptions()
                {
                    BAMPaths = new[] { string.Empty },
                    GenomePaths = new[] { GenomeChr19 },
                    OutputDirectory = Path.Combine(TestPaths.LocalTestDataDirectory, "ExcelStitchingTests"),
                    CallMNVs = true,
                    MaxSizeMNV = 6,
                    MaxGapBetweenMNV = 1,
                    Collapse = true,
                    OutputBiasFiles = true,
                    VcfWritingParameters = new Domain.Options.VcfWritingParameters()
                    { OutputGvcfFile = true }
                });

            }
        }


        //this test checks that the stitched variant read and reference read
        //load appropriately into the statemanager (for coverage counting)
        //and the candidate finder can find the variant, and assigns it the right direction.
        [Theory, MemberData("ScenarioData")]
        public void TestReadLoadingOnStitchingScenarios(StitchingScenario scenario, string resultFile)
        {

            //limit the scope of concern for now.
            if (scenario.ShouldStitch != true)
                return;

            //limit the scope of concern for now.
            if (scenario.ShouldRefStitch != true)
                return;

            var resultsSummary = Path.Combine(Options.OutputDirectory, LoadingSummaryFileName);
            using (StreamWriter sw = new StreamWriter(new FileStream(resultsSummary, FileMode.OpenOrCreate)))
            {
                var day = DateTime.Now.ToString("d"); //.net core
                var time = DateTime.Now.ToString("t"); //.net core


                var sb = new StringBuilder(
                    string.Join(",", day,time,
                    scenario.Category, scenario.Id));

                // If it was supposed to stitch, there should only be one read in the output reads, and it's the stitched one.

                var factory = new AmpliconTestFactory(RefGenomeSequence);

                try
                {
                    byte qualityForAll = 30;

                    var MNVdata = StageMNVdata(scenario);

                    var varRead = BuildRead(scenario.OutputRead1, qualityForAll, MNVdata);
                    var refRead = BuildRead(scenario.OutputRefRead1, qualityForAll, NoMNVdata(scenario));

                    if ((varRead == null) || (refRead == null))
                    {
                        //total fail.
                        sb.Append(",total fail to build reads");
                        sw.WriteLine(sb.ToString());
                        return;
                    }

                    //check the reads all loaded with the right counts into the right directions.
                    var varLoadingResults = StitchedReadLoadingHelper.CheckReadLoading(varRead, Options, ChrInfo, true, scenario);
                    if (varLoadingResults.Length == 1)
                    {
                        //total fail. get error msg
                        sb.Append("," + varLoadingResults[0]);
                        sw.WriteLine(sb.ToString());
                        return;
                    }

                    var refLoadingResults = StitchedReadLoadingHelper.CheckReadLoading(refRead, Options, ChrInfo, false, scenario);
                    if (refLoadingResults.Length == 1)
                    {
                        //total fail. get error msg
                        sb.Append("," + refLoadingResults[0]);
                        sw.WriteLine(sb.ToString());
                        return;
                    }


                    List<string> expectedValues = new List<string>()
                    { scenario.VarLoading,scenario.RefLoading,scenario.CandidateDirection,  "0"};

                    List<string> observedValues = new List<string>()
                    { varLoadingResults[0],refLoadingResults[0],varLoadingResults[1],refLoadingResults[1]};

                    sb.Append(GetResultString(expectedValues, observedValues));

                    sw.WriteLine(sb.ToString());
                }
                catch (Exception ex)
                {
                    sb.Append(",Fail:  " + ex);
                    sw.WriteLine(sb.ToString());
                }
            }
        }

        [Theory, MemberData("ScenarioData")]
        public void TestForStrandBiasOnStitchingScenarios(StitchingScenario scenario, string resultFile)
        {


            //limit the scope of concern for now.
            if (scenario.ShouldRefStitch != true)
                return;


            //limit the scope of concern for now.
            if (scenario.ShouldStitch != true)
                return;

            var resultsSummary = Path.Combine(Options.OutputDirectory, StrandBiasSummaryFileName);
            using (StreamWriter sw = new StreamWriter(new FileStream(resultsSummary, FileMode.OpenOrCreate))) 
            {
                var day = DateTime.Now.ToString("d"); //.net core
                var time = DateTime.Now.ToString("t"); //.net core

                var sb = new StringBuilder(
                    string.Join(",", day,time,
                    scenario.Category, scenario.Id));

                try
                {

                    if (!Directory.Exists(Options.OutputDirectory))
                        Directory.CreateDirectory(Options.OutputDirectory);


                    var factory = new AmpliconTestFactory(new string('A', 100), sourceIsStitched: true);

                    byte qualityForAll = 30;
                    int numVariantCounts = 2;// 10;
                    int numReferenceCounts = 2; // 90;
                    var varRead = BuildRead(scenario.OutputRead1, qualityForAll, StageMNVdata(scenario));
                    var refRead = BuildRead(scenario.OutputRefRead1, qualityForAll, NoMNVdata(scenario));

                    if (refRead == null)
                        return;

                    factory.StageStitchedVariant(
                          varRead, numVariantCounts,
                          refRead, numReferenceCounts);

                    var outputFileName = string.Format("{0}_{1}.vcf", scenario.Category, scenario.Id);
                    var vcfOutputPath = Path.Combine(Options.OutputDirectory, outputFileName);
                    var biasOutputPath = StrandBiasFileWriter.GetBiasFilePath(vcfOutputPath);

                    File.Delete(vcfOutputPath);
                    File.Delete(biasOutputPath);

                    StitchedReadBiasHelper.CallStrandedVariantsWithMockData(vcfOutputPath, Options, factory);
                    var varResults = StitchedReadBiasHelper.GetResults(VcfReader.GetAllVariantsInFile(vcfOutputPath));
                    var biasResults = StitchedReadBiasHelper.GetStrandResultsFromFile(biasOutputPath);

                    var observedFrequency = (varResults.Count == 0) ? "0" : "";
                    var observedSB = (biasResults.Count == 0) ? "FN" : "";

                    for (int i = 0; i < varResults.Count; i++)
                    {
                        var varResult = varResults[i];
                        if (i != 0)
                            observedFrequency += ";";
                        observedFrequency += varResult.VariantFrequency;
                    }

                    for (int i = 0; i < biasResults.Count; i++)
                    {
                        var biasResult = biasResults[i];
                        if (i != 0)
                            observedSB += ";";
                        observedSB += biasResult.HasStrandBias;

                        //there should be no SB on our current set of stitched scenarios.
                        Assert.True(!biasResult.HasStrandBias);
                    }

                    var expectedValues = new List<string>()
                    { "1",scenario.Frequency,scenario.ShouldBias};

                    var observedValues = new List<string>()
                    { varResults.Count.ToString(),observedFrequency,observedSB};

                    sb.Append(GetResultString(expectedValues, observedValues));

                    sw.WriteLine(sb.ToString());
                }
                catch (Exception ex)
                {
                    sb.Append(",Fail:  " + ex);
                    sw.WriteLine(sb.ToString());
                }
            }

        }

        private static Tuple<int, int> NoMNVdata(StitchingScenario scenario)
        {
            return (Tuple.Create(-1, 0));
        }

        private static Tuple<int, int> StageMNVdata(StitchingScenario scenario)
        {
            int MNVPosition = 0;
            int MNVLength = 0;
            if (scenario.Category.ToLower().Contains("mnv"))
            {
                MNVPosition = int.Parse(scenario.VarLoading.Substring(0, 1));
                MNVLength = scenario.VarLoading.Length - 1;
            }

            return (Tuple.Create(MNVPosition, MNVLength));
        }

        private static BamAlignment BuildRead(AbstractAlignment alignment,
            byte qualityForAll, Tuple<int, int> MNVdata)
        {

            int MNVPosition = MNVdata.Item1;
            int MNVLength = MNVdata.Item2;

            try
            {
                var ca = new CigarAlignment(alignment.Cigar);
                int readLength = (int)ca.GetReadSpan();


                string readSequence = new string('A', readLength); //originalAlignment.Sequence;

                if (MNVLength > 0)
                {
                    readSequence = new string('A', MNVPosition - 1);
                    readSequence += new string('G', MNVLength);
                    readSequence += new string('A', readLength - readSequence.Length);
                }


                var varTagUtils = new TagUtils();
                varTagUtils.AddStringTag("XD", alignment.Directions);

                var varRead = new BamAlignment()
                {
                    RefID = 1,
                    Position = alignment.Position - 1,
                    CigarData = ca,
                    Bases = readSequence,
                    TagData = varTagUtils.ToBytes(),
                    Qualities = Enumerable.Repeat(qualityForAll, readLength).ToArray(),
                    MapQuality = 50
                };
                return varRead;
            }
            catch
            {
                return null;
            }
        }


        private static bool CheckForAgreement(List<string> expectedValues, List<string> observedValues)
        {
            for (int i = 0; i < expectedValues.Count; i++)
            {
                if (expectedValues[i].ToLower() != observedValues[i].ToLower())
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetResultString(List<string> expectedValues, List<string> observedValues)
        {
            var sb = new StringBuilder(",");

            for (int i = 0; i < expectedValues.Count; i++)
            {
                sb.Append(expectedValues[i] + "," + observedValues[i] + ",");
            }

            sb.Append(CheckForAgreement(expectedValues, observedValues));

            return sb.ToString();
        }

    }
}