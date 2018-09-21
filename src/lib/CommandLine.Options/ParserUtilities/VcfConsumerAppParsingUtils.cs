using System;
using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using Pisces.Domain.Options;
using CommandLine.Util;

namespace CommandLine.Options
{
    //Handles getting the original configuration that created a vcf.
    //Used by any app in the suite that needs to consume a vcf and figure out what settings generated it.
    public class VcfConsumerAppParsingUtils
    {

        /// <summary>
        /// We have a number of applications that are vcf-consumers (Psara, Scylla, VennVcf, VQR),
        /// and they need to (or should) parse out the original Pisces settings that were used to make the input vcf. 
        /// </summary>
        /// <param name="optionsToUpdate"></param>
        /// <param name="vcfHeaderLines"></param>
        /// <param name="configFileDir"></param>
        /// <returns></returns>
        public static VcfConsumerAppOptions TryToUpdateWithOriginalOptions(VcfConsumerAppOptions optionsToUpdate, List<string> vcfHeaderLines, string configFileDir)
        {
            //update and revalidate, if required. 
            //(The new options parser will automatically revalidate for us.)
            var piscesOptionsParser = GetOriginalPiscesOptions(vcfHeaderLines, configFileDir);
            var originalPiscesOptions = piscesOptionsParser.PiscesOptions;

            if (piscesOptionsParser.HadSuccess)
            {
                optionsToUpdate.VariantCallingParams = originalPiscesOptions.VariantCallingParameters;
                optionsToUpdate.BamFilterParams = originalPiscesOptions.BamFilterParameters;
                optionsToUpdate.VcfWritingParams = originalPiscesOptions.VcfWritingParameters;

                //validation is just a subset of the PiscesOptions validation
                optionsToUpdate.SetDerivedValues();
                optionsToUpdate.Validate();
            }

            return optionsToUpdate;
        }

        public static PiscesOptionsParser GetOriginalPiscesOptions(List<string> vcfHeaderLines, string configFileDir)
        {
            var vcfConfigureFile = Path.Combine(configFileDir, "PiscesOptions.used.json");
            PiscesOptionsParser piscesParser = new PiscesOptionsParser();

            if (File.Exists(vcfConfigureFile))
                piscesParser = GetOptionsFromConfigFile(vcfConfigureFile);
            else
            {
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} does not exist. Reading defaults from the VCF header");
                piscesParser = GetPiscesOptionsFromVcfHeader(vcfHeaderLines);
            }

            return piscesParser;
        }

        private static PiscesOptionsParser GetOptionsFromConfigFile(string vcfConfigureFile)
        {
            var piscesParser = new PiscesOptionsParser();
       
            try
            {
                piscesParser.Options = JsonUtil.Deserialize<PiscesApplicationOptions>(vcfConfigureFile);
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} loaded");
                return piscesParser;
            }
            catch (Exception ex)
            {
                Logger.WriteExceptionToLog(ex);
                Logger.WriteToLog($"Pisces option file {vcfConfigureFile} failed to load. Continuing without it ");
                piscesParser.ParsingResult.Exception = ex;
                piscesParser.ParsingResult.UpdateExitCode(ExitCodeType.InvalidFileFormat);
                return piscesParser;
            }
        }

        public static PiscesOptionsParser GetPiscesOptionsFromVcfHeader(List<string> VcfHeaderLines)
        {
            var piscesParser = new PiscesOptionsParser();
            var startString = "##Pisces_cmdline=";
            if (VcfHeaderLines.Count != 0 && VcfHeaderLines.Exists(x => x.StartsWith(startString)))
            {
                try
                {
                    var piscesCmd = VcfHeaderLines.FindLast(x => x.StartsWith(startString)).Replace(startString, "").Replace("\"", "").ToLower();
                    piscesCmd = piscesCmd.Replace("-v ", "-vffilter "); //"v" used to be vf filter, now it returns the version number. Be kind and help the user with this one. If th ey pass "-v" that will shut down all the parsing and output the version.
                    piscesCmd = piscesCmd.Replace("-bamfolder ", "-bam "); //being kind to another obsolete argument

                    //parse the original pisces options, but do not validate. 
                    //We dont need to validate everything, just to get the vcf processing options.
                    piscesParser.ParseArgs(piscesCmd.Split(), false);

                    ((PiscesApplicationOptions)piscesParser.Options).SetDerivedParameters();

                    return piscesParser;
                }
                catch (Exception ex)
                {
                    Logger.WriteToLog("Unable to parse the original Pisces commandline from the input vcf.");
                    Logger.WriteExceptionToLog(ex);

                    piscesParser.ParsingResult.Exception = ex;
                    piscesParser.ParsingResult.UpdateExitCode(ExitCodeType.BadFormat);
                }
            }

            piscesParser.ParsingResult.Exception = new Exception("Pisces command line was not found in the input vcf");
            piscesParser.ParsingResult.UpdateExitCode(ExitCodeType.BadFormat);

       

            return piscesParser;
        }

    }
}
