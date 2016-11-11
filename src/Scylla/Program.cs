using System;
using System.IO;
using VariantPhasing;
using VariantPhasing.Logic;

namespace Scylla
{
    //example calls:
    // -vcf \\sd-isilon\bioinfosd\lteng\MNV_25\MergedBamAll\MNV-25-var327_S327.genome.vcf -bam \\sd-isilon\bioinfosd\lteng\MNV_25\MergedBamAll\MNV-25-var327_S327.bam -out C:\Products\LatestMNV\out
    //-vcf \\sd-isilon\bioinfosd\lteng\MergedBam\MNV-50-var50_S50.genome.vcf -bam \\sd-isilon\bioinfosd\lteng\MergedBam\MNV-50-var50_S50.bam -out C:\Products\S1
    //-vcf \\ussd-prd-isi04\pisces\TestData\R2\ATTC_indel\CRM-CCL-119D_S12.genome.vcf -bam \\ussd-prd-isi04\pisces\TestData\R2\ATTC_indel\CRM-CCL-119D_S12.bam -out \\ussd-prd-isi04\pisces\TestData\Out -chr [chr9]

    public class Program
    {
        private ApplicationOptions _options;

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
                Pisces.Processing.Utility.Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
            finally
            {
                Pisces.Processing.Utility.Logger.TryCloseLog();
            }
        }

        public Program(string[] args)
        {
            _options = CommandLineParameters.ParseAndValidateCommandLine(args);
            if (_options == null) return;
            Init();
        }

        private void Init()
        {
            Pisces.Processing.Utility.Logger.TryOpenLog(_options.LogFolder, _options.LogFileName);
            Pisces.Processing.Utility.Logger.WriteToLog("Command-line arguments: ");
            Pisces.Processing.Utility.Logger.WriteToLog(_options.CommandLineArguments);
			_options.Save(Path.Combine(_options.LogFolder, "ScyllaOptions.used.xml"));
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
