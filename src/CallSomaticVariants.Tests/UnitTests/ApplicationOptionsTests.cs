using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;
using Constants = CallSomaticVariants.Types.Constants;

namespace CallSomaticVariants.Tests.ApplicationOptionsTests
{
    public class ApplicationOptionsTests
    {
        private string _existingBamPath = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35.bam");
        private string _existingBamPath2 = Path.Combine(UnitTestPaths.TestDataDirectory, "var123var35_removedSQlines.bam");
        private string _existingGenome = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr19");
        private string _existingInterval = Path.Combine(UnitTestPaths.TestDataDirectory, "chr17only.picard");

        private Dictionary<string, Action<ApplicationOptions>> GetOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ApplicationOptions>>();

            optionsExpectationsDict.Add("-a 40", (o) => Assert.Equal(40, o.MinimumVariantQScore));
            optionsExpectationsDict.Add("-b 40", (o) => Assert.Equal(40, o.MinimumBaseCallQuality));
            optionsExpectationsDict.Add(@"-B C:\test.bam,C:\test2.bam", (o) => Assert.Equal(2, o.BAMPaths.Length));
            optionsExpectationsDict.Add("-c 40", (o) => Assert.Equal(40, o.MinimumCoverage));
            optionsExpectationsDict.Add("-d true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.True(o.DebugMode));
            optionsExpectationsDict.Add("-f 0.555", (o) => Assert.Equal(0.555f, o.MinimumFrequency));
            optionsExpectationsDict.Add("-F 40", (o) => Assert.Equal(40, o.FilteredVariantQScore));
            optionsExpectationsDict.Add("-fo true", (o) => Assert.True(o.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add(@"-g C:\genome,C:\genome2", (o) => Assert.Equal(2, o.GenomePaths.Length));
            optionsExpectationsDict.Add("-NL 40", (o) => Assert.Equal(40, o.AppliedNoiseLevel));
            optionsExpectationsDict.Add("-gVCF true", (o) => Assert.True(o.OutputgVCFFiles));
            optionsExpectationsDict.Add("-CallMNVs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-PhaseSNPs true", (o) => Assert.True(o.CallMNVs));
            optionsExpectationsDict.Add("-MaxMNVLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxPhaseSNPLength 40", (o) => Assert.Equal(40, o.MaxSizeMNV));
            optionsExpectationsDict.Add("-MaxGapBetweenMNV 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add("-MaxGapPhasedSNP 40", (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            optionsExpectationsDict.Add(@"-i C:\blah,C:\blah2", (o) => Assert.Equal(2, o.IntervalPaths.Length));
            optionsExpectationsDict.Add("-m 40", (o) => Assert.Equal(40, o.MinimumMapQuality));
            optionsExpectationsDict.Add("-GT threshold", (o) => Assert.Equal(GenotypeModel.Thresholding, o.GTModel));
            optionsExpectationsDict.Add("-SBModel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            optionsExpectationsDict.Add("-o true", (o) => Assert.True(o.OutputBiasFiles));
            optionsExpectationsDict.Add("-p true", (o) => Assert.True(o.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-q 40", (o) => Assert.Equal(40, o.MaximumVariantQScore));
            optionsExpectationsDict.Add("-s 0.7", (o) => Assert.Equal(0.7f, o.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-t 40", (o) => Assert.Equal(40, o.MaxNumThreads));
            optionsExpectationsDict.Add("-ThreadByChr true", (o) => Assert.True(o.ThreadByChr));
            optionsExpectationsDict.Add("-StitchPairedReads false", (o) => Assert.False(o.StitchReads));
            optionsExpectationsDict.Add("-ReportNoCalls true", (o) => Assert.True(o.ReportNoCalls));
            optionsExpectationsDict.Add(@"-OutFolder C:\out", (o) => Assert.Equal(@"C:\out", o.OutputFolder));

            return optionsExpectationsDict;
        }
            
        [Fact]
        [Trait("ReqID","SDS-1")]
        public void CommandLineWhitespaceParse()
        {
            var optionsExpectations = GetOptionsExpectations();
            Action<ApplicationOptions> expectations = null;
            foreach (var option in optionsExpectations.Values)
            {
                expectations += option;
            }

            //Test with multiple options strung together by spaces.
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys), true, expectations);
           
            //Different separator shouldn't work
            ExecuteParsingTest(string.Join(";", optionsExpectations.Keys), false, expectations);
           
            //Order shouldn't matter
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderByDescending(o=>o)), true, expectations);
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderBy(o => o)), true, expectations);

        }


        [Fact]
        [Trait("ReqID","SDS-2")]
        public void CommandLineParsing()
        {

            ExecuteParsingTest("-a 40", true, (o) => Assert.Equal(40, o.MinimumVariantQScore));
            ExecuteParsingTest("-b 40", true, (o) => Assert.Equal(40, o.MinimumBaseCallQuality));
            ExecuteParsingTest(@"-B C:\test.bam,C:\test2.bam", true, (o) => Assert.Equal(2, o.BAMPaths.Length));
            ExecuteParsingTest("-c 40", true, (o) => Assert.Equal(40, o.MinimumCoverage));
            ExecuteParsingTest("-d true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingTest("-debug true", true, (o) => Assert.True(o.DebugMode));
            ExecuteParsingTest("-f 0.555", true, (o) => Assert.Equal(0.555f, o.MinimumFrequency));
            ExecuteParsingTest("-F 40", true, (o) => Assert.Equal(40, o.FilteredVariantQScore));
            ExecuteParsingTest("-fo true", true, (o) => Assert.True(o.FilterOutVariantsPresentOnlyOneStrand));
            ExecuteParsingTest(@"-g C:\genome,C:\genome2", true, (o) => Assert.Equal(2, o.GenomePaths.Length));
            ExecuteParsingTest("-NL 40", true, (o) => Assert.Equal(40, o.AppliedNoiseLevel));
            ExecuteParsingTest("-gVCF true", true, (o) => Assert.True(o.OutputgVCFFiles));
            ExecuteParsingTest("-CallMNVs true", true, (o) => Assert.True(o.CallMNVs));
            ExecuteParsingTest("-PhaseSNPs true", true, (o) => Assert.True(o.CallMNVs));
            ExecuteParsingTest("-MaxMNVLength 40", true, (o) => Assert.Equal(40, o.MaxSizeMNV));
            ExecuteParsingTest("-MaxPhaseSNPLength 40", true, (o) => Assert.Equal(40, o.MaxSizeMNV));
            ExecuteParsingTest("-MaxGapBetweenMNV 40", true, (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            ExecuteParsingTest("-MaxGapPhasedSNP 40", true, (o) => Assert.Equal(40, o.MaxGapBetweenMNV));
            ExecuteParsingTest(@"-i C:\blah,C:\blah2", true, (o) => Assert.Equal(2, o.IntervalPaths.Length));
            ExecuteParsingTest("-m 40", true, (o) => Assert.Equal(40, o.MinimumMapQuality));
            ExecuteParsingTest("-GT threshold", true, (o) => Assert.Equal(GenotypeModel.Thresholding, o.GTModel));
            ExecuteParsingTest("-SBModel poisson", true, (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            ExecuteParsingTest("-o true", true, (o) => Assert.True(o.OutputBiasFiles));
            ExecuteParsingTest("-p true", true, (o) => Assert.True(o.OnlyUseProperPairs));
            ExecuteParsingTest("-q 40", true, (o) => Assert.Equal(40, o.MaximumVariantQScore));
            ExecuteParsingTest("-s 0.7", true, (o) => Assert.Equal(0.7f, o.StrandBiasAcceptanceCriteria));
            ExecuteParsingTest("-t 40", true, (o) => Assert.Equal(40, o.MaxNumThreads));
            ExecuteParsingTest("-ThreadByChr true", true, (o) => Assert.True(o.ThreadByChr));
            ExecuteParsingTest("-StitchPairedReads false", true, (o) => Assert.False(o.StitchReads));
            ExecuteParsingTest("-ReportNoCalls true", true, (o) => Assert.True(o.ReportNoCalls));
            ExecuteParsingTest(@"-OutFolder C:\out", true, (o) => Assert.Equal(@"C:\out", o.OutputFolder));
        }

        [Fact]
        [Trait("ReqID", "SDS-3")]
        public void CommandLineParsing_Errors()
        {
            // focus on mal-formed arguments

            // argument mismatches
            ExecuteParsingTest("-a", false);
            ExecuteParsingTest("-a 20 -b", false);
            ExecuteParsingTest("a 20", false);
            ExecuteParsingTest("-unknown", false);  

            // enum values
            ExecuteParsingTest("-GT help", false);
            ExecuteParsingTest("-SBModel bogus", false);  
        }

        [Fact]
        [Trait("ReqID", "SDS-4")]
        public void NumberOfBams()
        {
            var bams = new List<string>();
            var genomes = new List<string>();

            try
            {
                //0 bams - should error
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] {}; }, false);
                //1 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = new string[] {_existingBamPath}; }, true);

                for (int i = 0; i < 97; i++)
                {
                    var bam = "BAM_" + i + ".bam";
                    var genome = "Genome_" + i + ".bam";
                    bams.Add(bam);
                    genomes.Add(genome);
                    using (File.Create(bam))
                    {
                    }
                    using (File.Create(genome))
                    {
                    }
                }

                //96 bams - should be ok
                ExecuteValidationTest((o) => { o.BAMPaths = bams.Take(96).ToArray(); }, true);

                //97 bams - should error
                ExecuteValidationTest((o) => { o.BAMPaths = bams.ToArray(); }, false);

            }
            finally
            {
                foreach (var bam in bams)
                {
                    File.Delete(bam);
                }
                foreach (var genome in genomes)
                {
                    File.Delete(genome);
                }                
            }
        }

        [Fact]
        [Trait("ReqID","SDS-3")]
        [Trait("ReqID", "SDS-56")]
        public void Validate()
        {
            // ---------------------
            // verify default should be valid
            // ---------------------
            var option = GetBasicOptions();
            option.Validate();

            // ---------------------------------------------------
            // Either BAMPath(s) or BAMFolder should be specified.
            // ---------------------------------------------------
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new []{"bampath1.bam"};
                o.BAMFolder = @"C:\BAMFolder";
            }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = null; }, false);

            // ---------------------------------------------------
            // BAMFolder
            // Folder should exist
            // 1 Genome Path should be specified when BAMFolder is specified.
            // Atmost 1 Interval Path should be specified when BAMFolder is specified.
            // Threading by chromosome is not supported when BAMFolder is specified.
            // ---------------------------------------------------
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = @"C:\NonexistantBAMFolder";
                o.GenomePaths = new[] {_existingGenome};
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { "folder1","folder2" };
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] {"file1.intervals", "file2.intervals"};
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { _existingGenome };
                o.ThreadByChr = true;
            }, false);
            ExecuteValidationTest(o =>
            {
                o.BAMPaths = null;
                o.BAMFolder = UnitTestPaths.TestDataDirectory;
                o.GenomePaths = new[] { _existingGenome };
                o.IntervalPaths = new[] { _existingInterval };
            }, true);

            // ---------------------
            // BAM Paths
            // Duplicate BAMPaths detected.
            // BAM Path does not exist.
            // Threading by chromosome is only supported for single BAMPath input.
            // ---------------------
            ExecuteValidationTest((o) => { o.BAMPaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] {_existingBamPath, _existingBamPath}; }, false);
            ExecuteValidationTest((o) => { o.BAMPaths = new[] { "nonexistant.bam" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.ThreadByChr = true;
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
            }, false);
            ExecuteValidationTest((o) => { o.ThreadByChr = true; }, true);

            // genome paths
            ExecuteValidationTest((o) => { o.GenomePaths = null; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new string[0]; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { _existingGenome, _existingGenome }; }, false);
            ExecuteValidationTest((o) => { o.GenomePaths = new[] { "nonexistant" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] {_existingBamPath, _existingBamPath2};
                o.GenomePaths = new[] {_existingGenome, _existingGenome};
            }, true);  // dup genomes ok

            // intervals
            ExecuteValidationTest((o) => { o.IntervalPaths = new[] { _existingInterval, _existingInterval }; }, false);
            ExecuteValidationTest((o) => { o.IntervalPaths = new[] { "nonexistant.picard" }; }, false);
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
                o.IntervalPaths = new[] { _existingInterval, _existingInterval };
            }, true);  // dup intervals ok
            ExecuteValidationTest((o) =>
            {
                o.BAMPaths = new[] { _existingBamPath, _existingBamPath2 };
                o.IntervalPaths = new[] { _existingInterval, _existingInterval, _existingInterval };
            }, false);

            // ---------------------
            // verify parameters
            // ---------------------
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = 0; }, true);
            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 100;
                o.FilteredVariantQScore = 100;
            }, true);
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.MinimumVariantQScore = 101; }, false);

            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 0;
                                             o.MinimumVariantQScore = 0;
                                             o.FilteredVariantQScore = 0;
            }, true);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 100; }, true);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = -1; }, false);
            ExecuteValidationTest((o) => { o.MaximumVariantQScore = 101; }, false);

            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 50;
                o.MaximumVariantQScore = 49;
            }, false);

            ExecuteValidationTest((o) =>
            {
                o.MinimumVariantQScore = 50;
                o.MaximumVariantQScore = 50;
                o.FilteredVariantQScore = 50;
            }, true);

            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.MinimumBaseCallQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.MinimumFrequency = 0f; }, true);
            ExecuteValidationTest((o) => { o.MinimumFrequency = 1f; }, true);
            ExecuteValidationTest((o) => { o.MinimumFrequency = -0.99f; }, false);
            ExecuteValidationTest((o) => { o.MinimumFrequency = 1.01f; }, false);

            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MinimumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MaximumVariantQScore; }, true);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MinimumVariantQScore - 1; }, false);
            ExecuteValidationTest((o) => { o.FilteredVariantQScore = o.MaximumVariantQScore + 1; }, false);

            ExecuteValidationTest((o) => { o.AppliedNoiseLevel = 0; }, true);
            ExecuteValidationTest((o) => { o.AppliedNoiseLevel = -50; }, false);

            ExecuteValidationTest((o) => { o.MaxSizeMNV = 0; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.MaxGapBetweenMNV = -1; }, true);  // ok if not calling mnv
            ExecuteValidationTest((o) => { o.OutputFolder = UnitTestPaths.TestDataDirectory; }, true); // True for valid path
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = 1;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = 0;
            }, false);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = Constants.RegionSize;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxSizeMNV = Constants.RegionSize + 1;
            }, false);

            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxGapBetweenMNV = 0;
            }, true);
            ExecuteValidationTest((o) =>
            {
                o.CallMNVs = true;
                o.MaxGapBetweenMNV = -1;
            }, false);

            ExecuteValidationTest((o) => { o.MinimumMapQuality = 0; }, true);
            ExecuteValidationTest((o) => { o.MinimumMapQuality = -1; }, false);

            ExecuteValidationTest((o) => { o.StrandBiasAcceptanceCriteria = 0f; }, true);
            ExecuteValidationTest((o) => { o.StrandBiasAcceptanceCriteria = -0.01f; }, false);

            ExecuteValidationTest((o) => { o.MaxNumThreads = 1; }, true);
            ExecuteValidationTest((o) => { o.MaxNumThreads =  0; }, false);
        }

        [Fact]
        public void PopulateBAMPaths()
        {
            //Happy Path
            var options_1 = new ApplicationOptions()
            {
                BAMFolder = UnitTestPaths.TestDataDirectory,
                GenomePaths = new[] {_existingGenome},
                IntervalPaths = new[] {_existingInterval}
            };
            options_1.PopulateBAMPaths();
            Assert.NotNull(options_1.BAMPaths);
            Assert.True(options_1.BAMPaths.Length > 0);
            
            //no bam files found
            var options_3 = new ApplicationOptions()
            {
                BAMFolder = UnitTestPaths.TestGenomesDirectory,
                GenomePaths = new[] { _existingGenome },
            };
            Assert.Throws<ArgumentException>(() => options_3.PopulateBAMPaths());
        }

        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<ApplicationOptions> assertions = null)
        {
            var options = new ApplicationOptions();

            if (shouldPass)
            {
                options.UpdateOptions(arguments.Split(' '));
                if (assertions != null)
                    assertions(options);
            }
            else
            {
                Assert.Throws<Exception>(() => options.UpdateOptions(arguments.Split(' ')));
            }
        }

        private void ExecuteValidationTest(Action<ApplicationOptions> testSetup, bool shouldPass)
        {
            var options = GetBasicOptions();

            testSetup(options);

            if (shouldPass)
                options.Validate();
            else
            {
                Assert.Throws<ArgumentException>(() => options.Validate());
            }
        }

        private ApplicationOptions GetBasicOptions()
        {
            return new ApplicationOptions()
            {
                BAMPaths = new[] {_existingBamPath},
                GenomePaths = new[] {_existingGenome}
            };
        }
    }
}
