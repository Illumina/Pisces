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
        private PhasingApplicationOptions _options;

        private static void Main(string[] args)
        {
           
            try
            {
                var application = new Program(args);
                application.Execute();
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

        public Program() { } //for tester

        public Program(string[] args)
        {
			_options = new PhasingApplicationOptions();
           
            bool worked = CommandLineParameters.ParseAndValidateCommandLine(args,_options);
            if (!worked || (_options == null))
                return;
			
             Init(args);
        }

        private void Init(string[] args)
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(_options.CommandLineArguments);

            List<string> vcfHeaderLines;
            using (var reader = new VcfReader(_options.VcfPath))
            {
                vcfHeaderLines = reader.HeaderLines;
            }


            //where to find the Pisces options used to make the original vcf
            var piscesLogDirectory = Path.Combine(Path.GetDirectoryName(_options.VcfPath), "PiscesLogs");
            if (!Directory.Exists(piscesLogDirectory))
            piscesLogDirectory = Path.GetDirectoryName(_options.VcfPath);

            //update and revalidate, if required.
            if (_options.UpdateWithPiscesConfiguration(
                _options.CommandLineArguments.Split(), vcfHeaderLines, piscesLogDirectory))
            {
                _options.SetDerivedvalues();
                _options.Validate();
            }

            _options.Save(Path.Combine(_options.LogFolder, "ScyllaOptions.used.json"));
		}

        public void Execute()
        {
            if (_options == null)
                return;

            var factory = new Factory(_options);
            var phaser = new Phaser(factory);
            phaser.Execute(_options.NumThreads);
        }
    }
}
