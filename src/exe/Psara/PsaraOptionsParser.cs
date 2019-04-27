using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;

namespace Psara
{
    public class PsaraOptionsParser : BaseOptionParser
    {

        public PsaraOptionsParser()
        {
            Options = new PsaraOptions();
        }

        public PsaraOptions PsaraOptions { get => (PsaraOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "vcf=",
                    OptionTypes.PATH + " input file name",
                    value => PsaraOptions.VcfPath = value
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

            if ((PsaraOptions.VcfPath == null) || !(File.Exists(PsaraOptions.VcfPath)))
            {
                throw new ArgumentException(string.Format("Input vcf file is required. {0}", PsaraOptions.VcfPath));
            }

            if (string.IsNullOrEmpty(PsaraOptions.OutputDirectory))
            {
                //try to help by making one
                PsaraOptions.OutputDirectory = Path.GetDirectoryName(PsaraOptions.VcfPath);
            }

            if ((PsaraOptions.OutputDirectory != null) && !(Directory.Exists(PsaraOptions.OutputDirectory)))
            {
                Directory.CreateDirectory(PsaraOptions.OutputDirectory);
            }
            PsaraOptions.GeometricFilterParameters.Validate();

        }

    }
}