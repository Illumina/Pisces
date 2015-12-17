using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Logic.Processing;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Logic.VariantCalling;
using CallSomaticVariants.Models;
using CallSomaticVariants.Utility;
using SequencingFiles;
using Constants = CallSomaticVariants.Types.Constants;

namespace CallSomaticVariants
{
    public class Factory 
    {
        private readonly ApplicationOptions _options;
        private const char _intervalFileDelimiter = '\t';
        private Dictionary<string, Dictionary<string, List<Region>>> _bamIntervalLookup = new Dictionary<string, Dictionary<string, List<Region>>>();
        public List<BamWorkRequest> WorkRequests { get; private set; }
 
        public Factory(ApplicationOptions options)
        {
            _options = options;

            Constants.DebugMode = options.DebugMode;  // this is a pervasive setting that we'd want available throughout the application

            if (_options.BAMPaths != null)
            {
                UpdateBamIntervals();
                UpdateWorkRequests();
            }
        }

        public string GetCommandLine()
        {
            return _options.CommandLineArguments;
        }

        protected virtual IAlignmentSource CreateAlignmentSource(ChrReference chrReference, string bamFilePath)
        {
            var alignmentExtractor = new BamFileAlignmentExtractor(bamFilePath, _options.StitchReads, chrReference.Name);
            var mateFinder = _options.StitchReads ? new AlignmentMateFinder(Constants.MaxFragmentSize) : null;  // jg todo - do we want to expose this to command line?
            var stitcher = _options.StitchReads ? CreateStitcher() : null;
            var config = new AlignmentSourceConfig
            {
                MinimumMapQuality = _options.MinimumMapQuality,
                OnlyUseProperPairs = _options.OnlyUseProperPairs,
            };
            return new AlignmentSource(alignmentExtractor, mateFinder, stitcher, config);
        }

        protected virtual ICandidateVariantFinder CreateVariantFinder()
        {
            return new CandidateVariantFinder(_options.MinimumBaseCallQuality, _options.MaxSizeMNV, _options.MaxGapBetweenMNV, _options.CallMNVs);
        }

        protected virtual IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet)
        {
            return new AlleleCaller(new VariantCallerConfig
            {
                IncludeReferenceCalls = _options.OutputgVCFFiles,
                MinVariantQscore = _options.MinimumVariantQScore,
                MaxVariantQscore = _options.MaximumVariantQScore,
                VariantQscoreFilterThreshold = _options.FilteredVariantQScore > _options.MinimumVariantQScore ? _options.FilteredVariantQScore : (int?)null,
                MinCoverage = _options.MinimumCoverage,
                MinFrequency = _options.MinimumFrequency,
                EstimatedBaseCallQuality = GetEstimatedBaseCallQuality(),
                StrandBiasModel = _options.StrandBiasModel,
                StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = _options.FilterOutVariantsPresentOnlyOneStrand,
                GenotypeModel = _options.GTModel
            }, intervalSet);
        }

        protected virtual IStateManager CreateStateManager(ChrIntervalSet intervalSet)
        {
            return new RegionStateManager(_options.OutputgVCFFiles, _options.MinimumBaseCallQuality, intervalSet);
        }

        private ChrIntervalSet GetIntervalSet(string chrName, string bamFilePath)
        {
            ChrIntervalSet chrIntervalSet = null;
            if (_bamIntervalLookup.ContainsKey(bamFilePath))
            {
                var bamIntervals = _bamIntervalLookup[bamFilePath];
                var chrRegions = bamIntervals.ContainsKey(chrName)
                    ? bamIntervals[chrName]
                    : new List<Region>();  // empty means intervals applied, but none found for this chromosome

                chrIntervalSet = new ChrIntervalSet(chrRegions, chrName);
                chrIntervalSet.SortAndCollapse(); // make sure intervals are valid
            }

            return chrIntervalSet;
        }

        protected virtual IRegionPadder CreateRegionPadder(ChrReference chrReference, ChrIntervalSet intervalSet, bool includeReference)
        {
            // padder is only required if there are intervals and we are including reference calls
            return intervalSet == null || !_options.OutputgVCFFiles ? null : new RegionPadder(chrReference, intervalSet);
        }

        public virtual ISomaticVariantCaller CreateSomaticVariantCaller(ChrReference chrReference, string bamFilePath, IVcfWriter vcfWriter, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null)
        {
            var alignmentSource = CreateAlignmentSource(chrReference, bamFilePath);
            var variantFinder = CreateVariantFinder();
            var intervalSet = GetIntervalSet(chrReference.Name, bamFilePath);
            var alleleCaller = CreateVariantCaller(chrReference, intervalSet);
            var stateManager = CreateStateManager(intervalSet);
            var intervalPadder = CreateRegionPadder(chrReference, intervalSet, _options.OutputgVCFFiles);

            return new SomaticVariantCaller(alignmentSource, variantFinder, alleleCaller,
                vcfWriter, stateManager, chrReference, intervalPadder, biasFileWriter);
        }

        public VcfFileWriter CreateVcfWriter(string outputVcfPath, VcfWriterInputContext context)
        {
            return new VcfFileWriter(outputVcfPath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = _options.MinimumCoverage > 0 ? _options.MinimumCoverage : (int?)null,
                    QscoreFilterThreshold = _options.FilteredVariantQScore > _options.MinimumVariantQScore ? _options.FilteredVariantQScore : (int?)null,
                    StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria < 1 ? _options.StrandBiasAcceptanceCriteria : (float?)null,
                    FrequencyFilterThreshold = _options.MinimumFrequency,
                    ShouldOutputNoCallFraction = _options.ReportNoCalls,
                    ShouldOutputStrandBiasAndNoiseLevel = _options.OutputNoiseLevelAndStrandBias(),
                    ShouldFilterOnlyOneStrandCoverage = _options.FilterOutVariantsPresentOnlyOneStrand,
                    EstimatedBaseCallQuality = GetEstimatedBaseCallQuality()
                }, context);
        }

        public StrandBiasFileWriter CreateBiasFileWriter(string outputVcfPath)
        {
            var writer = _options.OutputBiasFiles ? new StrandBiasFileWriter(outputVcfPath) : null;
            if (writer!=null)
            {
                writer.WriteHeader();
            }
            return writer;
        }

        public Genome GetReferenceGenome(string genomePath)
        {
            var chromosomeNames = new List<string>();
            var bamWorkRequests =
                WorkRequests.Where(
                    w => w.GenomeDirectory.Equals(genomePath, StringComparison.InvariantCultureIgnoreCase)).ToList();

            for (var i = 0; i < bamWorkRequests.Count(); i++)
            {
                var bamFilePath = bamWorkRequests[i].BamFilePath;
                List<string> bamChromosomes;
                using (var reader = new BamReader(bamFilePath))
                {
                    bamChromosomes = reader.GetReferenceNames();
                }

                // load intervals and filter chromosomes if necessary
                var bamIntervals = _bamIntervalLookup.ContainsKey(bamFilePath) ? _bamIntervalLookup[bamFilePath] : null;        
                chromosomeNames.AddRange(bamIntervals == null ? bamChromosomes : bamChromosomes.Where(bamIntervals.ContainsKey));
            }

            var genome = new Genome(genomePath, chromosomeNames.Distinct().ToList());
            
            return genome;
        }

        public string GetOutputVcfPath(string inputBamPath)
        {
            var vcfOutputPath = Path.ChangeExtension(inputBamPath, _options.OutputgVCFFiles ? "genome.vcf" : ".vcf");

            if (!string.IsNullOrEmpty(_options.OutputFolder))
            {
                vcfOutputPath = Path.Combine(_options.OutputFolder, Path.GetFileName(vcfOutputPath));
            }

            return vcfOutputPath;
        }

        public Dictionary<string, List<Region>> GetIntervals(string inputBamPath)
        {
            return _bamIntervalLookup.ContainsKey(inputBamPath) ? _bamIntervalLookup[inputBamPath] : null;
        }

        private IAlignmentStitcher CreateStitcher()
        {
            if (_options.UseXCStitcher)
            {
                return new XCStitcher(_options.MinimumBaseCallQuality);
            }
            return new BasicStitcher(_options.MinimumBaseCallQuality);
        }

        /// <summary>
        /// Associates bam file with intervals to use.  If no intervals applied, bam file will have no entry in the lookup.
        /// </summary>
        private void UpdateBamIntervals()
        {
            if (_options.IntervalPaths != null)
            {
                for (var i = 0; i < _options.BAMPaths.Length; i ++)
                {
                    var intervalFilePath = _options.IntervalPaths.Length == 1
                        ? _options.IntervalPaths[0]
                        : _options.IntervalPaths[i];

                    try
                    {
                        var regionsByChr = new Dictionary<string, List<Region>>();

                        using (var reader = new StreamReader(intervalFilePath))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                var bits = line.Split(_intervalFileDelimiter);
                                if (bits.Length < 3 || bits[0].Length == 0 || bits[0][0] == '@')
                                    continue; //header or invalid line

                                var chromosomeName = bits[0];
                                if (!regionsByChr.ContainsKey(chromosomeName))
                                    regionsByChr[chromosomeName] = new List<Region>();

                                regionsByChr[chromosomeName].Add(new Region(int.Parse(bits[1]), int.Parse(bits[2])));
                            }
                        }
                        _bamIntervalLookup[_options.BAMPaths[i]] = regionsByChr;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteToLog(
                            "Unable to load interval file '{0}'.  No intervals will be applied to bam '{1}': {2}",
                            intervalFilePath, _options.BAMPaths[i], ex.Message);
                    }
                }
            }
        }

        private int GetEstimatedBaseCallQuality()
        {
            return _options.AppliedNoiseLevel == -1 ? _options.MinimumBaseCallQuality : _options.AppliedNoiseLevel;
        }

        private void UpdateWorkRequests()
        {
            WorkRequests = new List<BamWorkRequest>();

            for (var i = 0; i < _options.BAMPaths.Length; i ++)
            {
                WorkRequests.Add(new BamWorkRequest()
                {
                    BamFilePath = _options.BAMPaths[i],
                    VcfFilePath = GetOutputVcfPath(_options.BAMPaths[i]),
                    GenomeDirectory = _options.GenomePaths.Length == 1 ? _options.GenomePaths[0] : _options.GenomePaths[i],
                });
            }
        }
    }
}