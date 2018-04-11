using System;
using System.IO;
using System.Collections.Generic;
using CommandLine.IO;
using CommandLine.NDesk.Options;

namespace Psara
{
    public class PsaraOptionsParser : BaseOptionParser
    {

        public PsaraOptions PsaraOptions;

        public PsaraOptionsParser()
        {
            PsaraOptions = new PsaraOptions();
        }


        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + " input file name",
                    value => PsaraOptions.InputVcf = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + " output directory",
                    value=> PsaraOptions.OutputDirectory = value
                },
                {
                    "log=",
                    OptionTypes.STRING + " log file name",
                    value=> PsaraOptions.LogFileNameBase = value
                }

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
           };


            //add child-options to the dictionary

            PsaraOptions.GeometricFilterParameters = new GeometricFilterParameters();

            var geometricFilterParametersOptionsDict = GeometricFilterParsingMethods.GetParsingMethods(PsaraOptions.GeometricFilterParameters);
            var keys = optionDict.Keys;

            foreach (var key in keys)
            {
                foreach (var optSet in geometricFilterParametersOptionsDict[key])
                    optionDict[key].Add(optSet);
            }

            return optionDict;
        }


        public override void ValidateOptions()
        {
            if ((PsaraOptions.InputVcf == null) || !(File.Exists(PsaraOptions.InputVcf)))
            {
                throw new ArgumentException(string.Format("Input vcf file is required. {0}", PsaraOptions.InputVcf));
            }

            if (string.IsNullOrEmpty(PsaraOptions.OutputDirectory))
            {
                //try to help by making one
                PsaraOptions.OutputDirectory = Path.GetDirectoryName(PsaraOptions.InputVcf);
            }

            if ((PsaraOptions.OutputDirectory != null) && !(Directory.Exists(PsaraOptions.OutputDirectory)))
            {

                Directory.CreateDirectory(PsaraOptions.OutputDirectory);

                if (!(Directory.Exists(PsaraOptions.OutputDirectory)))
                    throw new ArgumentException(string.Format("Unable to create output folder. {0}", PsaraOptions.OutputDirectory));
            }

            PsaraOptions.GeometricFilterParameters.Validate();

        }

    }
}