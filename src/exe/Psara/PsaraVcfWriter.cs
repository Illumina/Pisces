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

namespace Psara
{
    public class PsaraVcfWriter : VcfFileWriter
    {
        private readonly List<string> _originalHeader;
        private readonly string _psaraCommandLine;
        Dictionary<FilterType, string> _originalFilterLines = new Dictionary<FilterType, string>();

        public PsaraVcfWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, List<string> originalHeader, string phasingCommandLine, int bufferLimit = 2000) : base(outputFilePath, config, context, bufferLimit)
        {
            _originalHeader = originalHeader;
            _originalFilterLines = VcfVariantUtilities.GetFilterStringsByType(originalHeader);
            _formatter = new VcfFormatter(config);
            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;
            _psaraCommandLine = phasingCommandLine;
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

            var currentVersion = FileUtilities.LocalAssemblyVersion<PsaraVcfWriter>();

            Writer.WriteLine("##VcfPostProcessingFilter=Psara " + currentVersion);
            if (_psaraCommandLine != null)
                Writer.WriteLine(_psaraCommandLine);

            for (int i = offset; i < _originalHeader.Count(); i++)
                Writer.WriteLine(_originalHeader[i]);
        }

        /// There are currenlty 0 filters that psara can add. But we expect it will one day add the 'OffTarget' filter.
        /// We need to check that any new filter tags get added, if the config requires it.
        public void AdjustHeaderLines()
        {
            
            //currently empty. Refer to Scylla for example code.

        }


    }
}