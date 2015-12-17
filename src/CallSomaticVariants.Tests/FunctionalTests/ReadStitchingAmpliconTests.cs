using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Logic.VariantCalling;
using CallSomaticVariants.Models;
using CallSomaticVariants.Test;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using Moq;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.FunctionalTests
{
    public class ReadStitchingAmpliconTests
    {
        private const int MAX_FRAGMENT_SIZE = 10000; // limits window to find mate
        private const int CHR_OFFSET = 3000;
        private readonly string _genomeChr19 = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr19");

        /// <summary>
        /// Insertion is fully contained in overlap
        /// 
        /// WT: read1 and read2 both fully overlap insertion position
        /// Variant: read1 and read2 both fully overlap insertion position
        /// R1 ---------++++---
        /// R2       ---++++---------- 
        /// </summary>
        [Fact]
        [Trait("Category", "ReadStitching")]
        public void Insertion_WTFullOverlap_VarFullOverlap()
        {
            var outputFileName = "Insertion-WTFullOverlap-VarFullOverlap_S1.genome.vcf";

            var test = new AmpliconInsertionTest()
            {
                StitchPairedReads = true,
                RequireXCTagToStitch = true,
                ReadLength = 30,
                ReferenceSequenceRelative = "GTTGGTCTTC" + "TATTTTATGCGAATTCTTCT" + "AAGATTCCCA",
                InsertionSequence = "ATACC",
                VariantPositionRelative = 23,
                VariantDepth = 25,
                ReferenceDepth = 25
            };

            var expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.5f,
                TotalDepth = 50f,
                VariantDepth = 25f,
                ReferenceDepth = 25f,
            };

            ExecuteTest(test, outputFileName, expectedResults);
            test.RequireXCTagToStitch = false;
            ExecuteTest(test, outputFileName, expectedResults);
           
            test.VariantDepth = 60;
            test.ReferenceDepth = 150;
            test.RequireXCTagToStitch = true;
            test.StitchPairedReads = true;
            expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.29f,
                TotalDepth = 210,
                VariantDepth = 60,
                ReferenceDepth = 150,
            };

            ExecuteTest(test, outputFileName, expectedResults);
        }

        /// <summary>
        /// Deletion right before overlap edge
        /// </summary>
        [Fact]
        [Trait("Category", "ReadStitching")]
        public void Deletion_Edge()
        {
            var outputFileName = "Deletion-Edge_S1.genome.vcf";

            var test = new AmpliconDeletionTest()
            {
                StitchPairedReads = true,
                RequireXCTagToStitch = true,
                ReadLength = 30,
                ReferenceSequenceRelative = "GTTGGTCTTC" + "TATTTTATGCGAATTCTTCT" + "AAGATTCCCA",
                VariantPositionRelative = 5,
                NumberDeletedBases = 5,
                VariantDepth = 75,
                ReferenceDepth = 25
            };

            var expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.75f,
                TotalDepth = 100f,
                VariantDepth = 75f,
                ReferenceDepth = 25f
            };

            ExecuteTest(test, outputFileName, expectedResults);
            test.RequireXCTagToStitch = false;
            ExecuteTest(test, outputFileName, expectedResults);
            test.StitchPairedReads = false;
            ExecuteTest(test, outputFileName, expectedResults);

            // change vaf and other side
            test.VariantDepth = 10;
            test.ReferenceDepth = 40;
            test.VariantPositionRelative = 30;
            test.RequireXCTagToStitch = true;
            expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.2f,
                TotalDepth = 50,
                VariantDepth = 10f,
                ReferenceDepth = 40f
            };

            ExecuteTest(test, outputFileName, expectedResults);
        }

        /// <summary>
        /// Deletion within overlap region
        /// </summary>
        [Fact]
        [Trait("Category", "ReadStitching")]
        public void Deletion_FullOverlap()
        {
            var outputFileName = "Deletion-FullOverlap_S1.genome.vcf";

            var test = new AmpliconDeletionTest()
            {
                StitchPairedReads = true,
                RequireXCTagToStitch = true,
                ReadLength = 30,
                ReferenceSequenceRelative = "GTTGGTCTTC" + "TATTTTATGCGAATTCTTCT" + "AAGATTCCCA",
                VariantPositionRelative = 15,
                NumberDeletedBases = 5,
                VariantDepth = 25,
                ReferenceDepth = 75
            };

            var expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.25f,
                TotalDepth = 100f,
                VariantDepth = 25f,
                ReferenceDepth = 75f
            };

            ExecuteTest(test, outputFileName, expectedResults);
            test.RequireXCTagToStitch = false;
            ExecuteTest(test, outputFileName, expectedResults);
            
            // change vaf 
            test.VariantDepth = 10;
            test.ReferenceDepth = 90;
            test.RequireXCTagToStitch = true;
            test.StitchPairedReads = true;
            expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.1f,
                TotalDepth = 100,
                VariantDepth = 10,
                ReferenceDepth = 90
            };

            ExecuteTest(test, outputFileName, expectedResults);
        }

        /// <summary>
        /// MNV right before overlap edge
        /// </summary>
        [Fact]
        [Trait("Category", "ReadStitching")]
        public void MNV_Edge()
        {
            var outputFileName = "Mnv-Edge_S1.genome.vcf";

            var test = new AmpliconMnvTest()
            {
                StitchPairedReads = true,
                RequireXCTagToStitch = true,
                ReadLength = 30,
                ReferenceSequenceRelative = "GTTGGTCTAT" + "TATTTTATGCGAATTCTTCT" + "AAGATTCCCA",
                VariantPositionRelative = 9,
                ChangedSequence = "CC",
                VariantDepth = 25,
                ReferenceDepth = 75
            };

            var expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.25f,
                TotalDepth = 100,
                VariantDepth = 25f,
                ReferenceDepth = 75f
            };

            ExecuteTest(test, outputFileName, expectedResults);
            test.RequireXCTagToStitch = false;
            ExecuteTest(test, outputFileName, expectedResults);
            test.StitchPairedReads = false;
            ExecuteTest(test, outputFileName, expectedResults);

            // change vaf
            test.VariantDepth = 10;
            test.ReferenceDepth = 40;
            test.RequireXCTagToStitch = true;
            test.StitchPairedReads = true;
            expectedResults = new AmpliconTestResult()
            {
                VariantFrequency = 0.2f,
                TotalDepth = 50,
                VariantDepth = 10f,
                ReferenceDepth = 40f
            };

            ExecuteTest(test, outputFileName, expectedResults);

            // other side
            test.VariantPositionRelative = 31;
            ExecuteTest(test, outputFileName, expectedResults);
        }

        /// <summary>
        /// MNV within overlap region's edge
        /// </summary>
        //[Fact]
        //[Trait("Category", "ReadStitching")]
        //public void MNV_PartialOverlap()
        //{
        //    var outputFileName = "Mnv-PartialOverlap_S1.genome.vcf";

        //    var test = new AmpliconMnvTest()
        //    {
        //        StitchPairedReads = true,
        //        RequireXCTagToStitch = true,
        //        ReadLength = 30,
        //        ReferenceSequenceRelative = "GTTGGTCTTC" + "TATTTTATGCGAATTCTTCT" + "AAGATTCCCA",
        //        VariantPositionRelative = 9,
        //        ChangedSequence = "AAG",
        //        VariantDepth = 25,
        //        ReferenceDepth = 25
        //    };

        //    var expectedResults = new AmpliconTestResult()
        //    {
        //        VariantFrequency = 0.5f,
        //        TotalDepth = 50,
        //        VariantDepth = 25,
        //        ReferenceDepth = 25
        //    };

        //    ExecuteTest(test, outputFileName, expectedResults);
        //    test.RequireXCTagToStitch = false;
        //    ExecuteTest(test, outputFileName, expectedResults);

        //    // Note: don't test without stitch reads
        //    // this test would fail because the two reads would detect different variants
        //    // test.StitchPairedReads = false;
        //    // ExecuteTest(test, outputFileName, expectedResults);

        //    test.VariantDepth = 10;
        //    test.ReferenceDepth = 90;
        //    test.RequireXCTagToStitch = true;

        //    expectedResults = new AmpliconTestResult()
        //    {
        //        VariantFrequency = 0.1f,
        //        TotalDepth = 100,
        //        VariantDepth = 10,
        //        ReferenceDepth = 90
        //    };

        //    ExecuteTest(test, outputFileName, expectedResults);
        //    test.RequireXCTagToStitch = false;
        //    ExecuteTest(test, outputFileName, expectedResults);
        //}

        #region Helpers
        private void ExecuteTest(AmpliconTest test, string outputFileName, AmpliconTestResult expectedResult)
        {
            var appOptions = new ApplicationOptions()
            {
                BAMPaths = new[] {string.Empty},
                GenomePaths = new[] {_genomeChr19},
                OutputFolder = UnitTestPaths.TestDataDirectory,
                OutputgVCFFiles = true,
                StitchReads = test.StitchPairedReads,
                RequireXCTagToStitch = test.RequireXCTagToStitch,
                CallMNVs = true,
                MaxSizeMNV = 3,
                MaxGapBetweenMNV = 1
            };

            var vcfOutputPath = Path.Combine(appOptions.OutputFolder, outputFileName);
            File.Delete(vcfOutputPath);

            test.ChrOffset = CHR_OFFSET;

            // test execution
            var factory = new AmpliconTestFactory(test.ReferenceSequenceAbsolute, test.StitchPairedReads);
            factory.ChrOffset = CHR_OFFSET;

            if (test is AmpliconInsertionTest)
            {
                factory.StageInsertion(
                    test.ReadLength, 
                    ((AmpliconInsertionTest) test).InsertionSequence,
                    test.VariantPositionAbsolute, 
                    test.VariantDepth, 
                    test.ReferenceDepth);
            }
            else if (test is AmpliconDeletionTest)
            {
                factory.StageDeletion(
                    test.ReadLength, 
                    ((AmpliconDeletionTest) test).NumberDeletedBases,
                    test.VariantPositionAbsolute, 
                    test.VariantDepth, 
                    test.ReferenceDepth);
            }
            else if (test is AmpliconMnvTest)
            {
                factory.StageMnv(
                    test.ReadLength, 
                    ((AmpliconMnvTest) test).ChangedSequence, 
                    test.VariantPositionAbsolute,
                    test.VariantDepth, 
                    test.ReferenceDepth);
            }

            CallVariantsWithMockData(vcfOutputPath, appOptions, factory);

            var results = GetResults(VcfReader.GetAllVariantsInFile(vcfOutputPath));

            Assert.True(results.Count == 1);

            var result = results[0];
            if (test is AmpliconInsertionTest)
            {
                Assert.True(result.VariantAllele.Substring(1) == ((AmpliconInsertionTest) test).InsertionSequence);
            }
            else if (test is AmpliconDeletionTest)
            {
                Assert.True(result.ReferenceAllele.Length == ((AmpliconDeletionTest) test).NumberDeletedBases + 1);
            }
            else if (test is AmpliconMnvTest)
            {
                Assert.True(result.VariantAllele == ((AmpliconMnvTest) test).ChangedSequence);
            }

            Assert.True(result.Filters == "PASS");
            Assert.True(result.Filters.Split(',').Count() == 1);
            Assert.True(result.Position == test.VariantPositionAbsolute);

            VerifyEqual(result.VariantFrequency, expectedResult.VariantFrequency, 0.1f);
            VerifyEqual(result.TotalDepth, expectedResult.TotalDepth, 1f);
            VerifyEqual(result.VariantDepth, expectedResult.VariantDepth, 1f);
            VerifyEqual(result.ReferenceDepth, expectedResult.ReferenceDepth, 1f);
        }

        private void CallVariantsWithMockData(string vcfOutputPath, ApplicationOptions options, AmpliconTestFactory atf)
        {
            var appFactory = new MockFactoryWithDefaults(options);
            using (var vcfWriter = appFactory.CreateVcfWriter(vcfOutputPath, new VcfWriterInputContext()))
            {
                var svc = CreateMockVariantCaller(vcfWriter, options, atf.ChrInfo, atf._MAE);
                vcfWriter.WriteHeader();
                svc.Execute();
            }

            Assert.True(File.Exists(vcfOutputPath));
        }

        private ISomaticVariantCaller CreateMockVariantCaller(VcfFileWriter vcfWriter, ApplicationOptions options, ChrReference chrRef, MockAlignmentExtractor mae, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null)
        {
            var config = new AlignmentSourceConfig
            {
                MinimumMapQuality = options.MinimumMapQuality,
                OnlyUseProperPairs = options.OnlyUseProperPairs,
            };

            IAlignmentStitcher stitcher = null;
            if (options.StitchReads)
            {
                if (options.UseXCStitcher)
                {
                    stitcher = new XCStitcher(options.MinimumBaseCallQuality);
                }
                else
                {
                    stitcher = new BasicStitcher(options.MinimumBaseCallQuality);
                }
            }

            var mateFinder = options.StitchReads ? new AlignmentMateFinder(MAX_FRAGMENT_SIZE) : null;
            var RegionPadder = new RegionPadder(chrRef, null);
            var alignmentSource = new AlignmentSource(mae, mateFinder, stitcher, config);
            var variantFinder = new CandidateVariantFinder(options.MinimumBaseCallQuality, options.MaxSizeMNV, options.MaxGapBetweenMNV, options.CallMNVs);
            var alleleCaller = new AlleleCaller(new VariantCallerConfig
            {
                IncludeReferenceCalls = options.OutputgVCFFiles,
                MinVariantQscore = options.MinimumVariantQScore,
                MaxVariantQscore = options.MaximumVariantQScore,
                VariantQscoreFilterThreshold = options.FilteredVariantQScore > options.MinimumVariantQScore ? options.FilteredVariantQScore : (int?)null,
                MinCoverage = options.MinimumCoverage,
                MinFrequency = options.MinimumFrequency,
                EstimatedBaseCallQuality = options.AppliedNoiseLevel == -1 ? options.MinimumBaseCallQuality : options.AppliedNoiseLevel,
                StrandBiasModel = options.StrandBiasModel,
                StrandBiasFilterThreshold = options.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = options.FilterOutVariantsPresentOnlyOneStrand,
                GenotypeModel = options.GTModel
            });
            var stateManager = new RegionStateManager();

            return new SomaticVariantCaller(
                alignmentSource,
                variantFinder,
                alleleCaller,
                vcfWriter,
                stateManager,
                chrRef,
                RegionPadder,
                biasFileWriter);
        }

        private List<AmpliconTestResult> GetResults(List<VcfVariant> vcfResults)
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

        private void VerifyEqual(float value1, float value2, float buffer)
        {
            Assert.True(Math.Abs(value1 - value2) < buffer);
        }
        #endregion
    } 
}