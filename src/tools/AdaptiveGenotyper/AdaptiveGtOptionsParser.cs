using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;

namespace AdaptiveGenotyper
{
    public class AdaptiveGtOptionsParser : BaseOptionParser
    {
        public AdaptiveGtOptionsParser()
        {
            Options = new AdaptiveGtOptions();
        }

        public AdaptiveGtOptions AdaptiveGtOptions { get => (AdaptiveGtOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + $" input file name",
                    value => AdaptiveGtOptions.VcfPath = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + $"output directory",
                    value=> AdaptiveGtOptions.OutputDirectory = value
                },
                {
                    "log=",
                    OptionTypes.STRING + $" log file name",
                    value=>AdaptiveGtOptions.LogFileName = value
                },
                {
                    "model|models=",
                    OptionTypes.PATH + $"models file previously generated",
                    value => AdaptiveGtOptions.ModelFile = value
                }

            };

            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
           };

            return optionDict;
        }

        public override void ValidateOptions()
        {
            CheckInputFilenameExists(AdaptiveGtOptions.VcfPath, "vcf input", "--vcf");

            if (ParsingFailed)
                return;

            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.GetDirectoryName(AdaptiveGtOptions.VcfPath);
            }

            CheckAndCreateDirectory(Options.OutputDirectory, " output directory", "-o", false);

            if (ParsingFailed)
                return;

            if (!string.IsNullOrEmpty(AdaptiveGtOptions.ModelFile))
            {
                CheckInputFilenameExists(AdaptiveGtOptions.ModelFile, "models file", "--models", false);
            }

        }


    }
}