using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;

namespace AdaptiveGenotyper
{
    public class GenotyperOptionsParser : BaseOptionParser
    {
        public GenotyperOptionsParser()
        {
            Options = new GenotyperOptions();
        }

        public GenotyperOptions GQROptions { get => (GenotyperOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + $" input file name",
                    value => GQROptions.InputVcf = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + $"output directory",
                    value=> GQROptions.OutputDirectory = value
                },
                {
                    "log=",
                    OptionTypes.STRING + $" log file name",
                    value=>GQROptions.LogFileName = value
                },
                {
                    "model|models=",
                    OptionTypes.PATH + $"models file previously generated",
                    value => GQROptions.ModelFile = value
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
            CheckInputFilenameExists(GQROptions.InputVcf, "vcf input", "--vcf");

            if (ParsingFailed)
                return;

            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.GetDirectoryName(GQROptions.InputVcf);
            }

            CheckAndCreateDirectory(Options.OutputDirectory, " output directory", "-o", false);

            if (ParsingFailed)
                return;

            if (!string.IsNullOrEmpty(GQROptions.ModelFile))
            {
                CheckInputFilenameExists(GQROptions.ModelFile, "models file", "--models", false);
            }

        }


    }
}