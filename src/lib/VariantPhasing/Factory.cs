using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Common.IO.Utility;
using Alignment.IO.Sequencing;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;

namespace VariantPhasing
{
    public class Factory
    {
        private readonly ScyllaApplicationOptions _options;
        private readonly Genome _genome;
        public string VcfPath { get { return _options.VcfPath; } }
        public string FilteredNbhd { get { return _options.PhasableVariantCriteria.FilteredNbhdToProcess; } }

        public Factory(ScyllaApplicationOptions options)
        {
            _options = options;

            if (options.GenomePath == null)
            {
                Logger.WriteWarningToLog("No reference genome was supplied by the user. All reference bases will be output as 'R'. ");
            }
            else
            {
                _genome = SetGenome(options);
            }
        }

        public ScyllaApplicationOptions Options
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

        public Genome SetGenome(ScyllaApplicationOptions options)
        {
            var bamChromosomes = new List<string>() { };
            using (var reader = new BamReader(options.BamPath))
            {
                bamChromosomes = reader.GetReferenceNames();
            }
            return (new Genome(options.GenomePath, bamChromosomes));
        }

        public virtual IAlleleSource CreateOriginalVariantSource()
        {
            return new AlleleReader(_options.VcfPath);
        }

        public virtual INeighborhoodBuilder CreateNeighborhoodBuilder(int batchSize)
        {
            return new NeighborhoodBuilder(_options.PhasableVariantCriteria,
                _options.VariantCallingParams, CreateOriginalVariantSource(), _genome, batchSize);
        }

        public virtual IVcfFileWriter<CalledAllele> CreatePhasedVcfWriter()
        {
            //Write header. We can do this at the beginning, it's just copying from old vcf.
            List<string> header = AlleleReader.GetAllHeaderLines(_options.VcfPath);

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
                throw new InvalidDataException(string.Format("Input file is not a VCF file: '{0}'", originalFileName));
            }

            var outFile = Path.Combine(_options.OutputDirectory, outputFileName);

            var phasingCommandLine = "##Scylla_cmdline=" + _options.QuotedCommandLineArgumentsString;

            return new PhasedVcfWriter(outFile,
                new VcfWriterConfig(_options.VariantCallingParams, _options.VcfWritingParams, _options.BamFilterParams, null, _options.Debug, false),
                new VcfWriterInputContext(), header, phasingCommandLine);
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
            return new VeadGroupSource(new BamFileAlignmentExtractor(_options.BamPath, false), _options.BamFilterParams, _options.Debug, _options.LogFolder);
        }

        public virtual MNVSoftClipSupportFinder CreateSoftClipSupportFinder()
        {
            return new MNVSoftClipSupportFinder(new BamFileAlignmentExtractor(_options.BamPath, false),
                new MNVClippedReadComparator(new MNVSoftClipReadFilter()),
                _options.BamFilterParams.MinimumBaseCallQuality,
                _options.VariantCallingParams.MaximumVariantQScore,
                _options.SoftClipSupportParams.MinSizeForClipRescue);
        }
    }
}
