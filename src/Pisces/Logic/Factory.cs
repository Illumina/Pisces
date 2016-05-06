using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallVariants.Logic;
using Pisces.Interfaces;
using Pisces.Logic;
using Pisces.Logic.Alignment;
using Pisces.Logic.VariantCalling;
using Pisces.Types;
using SequencingFiles;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Processing;
using Pisces.Processing.Interfaces;
using Pisces.Processing.RegionState;
using Pisces.Processing.Utility;

namespace Pisces
{
    public class Factory : WorkFactory
    {
        private readonly ApplicationOptions _options;
        private const char _intervalFileDelimiter = '\t';
        private Dictionary<string, Dictionary<string, List<Region>>> _bamIntervalLookup = new Dictionary<string, Dictionary<string, List<Region>>>();
        public Dictionary<string, List<CandidateAllele>> _knownVariants = new Dictionary<string, List<CandidateAllele>>();
 
        public Factory(ApplicationOptions options) : base(options)
        {
            _options = options;

            GlobalConstants.DebugMode = options.DebugMode;  // this is a pervasive setting that we'd want available throughout the application

            UpdateWorkRequests();
            UpdateBamIntervals();
            UpdateKnownPriors();
        }

        public string GetCommandLine()
        {
            return _options.CommandLineArguments;
        }

        protected virtual IAlignmentSource CreateAlignmentSource(ChrReference chrReference, string bamFilePath)
        {
            var mateFinder = _options.StitchReads ? new AlignmentMateFinder(_options.MaxFragmentSize) : null;
            var alignmentExtractor = new BamFileAlignmentExtractor(bamFilePath, chrReference.Name, _bamIntervalLookup.ContainsKey(bamFilePath) && _options.SkipNonIntervalAlignments ? _bamIntervalLookup[bamFilePath] : null);
            var stitcher = _options.StitchReads ? CreateStitcher() : null;
            var config = new AlignmentSourceConfig
            {
                MinimumMapQuality = _options.MinimumMapQuality,
                OnlyUseProperPairs = _options.OnlyUseProperPairs,
                UnstitchableStrategy = _options.UnstitchableStrategy
            };
            return new AlignmentSource(alignmentExtractor, mateFinder, stitcher, config);
        }

        protected virtual ICandidateVariantFinder CreateVariantFinder()
        {
            return new CandidateVariantFinder(_options.MinimumBaseCallQuality, _options.MaxSizeMNV, _options.MaxGapBetweenMNV, _options.CallMNVs);
        }

        protected virtual IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet)
        {
            var coverageCalculator = CreateCoverageCalculator();

            return new AlleleCaller(new VariantCallerConfig
            {
                IncludeReferenceCalls = _options.OutputgVCFFiles,
                MinVariantQscore = _options.MinimumVariantQScore,
                MaxVariantQscore = _options.MaximumVariantQScore,
                MinGenotypeQscore = _options.MinimumGenotypeQScore,
                MaxGenotypeQscore = _options.MaximumGenotypeQScore,
                VariantQscoreFilterThreshold = _options.FilteredVariantQScore > _options.MinimumVariantQScore ? _options.FilteredVariantQScore : (int?)null,
                MinCoverage = _options.MinimumCoverage,
                MinFrequency = _options.MinimumFrequency,
                EstimatedBaseCallQuality = GetEstimatedBaseCallQuality(),
                StrandBiasModel = _options.StrandBiasModel,
                StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = _options.FilterOutVariantsPresentOnlyOneStrand,
                GenotypeModel = _options.GTModel,
                PloidyModel = _options.PloidyModel,
                DiploidThresholdingParameters = _options.DiploidThresholdingParameters,
                VariantFreqFilter = _options.FilteredVariantFrequency,
                LowGTqFilter = _options.LowGenotypeQualityFilter,
                IndelRepeatFilter = _options.IndelRepeatFilter,
                LowDepthFilter = _options.LowDepthFilter,
                ChrReference = chrReference,
                RMxNFilterMaxLengthRepeat = _options.RMxNFilterMaxLengthRepeat,
                RMxNFilterMinRepetitions = _options.RMxNFilterMinRepetitions
            }, intervalSet, 
            CreateVariantCollapser(chrReference.Name, coverageCalculator),
            coverageCalculator);
        }

        protected virtual ICoverageCalculator CreateCoverageCalculator()
        {
            return _options.CoverageMethod == CoverageMethod.Exact
                ? (ICoverageCalculator)new ExactCoverageCalculator()
                : new CoverageCalculator();
        }

        protected virtual IVariantCollapser CreateVariantCollapser(string chrName, ICoverageCalculator coverageCalculator)
        {
            return _options.Collapse ? 
                new VariantCollapser(_knownVariants.ContainsKey(chrName) ? _knownVariants[chrName] : null, coverageCalculator, _options.CollapseFreqThreshold, _options.CollapseFreqRatioThreshold) 
                : null;
        }

        protected virtual IStateManager CreateStateManager(ChrIntervalSet intervalSet)
        {
            return new RegionStateManager(_options.OutputgVCFFiles, _options.MinimumBaseCallQuality, intervalSet,
                trackOpenEnded: _options.Collapse, blockSize: GlobalConstants.RegionSize, 
                trackReadSummaries: _options.CoverageMethod == CoverageMethod.Exact);
        }

        private ChrIntervalSet GetIntervalSet(string chrName, string bamFilePath)
        {
            ChrIntervalSet chrIntervalSet = null;
            if (_bamIntervalLookup.ContainsKey(bamFilePath))
            {
                var bamIntervals = _bamIntervalLookup[bamFilePath];
                var chrRegions = new List<Region>();
                if (bamIntervals.ContainsKey(chrName))
                    chrRegions.AddRange(bamIntervals[chrName]);
                // empty means intervals applied, but none found for this chromosome

                chrIntervalSet = new ChrIntervalSet(chrRegions, chrName);
                chrIntervalSet.SortAndCollapse(); // make sure intervals are valid
            }

            return chrIntervalSet;
        }

       protected virtual IRegionMapper CreateRegionPadder(ChrReference chrReference, ChrIntervalSet intervalSet, bool includeReference)
        {
            // padder is only required if there are intervals and we are including reference calls
            return intervalSet == null || !_options.OutputgVCFFiles ? null : new RegionMapper(chrReference, intervalSet);
        }

        public virtual ISomaticVariantCaller CreateSomaticVariantCaller(ChrReference chrReference, string bamFilePath, IVcfWriter<BaseCalledAllele> vcfWriter, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null)
        {
            var alignmentSource = CreateAlignmentSource(chrReference, bamFilePath);
            var variantFinder = CreateVariantFinder();
            var intervalSet = GetIntervalSet(chrReference.Name, bamFilePath);
            var alleleCaller = CreateVariantCaller(chrReference, intervalSet);
            var stateManager = CreateStateManager(intervalSet);
            var intervalPadder = CreateRegionPadder(chrReference, intervalSet, _options.OutputgVCFFiles);

            return new SomaticVariantCaller(alignmentSource, variantFinder, alleleCaller,
                vcfWriter, stateManager, chrReference, intervalPadder, biasFileWriter, intervalSet);
        }

        public VcfFileWriter CreateVcfWriter(string outputVcfPath, VcfWriterInputContext context, IRegionMapper mapper = null)
        {
            return new VcfFileWriter(outputVcfPath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = _options.MinimumCoverage > 0 ? _options.MinimumCoverage : (int?)null,
                    IndelRepeatFilterThreshold = _options.IndelRepeatFilter > 0 ? _options.IndelRepeatFilter : (int?)null,
                    VariantQualityFilterThreshold = _options.FilteredVariantQScore > _options.MinimumVariantQScore ? _options.FilteredVariantQScore : (int?)null,
                    GenotypeQualityFilterThreshold = _options.LowGenotypeQualityFilter.HasValue && _options.LowGenotypeQualityFilter > _options.MinimumVariantQScore ? _options.LowGenotypeQualityFilter : null,
                    StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria < 1 ? _options.StrandBiasAcceptanceCriteria : (float?)null,
                    FrequencyFilterThreshold = _options.FilteredVariantFrequency,
                    MinFrequencyThreshold = _options.MinimumFrequency,
                    ShouldOutputNoCallFraction = _options.ReportNoCalls,
                    ShouldOutputStrandBiasAndNoiseLevel = _options.OutputNoiseLevelAndStrandBias(),
                    ShouldFilterOnlyOneStrandCoverage = _options.FilterOutVariantsPresentOnlyOneStrand,
                    EstimatedBaseCallQuality = GetEstimatedBaseCallQuality(),
                    ShouldOutputRcCounts = _options.ReportRcCounts,
                    AllowMultipleVcfLinesPerLoci = _options.AllowMultipleVcfLinesPerLoci,
                    PloidyModel = _options.PloidyModel,
                    RMxNFilterMaxLengthRepeat = _options.RMxNFilterMaxLengthRepeat,
                    RMxNFilterMinRepetitions = _options.RMxNFilterMinRepetitions
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

        protected override List<string> FilterBamChromosomes(List<string> bamChromosomes, string bamFilePath)
        {
            // load intervals and filter chromosomes if necessary
            var bamIntervals = _bamIntervalLookup.ContainsKey(bamFilePath) ? _bamIntervalLookup[bamFilePath] : null;
            return bamIntervals == null ? bamChromosomes : bamChromosomes.Where(bamIntervals.ContainsKey).ToList();
        }

        public override string GetOutputFile(string inputBamPath)
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
            return new BasicStitcher(_options.MinimumBaseCallQuality, _options.NifyDisagreements);
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

                        // sort regions
                        foreach(var chrRegions in regionsByChr.Values)
                            chrRegions.Sort((r1, r2) => r1.StartPosition.CompareTo(r2.StartPosition));

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

        private bool SkipPrior(CandidateAllele candidate)
        {
            return candidate.Type == AlleleCategory.Mnv && candidate.Reference.Length != candidate.Alternate.Length;
        }

        private void UpdateKnownPriors()
        {
            if (!string.IsNullOrEmpty(_options.PriorsPath))
            {
                using (var reader = new VcfReader(_options.PriorsPath))
                {
                    _knownVariants = reader.GetVariantsByChromosome(true, true, new List<AlleleCategory> { AlleleCategory.Insertion, AlleleCategory.Mnv }, doSkipCandidate: SkipPrior);
                    if (_options.TrimMnvPriors)
                    {
                        foreach (var knownVariantList in _knownVariants.Values)
                        {
                            foreach (var knownVariant in knownVariantList)
                            {
                                if (knownVariant.Type == AlleleCategory.Mnv)
                                {
                                    knownVariant.Reference = knownVariant.Reference.Substring(1);
                                    knownVariant.Alternate = knownVariant.Alternate.Substring(1);
                                    knownVariant.Coordinate++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}