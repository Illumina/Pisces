using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using VariantPhasing.Utility;

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
            _originalFilterLines = Extensions.GetFilterStringsByType(originalHeader);


            if (_originalFilterLines.ContainsKey(FilterType.RMxN))
                config = ExtractRMxNThresholds(config);

            if (_originalFilterLines.ContainsKey(FilterType.IndelRepeatLength))
                config = ExtractR8Threshold(config);

            //-ReportNoCalls True
            _formatter = new VcfFormatter(config);        
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _phasingCommandLine = phasingCommandLine;
        }

        private VcfWriterConfig ExtractRMxNThresholds(VcfWriterConfig config)
        {
            var filterString = _originalFilterLines[FilterType.RMxN].Split('=')[2].Split(',')[0];
            int m, n;
            var worked = Extensions.IsRMxN(filterString, out m, out n);
            if (worked)
            {
                config.RMxNFilterMaxLengthRepeat = m;
                config.RMxNFilterMinRepetitions = n;
            }

            return config;
        }

        private VcfWriterConfig ExtractR8Threshold(VcfWriterConfig config)
        {
            var filterString = _originalFilterLines[FilterType.IndelRepeatLength].Split('=')[2].Split(',')[0].ToLower();
            var threshold = -1;

            if ((filterString[0] == 'r') && (Extensions.LookForSingleThresholdValue(1, filterString, out threshold)))
                config.IndelRepeatFilterThreshold = threshold;

            return config;
        }


        public override void WriteHeader()
        {
            if (Writer == null)
                throw new Exception("Stream already closed");

            AdjustHeaderLines();
            int offset = 4;
            if (_originalHeader.Count - 1 < offset)
                offset = _originalHeader.Count - 1;

            foreach (var line in _originalHeader.Take(offset))
                Writer.WriteLine(line);

            var currentAssembly = Assembly.GetExecutingAssembly().GetName();        
            Writer.WriteLine("##VariantPhaser=Scylla " + currentAssembly.Version);

	        if (_phasingCommandLine != null)
		        Writer.WriteLine(_phasingCommandLine);
	        

			for(int i = offset; i<_originalHeader.Count();  i++)
                Writer.WriteLine(_originalHeader[i]);
            
        }

        /// There are currenlty 4 filters that Scylla can add: q30,LowDP,LowVariantFreq, MultiAllelicSite
        /// We need to check that these get added, if the config requires it.
        public void AdjustHeaderLines()
        {
            var originalFilterLines = Extensions.GetFilterStringsByType(_originalHeader);
            var scyllaFilterLines = _formatter.GenerateFilterStringsByType();
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
                    _originalHeader.Insert(lastFilterIndex, scyllaString);
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
            //this is a cheap hack, to be removed as soon as I merge another change.
            var comparer = new AlleleComparer();
            var sortedVariants = calledAlleles.OrderBy(a => a, comparer).ThenBy(a => a.Reference).ThenBy(a => a.Alternate);
            base.Write(sortedVariants);
        }
       

    }

    public class AlleleComparer : IComparer<CalledAllele>
    {
        private readonly bool _chrMFirst;

        public AlleleComparer(bool chrMFirst = true)
        {
            _chrMFirst = chrMFirst;
        }

        public int Compare(CalledAllele x, CalledAllele y)
        {
            return OrderVariants(x, y, _chrMFirst);
        }

        public static int OrderVariants(CalledAllele a, CalledAllele b, bool mFirst)
        {
            var vcfVariantA = new VcfVariant { ReferencePosition = a.Coordinate, ReferenceName = a.Chromosome };
            var vcfVariantB = new VcfVariant { ReferencePosition = b.Coordinate, ReferenceName = b.Chromosome };
            return Extensions.OrderVariants(vcfVariantA, vcfVariantB, mFirst);
        }

        public static int OrderVariants(CalledAllele a, VcfVariant b, bool mFirst)
        {
            var vcfVariantA = new VcfVariant { ReferencePosition = a.Coordinate, ReferenceName = a.Chromosome };
            return Extensions.OrderVariants(vcfVariantA, b, mFirst);
        }
    }
}