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
using Pisces.IO.Sequencing;
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
using StitchingLogic;

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

        public string[] GetCommandLine()
        {
            return _options.CommandLineArguments;
        }

        protected virtual IAlignmentSource CreateAlignmentSource(ChrReference chrReference, string bamFilePath, List<string> chrsToProcess = null)
        {
            AlignmentMateFinder mateFinder = null;
            var alignmentExtractor = new BamFileAlignmentExtractor(bamFilePath, chrReference.Name, _bamIntervalLookup.ContainsKey(bamFilePath) && _options.SkipNonIntervalAlignments ? _bamIntervalLookup[bamFilePath] : null);

            //Warn if the bam has sequences ordered differently to the reference genome.
            //That would confuse us because we will not know how the user wants to order the output gvcf.
            if (alignmentExtractor.SequenceOrderingIsNotConsistent(chrsToProcess))
            {
                Logger.WriteToLog("Warning:  Reference sequences in the bam do not match the order of the reference sequences in the genome. Check bam " + bamFilePath);
                Logger.WriteToLog("Variants will be ordered according to the reference genome");
            }

            var config = new AlignmentSourceConfig
            {
                MinimumMapQuality = _options.MinimumMapQuality,
                OnlyUseProperPairs = _options.OnlyUseProperPairs,
            };
            return new AlignmentSource(alignmentExtractor, mateFinder, config);
        }

   
        protected virtual ICandidateVariantFinder CreateVariantFinder()
        {
            return new CandidateVariantFinder(_options.MinimumBaseCallQuality, _options.MaxSizeMNV, _options.MaxGapBetweenMNV, _options.CallMNVs);
        }

        protected virtual IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet)
        {
            var coverageCalculator = CreateCoverageCalculator();
            var genotypeCalculator = GenotypeCreator.CreateGenotypeCalculator(
                _options.PloidyModel,_options.FilteredVariantFrequency,_options.MinimumDepth, _options.DiploidThresholdingParameters, _options.MinimumGenotypeQScore, _options.MaximumGenotypeQScore);

            return new AlleleCaller(new VariantCallerConfig
            {
                IncludeReferenceCalls = _options.OutputgVCFFiles,
                MinVariantQscore = _options.MinimumVariantQScore,
                MaxVariantQscore = _options.MaximumVariantQScore,
                MinGenotypeQscore = _options.MinimumGenotypeQScore,
                MaxGenotypeQscore = _options.MaximumGenotypeQScore,
                VariantQscoreFilterThreshold = _options.FilteredVariantQScore,
                MinCoverage = _options.MinimumDepth,
                MinFrequency = _options.MinimumFrequency,
                EstimatedBaseCallQuality = GetEstimatedBaseCallQuality(),
                StrandBiasModel = _options.StrandBiasModel,
                StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = _options.FilterOutVariantsPresentOnlyOneStrand,
                GenotypeCalculator = genotypeCalculator,
                VariantFreqFilter = _options.FilteredVariantFrequency,
                LowGTqFilter = _options.LowGenotypeQualityFilter,
                IndelRepeatFilter = _options.IndelRepeatFilter,
                LowDepthFilter = _options.LowDepthFilter,
                ChrReference = chrReference,
                RMxNFilterSettings = new RMxNFilterSettings
                {
                    RMxNFilterMaxLengthRepeat = _options.RMxNFilterMaxLengthRepeat,
                    RMxNFilterMinRepetitions = _options.RMxNFilterMinRepetitions,
                    RMxNFilterFrequencyLimit = _options.RMxNFilterFrequencyLimit
                },
				NoiseModel = _options.NoiseModel				
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

        protected virtual IStateManager CreateStateManager(ChrIntervalSet intervalSet, bool expectStitchedReads=false)
        {
            return new RegionStateManager(_options.OutputgVCFFiles, _options.MinimumBaseCallQuality, expectStitchedReads, intervalSet, 
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
            return intervalSet == null || !_options.OutputgVCFFiles ? null : new RegionMapper(chrReference, intervalSet, _options.MinimumBaseCallQuality);
        }

        public virtual ISomaticVariantCaller CreateSomaticVariantCaller(
            ChrReference chrReference, string bamFilePath, IVcfWriter<CalledAllele> vcfWriter, 
            IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null, List<string> chrToProcess = null)
        {
            var alignmentSource = CreateAlignmentSource(chrReference, bamFilePath, chrToProcess);
            var variantFinder = CreateVariantFinder();
            var intervalSet = GetIntervalSet(chrReference.Name, bamFilePath);
            var alleleCaller = CreateVariantCaller(chrReference, intervalSet);
            var stateManager = CreateStateManager(intervalSet, alignmentSource.SourceIsStitched);
            var intervalPadder = CreateRegionPadder(chrReference, intervalSet, _options.OutputgVCFFiles);

            return new SomaticVariantCaller(alignmentSource, variantFinder, alleleCaller,
                vcfWriter, stateManager, chrReference, intervalPadder, biasFileWriter, intervalSet);
        }

        public VcfFileWriter CreateVcfWriter(string outputVcfPath, VcfWriterInputContext context, IRegionMapper mapper = null)
        {
            return new VcfFileWriter(outputVcfPath,
                new VcfWriterConfig
                {
                    DepthFilterThreshold = _options.OutputgVCFFiles ? _options.MinimumDepth : (_options.LowDepthFilter > _options.MinimumDepth)? _options.LowDepthFilter : (int?)null,
                    IndelRepeatFilterThreshold = _options.IndelRepeatFilter > 0 ? _options.IndelRepeatFilter : (int?)null,
                    VariantQualityFilterThreshold = _options.FilteredVariantQScore,
                    GenotypeQualityFilterThreshold = _options.LowGenotypeQualityFilter.HasValue && _options.LowGenotypeQualityFilter > _options.MinimumVariantQScore ? _options.LowGenotypeQualityFilter : null,
                    StrandBiasFilterThreshold = _options.StrandBiasAcceptanceCriteria < 1 ? _options.StrandBiasAcceptanceCriteria : (float?)null,
                    FrequencyFilterThreshold = ( _options.FilteredVariantFrequency > _options.MinimumFrequency) ? _options.FilteredVariantFrequency : (float?)null,
                    MinFrequencyThreshold = _options.MinimumFrequency,
                    ShouldOutputNoCallFraction = _options.ReportNoCalls,
                    ShouldOutputStrandBiasAndNoiseLevel = _options.OutputNoiseLevelAndStrandBias(),
                    ShouldFilterOnlyOneStrandCoverage = _options.FilterOutVariantsPresentOnlyOneStrand,
                    EstimatedBaseCallQuality = GetEstimatedBaseCallQuality(),
                    ShouldOutputRcCounts = _options.ReportRcCounts,
                    AllowMultipleVcfLinesPerLoci = _options.AllowMultipleVcfLinesPerLoci,
                    PloidyModel = _options.PloidyModel,
                    RMxNFilterMaxLengthRepeat = _options.RMxNFilterMaxLengthRepeat,
                    RMxNFilterMinRepetitions = _options.RMxNFilterMinRepetitions,
                    RMxNFilterFrequencyLimit = _options.RMxNFilterFrequencyLimit,
					NoiseModel = _options.NoiseModel
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