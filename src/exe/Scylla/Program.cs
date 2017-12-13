using System;
using System.IO;
using System.Collections.Generic;
using VariantPhasing;
using VariantPhasing.Logic;
using Pisces.IO.Sequencing;
using Common.IO.Utility;

namespace Scylla
{
    public class Program
    {

        private static void Main(string[] args)
        {
           
            try
            {
                var options = new PhasingApplicationOptions();
                if(!CommandLineParameters.ParseAndValidateCommandLine(args, options))
                    return;

                Execute(options);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
            finally
            {
                Logger.CloseLog();
            }
        }


        private static void Init(PhasingApplicationOptions options)
        {
            Logger.OpenLog(options.LogFolder, options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(options.CommandLineArguments);

            List<string> vcfHeaderLines;
            using (var reader = new VcfReader(options.VcfPath))
            {
                vcfHeaderLines = reader.HeaderLines;
            }


            //where to find the Pisces options used to make the original vcf
            var piscesLogDirectory = Path.Combine(Path.GetDirectoryName(options.VcfPath), "PiscesLogs");
            if (!Directory.Exists(piscesLogDirectory))
            piscesLogDirectory = Path.GetDirectoryName(options.VcfPath);

            //update and revalidate, if required.
            if (options.UpdateWithPiscesConfiguration(
                options.CommandLineArguments.Split(), vcfHeaderLines, piscesLogDirectory))
            {
                options.SetDerivedvalues();
                options.Validate();
            }

            options.Save(Path.Combine(options.LogFolder, "ScyllaOptions.used.json"));
		}

        public static void Execute(PhasingApplicationOptions options)
        {
            Init(options);
            var factory = new Factory(options);
            var variantPhaser = new VariantPhaser(factory);
            variantPhaser.Execute(options.NumThreads);
        }
    }
}
