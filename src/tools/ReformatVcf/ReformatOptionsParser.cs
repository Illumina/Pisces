using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;

namespace ReformatVcf
{
    public class ReformatOptionsParser : BaseOptionParser
    {
        public ReformatOptionsParser()
        {
            Options = new ReformatOptions();
        }

        public ReformatOptions ReformatOptions { get => (ReformatOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + $" input file name",
                    value => ReformatOptions.VcfPath = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + $"output directory",
                    value=> ReformatOptions.OutputDirectory = value
                },

                { 
                    "crush=",
                    OptionTypes.STRING + $" log file name",
                    value=>ReformatOptions.VcfWritingParams.ForceCrush=  bool.Parse(value)
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
            //dont worry about it
        }


    }
}