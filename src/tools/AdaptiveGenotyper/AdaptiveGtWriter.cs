using Common.IO.Utility;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Pisces.Domain.Types;
using Pisces.Genotyping;
using Pisces.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdaptiveGenotyper
{
    public class AdaptiveGtWriter : VcfFileWriter
    {
        private readonly List<string> _originalHeader;
        private readonly string _commandLine;
        readonly Dictionary<FilterType, string> _originalFilterLines = new Dictionary<FilterType, string>();

        private AdaptiveGtWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context,
            List<string> originalHeader, string commandLine, int bufferLimit = 2000) 
                : base(outputFilePath, config, context, bufferLimit)
        {
            _originalHeader = originalHeader;
            _originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(originalHeader);
            _formatter = new VcfFormatter(config);
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _commandLine = commandLine;
        }

        public static AdaptiveGtWriter GetAdaptiveGtWriter(VcfConsumerAppOptions options, string outputFilePath)
        {
            var vcp = options.VariantCallingParams;
            var vwp = options.VcfWritingParams;
            var bfp = options.BamFilterParams;
            var vcfConfig = new VcfWriterConfig(vcp, vwp, bfp, null, false, false);
            var headerLines = AlleleReader.GetAllHeaderLines(options.VcfPath);

            var commandLineHeader = "##AdaptiveGT_cmdline=" + options.QuotedCommandLineArgumentsString;
            return new AdaptiveGtWriter(outputFilePath, vcfConfig, new VcfWriterInputContext(), headerLines, commandLineHeader);
        }

        public static void RewriteVcf(string vcfIn, string outDir, AdaptiveGtOptions options, RecalibrationResults results)
        {

            Logger.WriteToLog("Rewriting VCF.");

            string vcfFileName = Path.GetFileName(vcfIn);
            if (vcfFileName.Contains("genome."))
                vcfFileName = vcfFileName.Replace("genome", "recal");
            else
                vcfFileName = vcfFileName.Replace(".vcf", ".recal.vcf");

            string vcfOut = Path.Combine(outDir, vcfFileName);

            if (File.Exists(vcfOut))
                File.Delete(vcfOut);

            VcfUpdater<RecalibrationResults>.UpdateVcfLociByLoci(vcfOut, options, false, results, LocusProcessor.ProcessLocus,
                (List<string> vcfLine) => TypeOfUpdateNeeded.Modify, GetAdaptiveGtWriter);

            Logger.WriteToLog("filtering complete.");
        }
    }
}
