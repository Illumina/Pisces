using System.IO;
using System.Collections.Generic;
using CommandLine.Options;
using CommandLine.IO;
using CommandLine.NDesk.Options;
using Pisces.Domain.Options;

namespace VennVcf
{
    public class VennVcfOptionsParser : BaseOptionParser
    {
        public VennVcfOptions Options = new VennVcfOptions();

        public VennVcfOptionsParser()
        {
            Options = new VennVcfOptions();
        }


        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "if=",
                    OptionTypes.PATHS +" input file names, as a list.",
                    value => Options.InputFiles = OptionHelpers.ListOfParamsToStringArray(value)
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER +" output directory",
                    value=> Options.OutputDirectory = value
                },
                {
                    "consensus=",
                    OptionTypes.STRING + " consensus file name.",
                    value=> Options.ConsensusFileName = value
                },
                {
                    "mfirst=",
                    OptionTypes.BOOL +$" to order the chr with mito first or last.",
                    value=> Options.VcfWritingParams.MitochondrialChrComesFirst = bool.Parse(value)
                },
                {
                    "debug=",
                    OptionTypes.BOOL +" to print out extra logging",
                    value => Options.DebugMode = bool.Parse(value)
                }

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
             };

            BamFilterOptionsParser.AddBamFilterArgumentParsing(optionDict, Options.BamFilterParams);//TODO - VennVcf really SHOULD NOT need a bam filtering option set.
            VariantCallingOptionsParser.AddVariantCallingArgumentParsing(optionDict, Options.VariantCallingParams);
            VcfWritingOptionsParser.AddVcfWritingArgumentParsing(optionDict, Options.VcfWritingParams);


            return optionDict;
        }

        public override void ValidateOptions()
        {
            //this would set an error code. Once we have one, we should quit.

            if ((Options.InputFiles == null || Options.InputFiles.Length == 0))
                CheckInputFilenameExists("", "vcf input", "-if");

            if (ParsingFailed)
                return;

            foreach (var vcfFile in Options.InputFiles)
                CheckInputFilenameExists(vcfFile, "vcf input", "-if");

            if (ParsingFailed)
                return;

            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.GetDirectoryName(Options.OutputDirectory);
            }

            CheckAndCreateDirectory(Options.OutputDirectory, " output directory", "-o", false);
           
        }


    }
}