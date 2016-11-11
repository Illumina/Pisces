using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;

namespace VariantPhasing
{
    public class Factory
    {
        private readonly ApplicationOptions _options;
        public string VcfPath { get { return _options.VcfPath; } }
        public string FilteredNbhd { get { return _options.PhasableVariantCriteria.FilteredNbhdToProcess; } }

        public Factory(ApplicationOptions options)
        {
            _options = options;
        }

        public ApplicationOptions Options
        {
            get { return _options; }
        }

        public int ClusterConstraint
        {
            get { return _options.ClusteringParams.ClusterConstraint; }
        }

        public bool DebugMode
        {
            get { return _options.Debug; }
        }

        public string LogFolder
        {
            get { return _options.LogFolder; }
        }

        protected virtual IVcfVariantSource CreateOriginalVariantSource()
        {
           return new VcfReader(_options.VcfPath);            
        }

        public virtual INeighborhoodBuilder CreateNeighborhoodBuilder()
        {
            return new VcfNeighborhoodBuilder(_options.PhasableVariantCriteria, 
                _options.VariantCallingParams, CreateOriginalVariantSource());
        }

        public virtual IVcfFileWriter<CalledAllele> CreatePhasedVcfWriter()
        {
            //Write header. We can do this at the beginning, it's just copying from old vcf.
            List<string> header;
            using (var reader = new VcfReader(_options.VcfPath))
            {
                header = reader.HeaderLines;
            }
			
            var originalFileName = Path.GetFileName(_options.VcfPath);
            string outputFileName;

            if (originalFileName != null && originalFileName.EndsWith(".genome.vcf"))
            {
                outputFileName = originalFileName.Substring(0, originalFileName.LastIndexOf(".genome.vcf", StringComparison.Ordinal));
                outputFileName = outputFileName + ".phased.genome.vcf";
            }
            else if (originalFileName != null && originalFileName.EndsWith(".vcf"))
            {
                outputFileName = originalFileName.Substring(0, originalFileName.LastIndexOf(".vcf", StringComparison.Ordinal));
                outputFileName = outputFileName + ".phased.vcf";
            }
            else
            {
                throw new Exception(string.Format("Input file is not a VCF file: '{0}'", originalFileName));
            }

            var outFile = Path.Combine(_options.OutFolder, outputFileName);

	        var phasingCommandLine = "##Scylla_cmdline=\"" + _options.CommandLineArguments + "\"";

			return new PhasedVcfWriter(outFile,_options.GetWriterConfig(), new VcfWriterInputContext(), header, phasingCommandLine);
        }

        public VariantCaller CreateVariantCaller()
        {
            return new VariantCaller(_options.VariantCallingParams, _options.BamFilterParams);
        }

        public VcfMerger CreateVariantMerger()
        {
            return new VcfMerger(CreateOriginalVariantSource());
        }

        public NeighborhoodClusterer CreateNeighborhoodClusterer()
        {
            var processor = new NeighborhoodClusterer(_options.ClusteringParams);
            return processor;
        }

        public virtual IVeadGroupSource CreateVeadGroupSource()
        {
            return new VeadGroupSource(new BamFileAlignmentExtractor(_options.BamPath), _options.BamFilterParams, _options.Debug, _options.LogFolder);
        }

    }
}
