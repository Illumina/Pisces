using System;
using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using Newtonsoft.Json;

namespace Pisces.Domain.Options
{
    public class VcfProcessorOptions
    {
        public VcfWritingParameters VcfWritingParams = new VcfWritingParameters();
        public VariantCallingParameters VariantCallingParams = new VariantCallingParameters();
        public BamFilterParameters BamFilterParams = new BamFilterParameters();


        /// <summary>
        /// This method is a little complex. It parses the base command line once to get the vcf path. It then looks on the vcf path 
        /// for default input parameters (so it uses the same defaults as Pisces was ran with),
        /// in the PiscesOptions.used.json. Failing that, it will get them from the command in the vcf header.
        /// Once those defaults have been loaded, the Scylla command line will be parsed a second time (so, anything supplied by the user can override the defaults loaded) 
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="options"></param>
        public bool UpdateWithPiscesConfiguration(string[] arguments, List<string> VcfHeaderLines, string ConfigFileDir)
        {
            var vcfConfigureFile = Path.Combine(ConfigFileDir, "PiscesOptions.used.json");

        
            if (!File.Exists(vcfConfigureFile))
            {
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} does not exist. Reading defaults from the VCF header");

                PiscesApplicationOptions piscesOptions = PiscesApplicationOptions.GetPiscesOptionsFromVcfHeader(VcfHeaderLines);
                if (piscesOptions == null)
                {
                    Logger.WriteToLog("Unable to parse the original Pisces commandline. Continuing without it");
                    return false;
                }

                VariantCallingParams = piscesOptions.VariantCallingParameters;
                VcfWritingParams = piscesOptions.VcfWritingParameters;
                BamFilterParams = piscesOptions.BamFilterParameters;

                ParseCommandLine(arguments);
                return true;
            }

            if (UpdateOptions(vcfConfigureFile))
            {
                ParseCommandLine(arguments);
                return true;
            }

            return false;
        }

        private bool UpdateOptions(string vcfConfigureFile)
        {
            PiscesApplicationOptions piscesOptions;

            try
            {
                piscesOptions = JsonUtil.Deserialize<PiscesApplicationOptions>(vcfConfigureFile);
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} loaded");
            }
            catch (Exception ex)
            {
                Logger.WriteExceptionToLog(ex);
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} failed to load. Continuing without it ");
                return false;
            }


            VariantCallingParams = piscesOptions.VariantCallingParameters;
            VcfWritingParams = piscesOptions.VcfWritingParameters;
            BamFilterParams = piscesOptions.BamFilterParameters;

            return true;
        }

        public virtual bool ParseCommandLine(string[] arguments)
        {
            throw new NotImplementedException();
        }  

        public void SetDerivedvalues()
        {
            VariantCallingParams.SetDerivedParameters(BamFilterParams);
            VcfWritingParams.SetDerivedParameters(VariantCallingParams);
        }

        public void Validate()
        {
            BamFilterParams.Validate();
            VariantCallingParams.Validate();
        }

        public void Save(string filepath)
        {
            JsonUtil.Save(filepath, this);
        }
    }
}
