using System.Collections.Generic;
using System.IO;
using CommandLine.IO;
using CommandLine.NDesk.Options;

namespace VariantQualityRecalibration
{
    public class VQROptionsParser : BaseOptionParser
    {

        public VQROptions Options = new VQROptions();

        public VQROptionsParser()
        {
            Options = new VQROptions();
        }


        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + $" input file name",
                    value => Options.InputVcf = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + $"output directory",
                    value=> Options.OutputDirectory = value
                },
                {
                    "locicount=",
                    OptionTypes.INT + $" When using a vcf instead of a genome.vcf, the user should input the estimated num loci",
                    value=> Options.LociCount = int.Parse(value)
                },
                {
                    "b=",
                    OptionTypes.INT + $" baseline noise level, default {  Options.BaseQNoise}. (The new noise level is never recalibrated to lower than this.)",
                    value=> Options.BaseQNoise = int.Parse(value)
                },
                {
                    "f=",
                    OptionTypes.INT + $" filter Q score, default { Options.FilterQScore} (if a variant gets recalibrated, when we apply the \"LowQ\" filter)",
                    value=> Options.FilterQScore = int.Parse(value)
                },
                {
                    "z=",
                    OptionTypes.FLOAT + $" thresholding parameter, default { Options.ZFactor} (How many std devs above averge observed noise will the algorithm tolerate, before deciding a mutation type is likely to be artifact )",
                    value=> Options.ZFactor = float.Parse(value)
                },
                {
                    "q=",
                    OptionTypes.INT + $" max Q score, default { Options.MaxQScore} (if a variant gets recalibrated, when we cap the new Q score)",
                    value => Options.MaxQScore = int.Parse(value)
                },
                {
                    "log=",
                    OptionTypes.STRING + $" log file name",
                    value=> Options.LogFileName = value
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
            //this would set an error code. Once we have one, we should quit.

            CheckInputFilenameExists(Options.InputVcf, "vcf input", "--vcf");

            if (ParsingFailed)
                return;

            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.GetDirectoryName(Options.InputVcf);
            }

            CheckAndCreateDirectory(Options.OutputDirectory, " output directory", "-o", false);

            if (ParsingFailed)
                return;

            if (Options.InputVcf.ToLower().EndsWith(".vcf") && !Options.InputVcf.ToLower().EndsWith(".genome.vcf"))
            {
                HasRequiredParameter(Options.LociCount, "the estimated num loci for vcf input", "--locicount");
            }

        }


    }
}