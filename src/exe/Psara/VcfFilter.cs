using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using CommandLine.Options;

namespace Psara
{
   
    public class VcfFilter
    {
        GeometricFilter _geometricFilter;
        string _outputFile;
        List<string> _originalHeaderLines;
        PsaraOptions _psaraOptions;

        public PsaraVcfWriter GetPsaraVcfWriter(VcfConsumerAppOptions vcfConsumerOptions, string outputFilePath)
        {
            var config = new VcfWriterConfig(vcfConsumerOptions.VariantCallingParams, vcfConsumerOptions.VcfWritingParams, vcfConsumerOptions.BamFilterParams, null, false,
                false, false);

            var psaraCommandLineForVcfHeader = "##Psara_cmdline=" + vcfConsumerOptions.QuotedCommandLineArgumentsString;

            return (new PsaraVcfWriter(outputFilePath, config, new VcfWriterInputContext(), _originalHeaderLines, psaraCommandLineForVcfHeader));
        }

        public VcfFilter(PsaraOptions settings)
        {
            var vcfIn = settings.VcfPath;
            var vcfName = Path.GetFileName(vcfIn);

            _originalHeaderLines = AlleleReader.GetAllHeaderLines(vcfIn);
            _geometricFilter = new GeometricFilter(settings.GeometricFilterParameters);
            _psaraOptions = (PsaraOptions) VcfConsumerAppParsingUtils.TryToUpdateWithOriginalOptions( settings, _originalHeaderLines, vcfIn);
            _outputFile = Path.Combine(settings.OutputDirectory, vcfName.Replace(".vcf", ".filtered.vcf"));
            _outputFile = _outputFile.Replace(".genome.filtered.vcf", ".filtered.genome.vcf");

        }

        public void DoFiltering()
        {

            Logger.WriteToLog("filtering " + _psaraOptions.VcfPath + "...");

            if (File.Exists(_outputFile))
                File.Delete(_outputFile);

            VcfUpdater<GeometricFilter>.UpdateVcfLociByLoci(_outputFile, _psaraOptions, false, _geometricFilter,
                UpdateColocatedAlleles, CanNeverSkipVcfLine, GetPsaraVcfWriter);

            Logger.WriteToLog("filtering complete.");

        }


        public static TypeOfUpdateNeeded UpdateColocatedAlleles(VcfConsumerAppOptions appOptions, GeometricFilter filter, List<CalledAllele> inAlleles, out List<CalledAllele> outAlleles)
        {
            outAlleles = filter.DoFiltering(inAlleles);
            return TypeOfUpdateNeeded.Modify;
        }

        public static TypeOfUpdateNeeded CanNeverSkipVcfLine(List<string> originalVarString)
        {
            return TypeOfUpdateNeeded.Modify;
        }
    }
}
