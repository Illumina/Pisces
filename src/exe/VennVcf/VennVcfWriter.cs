using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Common.IO;

namespace VennVcf
{
  
    public class VennVcfWriter : VcfFileWriter
    {
        private readonly List<string> _originalHeader;
        private readonly string _vennCommandLine;
        Dictionary<FilterType, string> _originalFilterLines = new Dictionary<FilterType, string>();

        public VennVcfWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, 
            List<string> originalHeader, string phasingCommandLine, int bufferLimit = 2000, bool debugMode =false ) : base(outputFilePath, config, context, bufferLimit)
        {
            _originalHeader = originalHeader;
            _originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(originalHeader);
            _formatter = new VennVcfFormatter(config, debugMode);
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _vennCommandLine = phasingCommandLine;
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

            var currentAssembly =FileUtilities.LocalAssemblyPath<VennVcfWriter>();
            var currentVersion = FileUtilities.LocalAssemblyVersion<VennVcfWriter>();

            Writer.WriteLine("##VcfPostProcessor=VennVcf " + currentVersion);
            if (_vennCommandLine != null)
                Writer.WriteLine(_vennCommandLine);

            for (int i = offset; i < _originalHeader.Count(); i++)
                Writer.WriteLine(_originalHeader[i]);
        }

        //we add one header line, PB
        public void AdjustHeaderLines()
        {
            var originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(_originalHeader);
            var vennFilterLines = _formatter.GenerateFilterStringsByType();

            //Pisces might have used these, but venn (currently) never does.
            //So we only write them to header if Pisces already has them in the header.
            vennFilterLines.Remove(FilterType.RMxN);
            vennFilterLines.Remove(FilterType.IndelRepeatLength);

            int lastFilterIndex = _originalHeader.FindLastIndex(x => x.Contains("##FILTER"));

            if (lastFilterIndex == -1)
                lastFilterIndex = Math.Max(_originalHeader.Count - 2, -1);

            foreach (var pair in vennFilterLines)
            {
                var vennFilter = pair.Key;
                var vennString = pair.Value;

                if (!originalFilterLines.ContainsKey(vennFilter))
                {
                    lastFilterIndex++;
                    _originalHeader.Insert(lastFilterIndex, vennString.Replace("\">", ", by VennVcf\">"));
                }
                else
                {
                    //we already have this filter listed... but is the string the same? it should be.
                    if (vennString.Trim() != originalFilterLines[vennFilter].Trim()) //be gentle about line endings..
                    {
                        lastFilterIndex++;
                        _originalHeader.Insert(lastFilterIndex, vennString.Replace("\">", ", by VennVcf\">"));
                    }

                    //else, the filter values are the same.
                }

            }

        }


        public override void Write(IEnumerable<CalledAllele> calledAlleles, IRegionMapper mapper = null)
        {
            var comparer = new AlleleCompareByLoci();
            var sortedVariants = calledAlleles.OrderBy(a => a, comparer).ThenBy(a => a.ReferenceAllele).ThenBy(a => a.AlternateAllele);
            base.Write(sortedVariants);
        }


    }

}
