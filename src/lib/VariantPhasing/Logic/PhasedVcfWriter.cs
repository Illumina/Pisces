using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Common.IO;

namespace VariantPhasing.Logic
{
    public class PhasedVcfWriter : VcfFileWriter
    {
        private readonly List<string> _originalHeader;
        private readonly string _phasingCommandLine;
        Dictionary<FilterType, string> _originalFilterLines = new Dictionary<FilterType, string>();

        public PhasedVcfWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, List<string> originalHeader, string phasingCommandLine, int bufferLimit = 2000) : base(outputFilePath, config, context, bufferLimit)
        {
            _originalHeader = originalHeader;
            _originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(originalHeader);
            _formatter = new VcfFormatter(config);
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _phasingCommandLine = phasingCommandLine;
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

            var currentVersion = FileUtilities.LocalAssemblyVersion<PhasedVcfWriter>();

            Writer.WriteLine("##VariantPhaser=Scylla " + currentVersion);
            if (_phasingCommandLine != null)
                Writer.WriteLine(_phasingCommandLine);

            for (int i = offset; i < _originalHeader.Count(); i++)
                Writer.WriteLine(_originalHeader[i]);
        }

        /// There are currenlty 4 filters that Scylla can add: q30,LowDP,LowVariantFreq, MultiAllelicSite
        /// We need to check that these get added, if the config requires it.
        public void AdjustHeaderLines()
        {
            var originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(_originalHeader);
            var scyllaFilterLines = _formatter.GenerateFilterStringsByType();

            //Pisces might have used these, but scylla (currently) never does.
            //So we only write them to header if Pisces already has them in the header.
            scyllaFilterLines.Remove(FilterType.RMxN);
            scyllaFilterLines.Remove(FilterType.IndelRepeatLength);
            scyllaFilterLines.Remove(FilterType.NoCall);

            int lastFilterIndex = _originalHeader.FindLastIndex(x=> x.Contains("##FILTER"));

            if (lastFilterIndex == -1)
                lastFilterIndex = Math.Max(_originalHeader.Count-2,-1);

            foreach (var pair in scyllaFilterLines)
            {
                var scyllaFilter = pair.Key;
                var scyllaString = pair.Value;

                if (!originalFilterLines.ContainsKey(scyllaFilter))
                {
                    lastFilterIndex++;
                    _originalHeader.Insert(lastFilterIndex, scyllaString.Replace("\">", ", by Scylla\">"));
                }
                else
                {
                    //we already have this filter listed... but is the string the same? it should be.
                    if (scyllaString.Trim() != originalFilterLines[scyllaFilter].Trim()) //be gentle about line endings..
                    {
                        lastFilterIndex++;
                        _originalHeader.Insert(lastFilterIndex, scyllaString.Replace("\">", ", by Scylla\">"));
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