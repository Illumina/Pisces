using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO;
using Pisces.IO.Sequencing;
using Pisces.IO.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Common.IO;

namespace VariantQualityRecalibration
{
    public class VQRVcfWriter : VcfFileWriter
    {

        private readonly List<string> _originalHeader;
        private readonly string _vqrCommandLine;
        Dictionary<FilterType, string> _originalFilterLines = new Dictionary<FilterType, string>();

        public VQRVcfWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, List<string> originalHeader, string phasingCommandLine, int bufferLimit = 2000) : base(outputFilePath, config, context, bufferLimit)
        {
            _originalHeader = originalHeader;
            _originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(originalHeader);
            _formatter = new VcfFormatter(config);
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _vqrCommandLine = phasingCommandLine;
        }

        public static VQRVcfWriter GetVQRVcfFileWriter(VcfConsumerAppOptions options, string outputFilePath)
        {
            var vcp = options.VariantCallingParams;
            var vwp = options.VcfWritingParams;
            var bfp = options.BamFilterParams;
            var vcfConfig = new VcfWriterConfig(vcp, vwp, bfp, null, false, false);
            var headerLines = AlleleReader.GetAllHeaderLines(options.VcfPath);

            var vqrCommandLineForVcfHeader = "##VQR_cmdline=" + options.QuotedCommandLineArgumentsString;
            return (new VQRVcfWriter(outputFilePath, vcfConfig, new VcfWriterInputContext(), headerLines, vqrCommandLineForVcfHeader));
        }

        public override void WriteHeader()
        {
            if (Writer == null)
                throw new IOException("Stream already closed");

            AdjustHeaderLines();

            int offset = 4;
            if (_originalHeader.Count - 1 < offset)
                offset = _originalHeader.Count - 1;


            foreach (var line in _originalHeader.Take(offset))
            {
                Writer.WriteLine(line);
            }

            var currentVersion = FileUtilities.LocalAssemblyVersion<VQRVcfWriter>();

            Writer.WriteLine("##VariantQualityRecalibrator=VQR " + currentVersion);
            if (_vqrCommandLine != null)
                Writer.WriteLine(_vqrCommandLine);

            for (int i = offset; i < _originalHeader.Count(); i++)
                Writer.WriteLine(_originalHeader[i]);
        }

        /// There are currenlty 1 filter that VQR can add: q{N} for low variant quality scores.
        /// We need to check that this gets added, if the config requires it.
        public void AdjustHeaderLines()
        {
            var originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(_originalHeader);
            var vqrFilterLines = _formatter.GenerateFilterStringsByType();

            //Pisces might have used these, but VQR (currently) never does.
            //So we only write them to header if Pisces already has them in the header.
            vqrFilterLines.Remove(FilterType.RMxN);
            vqrFilterLines.Remove(FilterType.IndelRepeatLength);
            vqrFilterLines.Remove(FilterType.NoCall);

            int lastFilterIndex = _originalHeader.FindLastIndex(x => x.Contains("##FILTER"));

            if (lastFilterIndex == -1)
                lastFilterIndex = Math.Max(_originalHeader.Count - 2, -1);

            foreach (var pair in vqrFilterLines)
            {
                var vqrFilter = pair.Key;
                var vqrString = pair.Value;

                if (!originalFilterLines.ContainsKey(vqrFilter))
                {
                    lastFilterIndex++;
                    _originalHeader.Insert(lastFilterIndex, vqrString.Replace("\">", ", by VQR\">"));
                }
                else
                {
                    //we already have this filter listed... but is the string the same? it should be.
                    if (vqrString.Trim() != originalFilterLines[vqrFilter].Trim()) //be gentle about line endings..
                    {
                        lastFilterIndex++;
                        _originalHeader.Insert(lastFilterIndex, vqrString.Replace("\">", ", by VQR\">"));
                    }

                    //else, the filter values are the same.
                }

            }

        }


        public override void Write(IEnumerable<CalledAllele> calledAlleles, IRegionMapper mapper = null)
        {
            var comparer = new AlleleCompareByLociAndAllele();
            var sortedVariants = calledAlleles.OrderBy(a => a, comparer);
            base.Write(sortedVariants, mapper);
        }




    }
}