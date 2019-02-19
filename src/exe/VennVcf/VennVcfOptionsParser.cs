using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;
using Pisces.Domain.Options;

namespace VennVcf
{
    public class VennVcfOptionsParser : BaseOptionParser
    {
        public VennVcfOptionsParser()
        {
            Options = new VennVcfOptions();
        }

        public VennVcfOptions VennOptions { get => (VennVcfOptions) Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "if=",
                    OptionTypes.PATHS +" input file names, as a list.",
                    value => VennOptions.InputFiles = OptionHelpers.ListOfParamsToStringArray(value)
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER +" output directory",
                    value=> VennOptions.OutputDirectory = value
                },
                {
                    "consensus=",
                    OptionTypes.STRING + " consensus file name.",
                    value=> VennOptions.ConsensusFileName = value
                },            
                {
                    "debug=",
                    OptionTypes.BOOL +" to print out extra logging",
                    value => VennOptions.DebugMode = bool.Parse(value)
                }

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
             };

            BamFilterOptionsUtils.AddBamFilterArgumentParsing(optionDict, VennOptions.BamFilterParams);//TODO - VennVcf really SHOULD NOT need a bam filtering option set.
            VariantCallingOptionsParserUtils.AddVariantCallingArgumentParsing(optionDict, VennOptions.VariantCallingParams);
            VcfWritingParserUtils.AddVcfWritingArgumentParsing(optionDict, VennOptions.VcfWritingParams);


            return optionDict;
        }

        public override void ValidateOptions()
        {
            //this would set an error code. Once we have one, we should quit.

            if ((VennOptions.InputFiles == null || VennOptions.InputFiles.Length == 0))
                CheckInputFilenameExists("", "vcf input", "-if");

            if (ParsingFailed)
                return;

            foreach (var vcfFile in VennOptions.InputFiles)
                CheckInputFilenameExists(vcfFile, "vcf input", "-if");

            if (ParsingFailed)
                return;

            if (string.IsNullOrEmpty(VennOptions.OutputDirectory))
            {
                VennOptions.OutputDirectory = Path.GetDirectoryName(Options.OutputDirectory);
            }

            CheckAndCreateDirectory(VennOptions.OutputDirectory, " output directory", "-o", false);
           
        }


    }
}