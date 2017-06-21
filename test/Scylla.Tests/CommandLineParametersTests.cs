using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.Domain.Types;
using VariantPhasing;
using Xunit;

namespace Scylla.Tests
{
    public class CommandLineParametersTests
    {
        private Dictionary<string, Action<PhasingApplicationOptions>> GetOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<PhasingApplicationOptions>>();

            optionsExpectationsDict.Add(@"-out C:\out", (o) => Assert.Equal(@"C:\out", o.OutFolder));
            optionsExpectationsDict.Add(@"-bam C:\a.bam", (o) => Assert.Equal(@"C:\a.bam", o.BamPath));
            optionsExpectationsDict.Add(@"-vcf C:\a.vcf", (o) => Assert.Equal(@"C:\a.vcf", o.VcfPath));
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
            optionsExpectationsDict.Add("-minimumfrequency 0.001", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequency)); // <- note this will get overwritten with the min diploid frequency if  diploid.
            optionsExpectationsDict.Add("-minvariantfrequencyfilter 0.056", (o) => Assert.Equal(0.2f, o.VariantCallingParams.MinimumFrequencyFilter));  // <- note this will get overwritten with the min diploid frequency if  diploid.
            optionsExpectationsDict.Add("-filterduplicates false", (o) => Assert.Equal(false, o.BamFilterParams.RemoveDuplicates));
            optionsExpectationsDict.Add("-ploidy diploid", (o) => Assert.Equal(PloidyModel.Diploid, o.VariantCallingParams.PloidyModel));
            optionsExpectationsDict.Add("-clusterconstraint 75", (o) => Assert.Equal(75, o.ClusteringParams.ClusterConstraint));


            return optionsExpectationsDict;
        }

        private void ExecuteParsingTest(string arguments, bool shouldPass, Action<PhasingApplicationOptions> assertions = null)
        {
            var options = new PhasingApplicationOptions();

            if (shouldPass)
            {
                options.ParseCommandLine(arguments.Split(' '));
                if (assertions != null)
                    assertions(options);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => options.ParseCommandLine(arguments.Split(' ')));
            }
        }

        [Fact]
        public void ParseCommandLine()
        {
            var optionsExpectations = GetOptionsExpectations();
            Action<PhasingApplicationOptions> expectations = null;
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
        }

        [Fact]
        public void PrintUsageInfo()
        {
            // Should not throw exception... not much else to test here...
            CommandLineParameters.PrintUsageInfo();
        }

        [Fact]
        public void TestInvalidArguments()
        {
            //should all return null and not throw anything

            PhasingApplicationOptions phasingOptions = new PhasingApplicationOptions();

            bool parseNull = CommandLineParameters.ParseAndValidateCommandLine(null, phasingOptions);
            bool parseEmpty = CommandLineParameters.ParseAndValidateCommandLine(new string[] { }, phasingOptions);
            bool parseH = CommandLineParameters.ParseAndValidateCommandLine(new string[] { "-h","blah"}, phasingOptions);
            bool parseHelp = CommandLineParameters.ParseAndValidateCommandLine(new string[] { "-help", "blah" }, phasingOptions);

            Assert.Equal(false, parseNull);
            Assert.Equal(false, parseEmpty);
            Assert.Equal(false, parseH);
            Assert.Equal(false, parseHelp);


            //should parse what it can, and not throw anything

            var inBam = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.bam");
            var inVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "chr21_11085587_S1.genome.vcf");
            var outPath = Path.Combine(TestPaths.LocalScratchDirectory, "chr21_11085587_S1.phased.genome.vcf");


            bool parseExtraSetting = CommandLineParameters.ParseAndValidateCommandLine(
                new string[] { "-out", outPath, "-bam", inBam, "-vcf", inVcf, "-extrathing", "blah" }, phasingOptions);

            Assert.Equal(true, parseExtraSetting);
            Assert.NotEmpty(phasingOptions.OutFolder);
            Assert.NotEmpty(phasingOptions.BamPath);
            Assert.NotEmpty(phasingOptions.VcfPath);

        }
    }
}
