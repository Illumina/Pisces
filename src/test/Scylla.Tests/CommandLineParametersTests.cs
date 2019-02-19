using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.Domain.Types;
using VariantPhasing;
using CommandLine.Util;
using Xunit;

namespace Scylla.Tests
{

    public class CommandLineParametersTests
    {
        private string _existingBamPath = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.bam");
        private string _existingVcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "small_S1.genome.vcf");
        private string _testOutputFolder = Path.Combine(TestPaths.LocalScratchDirectory, "ScyllaCommandLineTests");


        private Dictionary<string, Action<ScyllaApplicationOptions>> GetOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ScyllaApplicationOptions>>();

            optionsExpectationsDict.Add(@"-out " + _testOutputFolder, (o) => Assert.Equal(_testOutputFolder, o.OutputDirectory));
            optionsExpectationsDict.Add(@"-bam " + _existingBamPath, (o) => Assert.Equal(_existingBamPath, o.BamPath));
            optionsExpectationsDict.Add(@"-vcf " + _existingVcfPath, (o) => Assert.Equal(_existingVcfPath, o.VcfPath));
            optionsExpectationsDict.Add("-dist 20", (o) => Assert.Equal(20, o.PhasableVariantCriteria.PhasingDistance));
            optionsExpectationsDict.Add("-passingvariantsonly true", (o) => Assert.Equal(true, o.PhasableVariantCriteria.PassingVariantsOnly));
            optionsExpectationsDict.Add("-hetvariantsonly false", (o) => Assert.Equal(false, o.PhasableVariantCriteria.HetVariantsOnly));
            optionsExpectationsDict.Add("-allowclustermerging true", (o) => Assert.Equal(true, o.ClusteringParams.AllowClusterMerging));
            optionsExpectationsDict.Add("-allowworstfitremoval true", (o) => Assert.Equal(true, o.ClusteringParams.AllowWorstFitRemoval));
            optionsExpectationsDict.Add("-debug true", (o) => Assert.Equal(true, o.Debug));
            optionsExpectationsDict.Add("-maxnbhdstoprocess 10", (o) => Assert.Equal(10, o.PhasableVariantCriteria.MaxNumNbhdsToProcess));
            optionsExpectationsDict.Add("-chr [chr1,chr3,]", (o) => Assert.Equal(new[] { "chr1", "chr3" }, o.PhasableVariantCriteria.ChrToProcessArray));
            optionsExpectationsDict.Add("-pp true", (o) => Assert.Equal(true, o.BamFilterParams.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-maxnumthreads 10", (o) => Assert.Equal(10, o.NumThreads));
            optionsExpectationsDict.Add("-minbasecallquality 12", (o) => Assert.Equal(12, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("-minmapquality 52", (o) => Assert.Equal(52, o.BamFilterParams.MinimumMapQuality));
            optionsExpectationsDict.Add("-minvariantqscore 10", (o) => Assert.Equal(10, o.VariantCallingParams.MinimumVariantQScore));
            optionsExpectationsDict.Add("-variantqualityfilter 12", (o) => Assert.Equal(12, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-minimumfrequency 0.001", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequency)); // <- note this will get overwritten with the min diploid frequency if  diploid. *
            optionsExpectationsDict.Add("-minvariantfrequencyfilter 0.056", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequencyFilter));  // <- note this will get overwritten with the min diploid frequency if  diploid.*
            optionsExpectationsDict.Add("-filterduplicates false", (o) => Assert.Equal(false, o.BamFilterParams.RemoveDuplicates));
            optionsExpectationsDict.Add("-ploidy diploid", (o) => Assert.Equal(PloidyModel.DiploidByThresholding, o.VariantCallingParams.PloidyModel));
            optionsExpectationsDict.Add("-clusterconstraint 75", (o) => Assert.Equal(75, o.ClusteringParams.ClusterConstraint));
            optionsExpectationsDict.Add("-g genomePath", (o) => Assert.Equal("genomePath", o.GenomePath));

            //* Happens in SetDerivedValues(); in new OptionSet method, and at the end of the string-parsing code in old (to be removed) method.

            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<ScyllaApplicationOptions>> GetOptionsExpectationsWithUppercase()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ScyllaApplicationOptions>>();

            optionsExpectationsDict.Add(@"-OUT C:\out", (o) => Assert.Equal(@"C:\out", o.OutputDirectory));
            optionsExpectationsDict.Add(@"-Bam C:\a.bam", (o) => Assert.Equal(@"C:\a.bam", o.BamPath));
            optionsExpectationsDict.Add(@"-vcF C:\a.vcf", (o) => Assert.Equal(@"C:\a.vcf", o.VcfPath));
            optionsExpectationsDict.Add("-Dist 20", (o) => Assert.Equal(20, o.PhasableVariantCriteria.PhasingDistance));
            optionsExpectationsDict.Add("-PassingVariantsOnly true", (o) => Assert.Equal(true, o.PhasableVariantCriteria.PassingVariantsOnly));
            optionsExpectationsDict.Add("-HetvariantsOnly false", (o) => Assert.Equal(false, o.PhasableVariantCriteria.HetVariantsOnly));
            optionsExpectationsDict.Add("-allowClustermerging true", (o) => Assert.Equal(true, o.ClusteringParams.AllowClusterMerging));
            optionsExpectationsDict.Add("-allowWorstfitremoval true", (o) => Assert.Equal(true, o.ClusteringParams.AllowWorstFitRemoval));
            optionsExpectationsDict.Add("-debUg True", (o) => Assert.Equal(true, o.Debug));
            optionsExpectationsDict.Add("-maxnbhdStoprocess 12", (o) => Assert.Equal(12, o.PhasableVariantCriteria.MaxNumNbhdsToProcess));
            optionsExpectationsDict.Add("-chR [chr1,chr3,]", (o) => Assert.Equal(new[] { "chr1", "chr3" }, o.PhasableVariantCriteria.ChrToProcessArray));
            optionsExpectationsDict.Add("-PP true", (o) => Assert.Equal(true, o.BamFilterParams.OnlyUseProperPairs));
            optionsExpectationsDict.Add("-maXnumthreads 10", (o) => Assert.Equal(10, o.NumThreads));
            optionsExpectationsDict.Add("-Minbasecallquality 34", (o) => Assert.Equal(34, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("-Minmapquality 52", (o) => Assert.Equal(52, o.BamFilterParams.MinimumMapQuality));
            optionsExpectationsDict.Add("-minVariantqscore 10", (o) => Assert.Equal(10, o.VariantCallingParams.MinimumVariantQScore));
            optionsExpectationsDict.Add("-variantqualityfilter 78", (o) => Assert.Equal(78, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("-MINimumfrequency 0.001", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequency)); // <- note this will get overwritten with the min diploid frequency if  diploid. *
            optionsExpectationsDict.Add("-MINvariantfrequencyfilter 0.056", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequencyFilter));  // <- note this will get overwritten with the min diploid frequency if  diploid. *
            optionsExpectationsDict.Add("-filterDuplicates false", (o) => Assert.Equal(false, o.BamFilterParams.RemoveDuplicates));
            optionsExpectationsDict.Add("-Ploidy diploid", (o) => Assert.Equal(PloidyModel.DiploidByThresholding, o.VariantCallingParams.PloidyModel));
            optionsExpectationsDict.Add("-Clusterconstraint 73", (o) => Assert.Equal(73, o.ClusteringParams.ClusterConstraint));

            //* Happens in SetDerivedValues(); in new OptionSet method, and at the end of the string-parsing code in old (to be removed) method.

            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<ScyllaApplicationOptions>> GetOptionsExpectationsWithDoubleHyphen()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<ScyllaApplicationOptions>>();

            optionsExpectationsDict.Add(@"--out C:\oUt", (o) => Assert.Equal(@"C:\oUt", o.OutputDirectory));
            optionsExpectationsDict.Add(@"--bam C:\a.BAM", (o) => Assert.Equal(@"C:\a.BAM", o.BamPath));
            optionsExpectationsDict.Add(@"--vcf C:\a.VCF", (o) => Assert.Equal(@"C:\a.VCF", o.VcfPath));
            optionsExpectationsDict.Add("--dist 20", (o) => Assert.Equal(20, o.PhasableVariantCriteria.PhasingDistance));
            optionsExpectationsDict.Add("--passingvariantsonly true", (o) => Assert.Equal(true, o.PhasableVariantCriteria.PassingVariantsOnly));
            optionsExpectationsDict.Add("--hetvariantsonly false", (o) => Assert.Equal(false, o.PhasableVariantCriteria.HetVariantsOnly));
            optionsExpectationsDict.Add("--allowclustermerging true", (o) => Assert.Equal(true, o.ClusteringParams.AllowClusterMerging));
            optionsExpectationsDict.Add("--allowworstfitremoval true", (o) => Assert.Equal(true, o.ClusteringParams.AllowWorstFitRemoval));
            optionsExpectationsDict.Add("--debug true", (o) => Assert.Equal(true, o.Debug));
            optionsExpectationsDict.Add("--maxnbhdstoprocess 10", (o) => Assert.Equal(10, o.PhasableVariantCriteria.MaxNumNbhdsToProcess));
            optionsExpectationsDict.Add("--chr [chr1,chr3,]", (o) => Assert.Equal(new[] { "chr1", "chr3" }, o.PhasableVariantCriteria.ChrToProcessArray));
            optionsExpectationsDict.Add("--pp true", (o) => Assert.Equal(true, o.BamFilterParams.OnlyUseProperPairs));
            optionsExpectationsDict.Add("--maxnumthreads 10", (o) => Assert.Equal(10, o.NumThreads));
            optionsExpectationsDict.Add("--minbasecallquality 12", (o) => Assert.Equal(12, o.BamFilterParams.MinimumBaseCallQuality));
            optionsExpectationsDict.Add("--minmapquality 52", (o) => Assert.Equal(52, o.BamFilterParams.MinimumMapQuality));
            optionsExpectationsDict.Add("--minvariantqscore 10", (o) => Assert.Equal(10, o.VariantCallingParams.MinimumVariantQScore));
            optionsExpectationsDict.Add("--variantqualityfilter 12", (o) => Assert.Equal(12, o.VariantCallingParams.MinimumVariantQScoreFilter));
            optionsExpectationsDict.Add("--minimumfrequency 0.001", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequency)); // <- note this will get overwritten with the min diploid frequency if  diploid.*
            optionsExpectationsDict.Add("--minvariantfrequencyfilter 0.056", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequencyFilter));  // <- note this will get overwritten with the min diploid frequency if  diploid.*
            optionsExpectationsDict.Add("--filterduplicates false", (o) => Assert.Equal(false, o.BamFilterParams.RemoveDuplicates));
            optionsExpectationsDict.Add("--ploidy diploid", (o) => Assert.Equal(PloidyModel.DiploidByThresholding, o.VariantCallingParams.PloidyModel));
            optionsExpectationsDict.Add("--clusterconstraint 75", (o) => Assert.Equal(75, o.ClusteringParams.ClusterConstraint));

            //* Happens in SetDerivedValues(); in new OptionSet method, and at the end of the string-parsing code in old (to be removed) method.


            return optionsExpectationsDict;
        }
        
        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<ScyllaApplicationOptions> assertions = null)
        {
            var optionParser = GetParsedAndValidatedApplicationOptions(arguments);
            var parseResult = optionParser.ParsingResult;

            if (shouldPass)
            {
                var options = optionParser.ScyllaOptions;
                assertions(options);
                Assert.True(parseResult.ExitCode == 0);
            }
            else //TODO - it would be nice to specify the actual error codes from the parsing result
            {
                Assert.NotNull(parseResult.Exception);
                Assert.True(parseResult.ExitCode != 0);
            }
        }
        
        private ScyllaOptionsParser GetParsedAndValidatedApplicationOptions(string arguments)
        {
            var optionParser = new ScyllaOptionsParser();
            optionParser.ParseArgs(arguments.Split(' '), true);
            return optionParser;
        }
       
        [Fact]
        public void ParseCommandLine()
        {
            var optionsExpectations = GetOptionsExpectations();
            Action<ScyllaApplicationOptions> expectations = null;
            foreach (var option in optionsExpectations.Values)
            {
                expectations += option;
            }

            //Test with multiple options strung together by spaces.
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys), true, expectations);

            //Different separator shouldn't work
            ExecuteParsingTest(string.Join(";", optionsExpectations.Keys), false, expectations);

            //Order shouldn't matter
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderByDescending(o => o)), true, expectations);
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys.OrderBy(o => o)), true, expectations);

            ExecuteParsingTest("blah", false);

            //Verify capitalization doesnt matter
            var optionsUpperCaseExpectations = GetOptionsExpectationsWithUppercase();
            expectations = null;
            foreach (var option in optionsExpectations.Values)
            {
                expectations += option;
            }
            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys), true, expectations);

            //Verify hyphenation doesnt matter
            var optionsWithDoubleHyphenExpectations = GetOptionsExpectationsWithDoubleHyphen();
            expectations = null;
            foreach (var option in optionsExpectations.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", optionsExpectations.Keys), true, expectations);
        }

        [Fact]
        public void PrintUsageInfo()
        {
            // Should not throw exception... not much else to test here...
          
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-help" }));
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-h" }));
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "-HELP" }));
            Assert.Equal((int)ExitCodeType.Success, Program.Main(new string[] { "--h" }));
        }

        [Fact]
        public void TestInvalidArguments()
        {
           
            ScyllaApplicationOptions phasingOptions = new ScyllaApplicationOptions();
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { }));
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "" }));
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] { "\t" }));
            Assert.Equal((int)ExitCodeType.MissingCommandLineOption, Program.Main(new string[] {" " }));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-h","-blah"}));
            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(new string[] { "-help", "-blah" }));

            var inBam = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.genome.vcf");
            var outPath = Path.Combine(TestPaths.LocalScratchDirectory, "chr21_11085587_S1.phased.genome.vcf");


            string[] extraSetting = new string[] { "-out", outPath, "-bam", inBam, "-vcf", inVcf, "-extrathing", "blah" };

            Assert.Equal((int)ExitCodeType.UnknownCommandLineOption, Program.Main(extraSetting));

        }
    }
}
