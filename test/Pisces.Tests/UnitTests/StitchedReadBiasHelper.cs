using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Pisces.Domain.Options;
using Pisces.Interfaces;
using Pisces.Logic;
using Pisces.Logic.Alignment;
using Pisces.Logic.VariantCalling;
using Pisces.Tests.MockBehaviors;
using Pisces.IO.Sequencing;
using TestUtilities.MockBehaviors;
using Pisces.Calculators;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.Processing.RegionState;
using Xunit;
using Xunit.Extensions;

namespace Pisces.Tests.UnitTests
{

    public class BiasTestResult
    {
        public int Position { get; set; }
        public string ReferenceAllele { get; set; }
        public string AlternateAllele { get; set; }
        public int OverallSupport { get; set; }  //Overall_Support,
        public int OverallCoverage { get; set; } //Overall_Coverage
        public int[] FinalFRSupport = new int[] { 0, 0 };  //Forward_Support,Reverse_Support
        public int[] FinalFRCoverage = new int[] { 0, 0 }; //Forward_Coverage,Reverse_Coverage
        public int[] RawFRSSupport = new int[] { 0, 0, 0 }; //RawSupportCountByReadType_0,RawSupportCountByReadType_1,RawSupportCountByReadType_2
        public int[] RawFRSCoverage = new int[] { 0, 0, 0 }; //RawCoverageCountByReadType_0 ,RawCoverageCountByReadType_1,RawCoverageCountByReadType_2

        public bool HasStrandBias { get; set; } //BiasAcceptable?

        private static string[] _headerString = new string[] {
            "Chr","Position","Reference","Alternate","Overall_ChanceFalsePos","Forward_ChanceFalsePos","Reverse_ChanceFalsePos","Overall_ChanceFalseNeg","Forward_ChanceFalseNeg","Reverse_ChanceFalseNeg","Overall_Freq","Forward_Freq","Reverse_Freq","Overall_Support","Forward_Support","Reverse_Support","Overall_Coverage","Forward_Coverage","Reverse_Coverage","RawCoverageCountByReadType_0","RawCoverageCountByReadType_1","RawCoverageCountByReadType_2","RawSupportCountByReadType_0","RawSupportCountByReadType_1","RawSupportCountByReadType_2","BiasScore","BiasAcceptable?","VarPresentOnBothStrands?","CoverageAvailableOnBothStrands?" };

        public static BiasTestResult ResultFromLine(string line)
        {
            var result = new BiasTestResult();

            string[] splat = line.Split('\t');

            //header line
            if (splat[0] == _headerString[0])
                return null;

            if (splat.Length < _headerString.Length)
            {
                throw new System.Exception("Check strand bias file format matches the unit test.");
            }

            for (int i = 0; i < _headerString.Length; i++)
            {
                string value = splat[i];
                string colHeader = _headerString[i];

                switch (colHeader)
                {
                    case "Position":
                        result.Position = int.Parse(value);
                        break;
                    case "Reference":
                        result.ReferenceAllele = value;
                        break;
                    case "Alternate":
                        result.AlternateAllele = value;
                        break;
                    case "Overall_Support":
                        result.OverallSupport = int.Parse(value);
                        break;
                    case "Overall_Coverage":
                        result.OverallCoverage = int.Parse(value);
                        break;
                    case "Forward_Support":
                        result.FinalFRSupport[0] = int.Parse(value);
                        break;
                    case "Reverse_Support":
                        result.FinalFRSupport[1] = int.Parse(value);
                        break;
                    case "Forward_Coverage":
                        result.FinalFRCoverage[0] = int.Parse(value);
                        break;
                    case "Reverse_Coverage":
                        result.FinalFRCoverage[1] = int.Parse(value);
                        break;
                    case "RawSupportCountByReadType_0":
                        result.RawFRSSupport[0] = int.Parse(value);
                        break;
                    case "RawSupportCountByReadType_1":
                        result.RawFRSSupport[1] = int.Parse(value);
                        break;
                    case "RawSupportCountByReadType_2":
                        result.RawFRSSupport[2] = int.Parse(value);
                        break;
                    case "RawCoverageCountByReadType_0":
                        result.RawFRSCoverage[0] = int.Parse(value);
                        break;
                    case "RawCoverageCountByReadType_1":
                        result.RawFRSCoverage[1] = int.Parse(value);
                        break;
                    case "RawCoverageCountByReadType_2":
                        result.RawFRSCoverage[2] = int.Parse(value);
                        break;
                    case "BiasAcceptable?":
                        result.HasStrandBias = !(bool.Parse(value));
                        break;                 
                    default:
                        break;

                }

            }

            return result;
        }

    }


    public class StitchedReadBiasHelper
    {

        public static List<BiasTestResult> GetStrandResultsFromFile(string biasFile)
        {
            var results = new List<BiasTestResult>();

            if (File.Exists(biasFile))
            {
                using (StreamReader sr = new StreamReader(new FileStream(biasFile, FileMode.Open, FileAccess.Read)))
                {
                    while (true)
                    {
                        string line = sr.ReadLine();

                        if (string.IsNullOrEmpty(line))
                            break;

                        var result = BiasTestResult.ResultFromLine(line);

                        if (result != null)
                            results.Add(result);
                    }
                }
            }

            return results;
        }

        public static void CallStrandedVariantsWithMockData(string vcfOutputPath, PiscesApplicationOptions options, AmpliconTestFactory testFactory)
        {
            var appFactory = new MockFactoryWithDefaults(options);
            using (var vcfWriter = appFactory.CreateVcfWriter(vcfOutputPath, new VcfWriterInputContext()))
            {
                using (var biasWriter = new StrandBiasFileWriter(vcfOutputPath))
                {
                    var svc = CreateMockVariantCaller(vcfWriter, options, testFactory.ChrInfo, testFactory.AlignmentExtractor, biasWriter);
                    vcfWriter.WriteHeader();
                    biasWriter.WriteHeader();
                    svc.Execute();
                    biasWriter.Dispose();
                }
            }
            Assert.True(File.Exists(vcfOutputPath));
        }

        public static ISomaticVariantCaller CreateMockVariantCaller(VcfFileWriter vcfWriter, PiscesApplicationOptions options, ChrReference chrRef, MockAlignmentExtractor mockAlignmentExtractor, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null)
        {
            var config = new AlignmentSourceConfig
            {
                MinimumMapQuality = options.BamFilterParameters.MinimumMapQuality,
                OnlyUseProperPairs = options.BamFilterParameters.OnlyUseProperPairs,
                SkipDuplicates = options.BamFilterParameters.RemoveDuplicates
            };

            AlignmentMateFinder mateFinder = null;
            var alignmentSource = new AlignmentSource(mockAlignmentExtractor, mateFinder, config);
            var variantFinder = new CandidateVariantFinder(options.BamFilterParameters.MinimumBaseCallQuality, options.MaxSizeMNV, options.MaxGapBetweenMNV, options.CallMNVs);
            var coverageCalculator = new CoverageCalculator();

            var alleleCaller = new AlleleCaller(new VariantCallerConfig
            {
                IncludeReferenceCalls = options.VcfWritingParameters.OutputGvcfFile,
                MinVariantQscore = options.VariantCallingParameters.MinimumVariantQScore,
                MaxVariantQscore = options.VariantCallingParameters.MaximumVariantQScore,
                VariantQscoreFilterThreshold = options.VariantCallingParameters.MinimumVariantQScoreFilter > options.VariantCallingParameters.MinimumVariantQScore ? options.VariantCallingParameters.MinimumVariantQScoreFilter : (int?)null,
                MinCoverage = options.VariantCallingParameters.MinimumCoverage,
                MinFrequency = options.VariantCallingParameters.MinimumFrequency,
                NoiseLevelUsedForQScoring = options.VariantCallingParameters.NoiseLevelUsedForQScoring,
                StrandBiasModel = options.VariantCallingParameters.StrandBiasModel,
                StrandBiasFilterThreshold = options.VariantCallingParameters.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = options.VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand,
                ChrReference = chrRef
            },
            coverageCalculator: coverageCalculator,
            variantCollapser: options.Collapse ? new VariantCollapser(null, coverageCalculator) : null);

            var stateManager = new RegionStateManager(
                expectStitchedReads: mockAlignmentExtractor.SourceIsStitched,
                trackOpenEnded: options.Collapse, trackReadSummaries: options.CoverageMethod == CoverageMethod.Approximate);

            //statmanager is an allele source
            Assert.Equal(0, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));


            return new SomaticVariantCaller(
                alignmentSource,
                variantFinder,
                alleleCaller,
                vcfWriter,
                stateManager,
                chrRef,
                null,
                biasFileWriter);
        }

        public static List<AmpliconTestResult> GetResults(List<VcfVariant> vcfResults)
        {
            var results = new List<AmpliconTestResult>();

            var allVariants = vcfResults.Where(x => x.VariantAlleles[0] != ".");
            foreach (var variant in allVariants)
            {
                var result = new AmpliconTestResult()
                {
                    Position = variant.ReferencePosition,
                    ReferenceAllele = variant.ReferenceAllele,
                    VariantAllele = variant.VariantAlleles[0]
                };

                var genotype = variant.Genotypes[0];
                result.VariantFrequency = float.Parse(genotype["VF"]);

                var ADTokens = genotype["AD"].Split(',');
                result.ReferenceDepth = float.Parse(ADTokens[0]);
                result.VariantDepth = float.Parse(ADTokens[1]);
                result.TotalDepth = float.Parse(variant.InfoFields["DP"]);
                result.Filters = variant.Filters;

                results.Add(result);
            }

            return results;
        }


    }

}
