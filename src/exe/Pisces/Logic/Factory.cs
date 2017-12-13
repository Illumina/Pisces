using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Logic;
using Pisces.Logic.Alignment;
using Pisces.Logic.VariantCalling;
using Pisces.Domain;
using Pisces.IO.Sequencing;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Options;
using Pisces.Processing;
using Pisces.Processing.Interfaces;
using Pisces.Processing.RegionState;
using Common.IO.Utility;

namespace Pisces
{
    public class Factory : WorkFactory
    {
        private readonly PiscesApplicationOptions _options;
        private const char _intervalFileDelimiter = '\t';
        private Dictionary<string, Dictionary<string, List<Region>>> _bamIntervalLookup = new Dictionary<string, Dictionary<string, List<Region>>>();
        public Dictionary<string, List<CandidateAllele>> _knownVariants = new Dictionary<string, List<CandidateAllele>>();
	    private Dictionary<string, HashSet<Tuple<string, int, string, string>>> _forcedAllelesByChrom = new Dictionary<string, HashSet<Tuple<string, int, string, string>>>();
 
        public Factory(PiscesApplicationOptions options) : base(options)
        {
            _options = options;

            GlobalConstants.DebugMode = options.DebugMode;  // this is a pervasive setting that we'd want available throughout the application

            UpdateWorkRequests();
            UpdateBamIntervals();
            UpdateKnownPriors();
	        GetForcedAlleles();
        }

	    private void GetForcedAlleles()
	    {
		    if(_options.ForcedAllelesFileNames == null || _options.ForcedAllelesFileNames.Count==0) return;
	        foreach (var fileName in _options.ForcedAllelesFileNames)
	        {
	            using (var reader = new VcfReader(fileName,false,false))
	            {
	                foreach (var variant in reader.GetVariants())
	                {
	                    var chr = variant.ReferenceName;
	                    var pos = variant.ReferencePosition;
	                    var refAllele = variant.ReferenceAllele.ToUpper();
	                    var altAlleles = variant.VariantAlleles.Select(x=>x.ToUpper());

	                    if(!_forcedAllelesByChrom.ContainsKey(chr))
	                        _forcedAllelesByChrom[chr] = new HashSet<Tuple<string, int, string, string>>();

	                    foreach (var altAllele in altAlleles)
	                    {
	                        if (!IsValidAlt(altAllele, refAllele))
	                        {
	                            Logger.WriteToLog($"Invalid forced genotyping variant: {variant}");
                                continue;
	                        }
	                        _forcedAllelesByChrom[chr].Add(new Tuple<string, int, string, string>(chr, pos, refAllele, altAllele));
                        }
	                    
                    }   
	            }
	        }
	    }

        private bool IsValidAlt(string altAllele, string refAllele)
        {
            if (refAllele == altAllele) return false;

            if (altAllele.Any(x => x != 'A' && x != 'T' && x != 'C' && x != 'G')) return false;

            return true;
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
                MinimumMapQuality = _options.BamFilterParameters.MinimumMapQuality,
                OnlyUseProperPairs = _options.BamFilterParameters.OnlyUseProperPairs,
                SkipDuplicates = _options.BamFilterParameters.RemoveDuplicates
            };
            return new AlignmentSource(alignmentExtractor, mateFinder, config);
        }

   
        protected virtual ICandidateVariantFinder CreateVariantFinder()
        {
            return new CandidateVariantFinder(_options.BamFilterParameters.MinimumBaseCallQuality, _options.MaxSizeMNV, _options.MaxGapBetweenMNV, _options.CallMNVs);
        }

        protected virtual IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet, IAlignmentSource alignmentSource, HashSet<Tuple<string, int, string, string>> forceGtAlleles = null)
        {
            var coverageCalculator = CreateCoverageCalculator(alignmentSource);
	        var genotypeCalculator = GenotypeCreator.CreateGenotypeCalculator(
		        _options.VariantCallingParameters.PloidyModel, _options.VariantCallingParameters.MinimumFrequencyFilter,
		        _options.VariantCallingParameters.MinimumCoverage,
		        _options.VariantCallingParameters.DiploidThresholdingParameters,
		        _options.VariantCallingParameters.MinimumGenotypeQScore,
		        _options.VariantCallingParameters.MaximumGenotypeQScore, 
                _options.VariantCallingParameters.TargetLODFrequency,
                 _options.VariantCallingParameters.MinimumFrequency,
                chrReference.Name,_options.VariantCallingParameters.IsMale );

			genotypeCalculator.SetMinFreqFilter(_options.VariantCallingParameters.MinimumFrequencyFilter);

            var locusProcessor = _options.VariantCallingParameters.PloidyModel == PloidyModel.Diploid
                ? (ILocusProcessor)new DiploidLocusProcessor()
                : new SomaticLocusProcessor();

            var variantCallerConfig = new VariantCallerConfig
            {
                IncludeReferenceCalls = _options.VcfWritingParameters.OutputGvcfFile,
                MinVariantQscore = _options.VariantCallingParameters.MinimumVariantQScore,
                MaxVariantQscore = _options.VariantCallingParameters.MaximumVariantQScore,
                MinGenotypeQscore = _options.VariantCallingParameters.MinimumGenotypeQScore,
                MaxGenotypeQscore = _options.VariantCallingParameters.MaximumGenotypeQScore,
                VariantQscoreFilterThreshold = _options.VariantCallingParameters.MinimumVariantQScoreFilter,
                MinCoverage = _options.VariantCallingParameters.MinimumCoverage,
                MinFrequency = genotypeCalculator.MinVarFrequency,
                NoiseLevelUsedForQScoring = _options.VariantCallingParameters.NoiseLevelUsedForQScoring,
                StrandBiasModel = _options.VariantCallingParameters.StrandBiasModel,
                StrandBiasFilterThreshold = _options.VariantCallingParameters.StrandBiasAcceptanceCriteria,
                FilterSingleStrandVariants = _options.VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand,
                GenotypeCalculator = genotypeCalculator,
                VariantFreqFilter = genotypeCalculator.MinVarFrequencyFilter,
                LowGTqFilter = _options.VariantCallingParameters.LowGenotypeQualityFilter,
                IndelRepeatFilter = _options.VariantCallingParameters.IndelRepeatFilter,
                LowDepthFilter = _options.VariantCallingParameters.LowDepthFilter,
                ChrReference = chrReference,
                RMxNFilterSettings = new RMxNFilterSettings
                {
                    RMxNFilterMaxLengthRepeat = _options.VariantCallingParameters.RMxNFilterMaxLengthRepeat,
                    RMxNFilterMinRepetitions = _options.VariantCallingParameters.RMxNFilterMinRepetitions,
                    RMxNFilterFrequencyLimit = _options.VariantCallingParameters.RMxNFilterFrequencyLimit
                },
                NoiseModel = _options.VariantCallingParameters.NoiseModel,
                LocusProcessor = locusProcessor
            };

            

            var alleleCaller =  new AlleleCaller(variantCallerConfig, intervalSet, 
            CreateVariantCollapser(chrReference.Name, coverageCalculator),
            coverageCalculator);

			alleleCaller.AddForcedGtAlleles(forceGtAlleles);

	        return alleleCaller;

        }

        protected virtual ICoverageCalculator CreateCoverageCalculator(IAlignmentSource alignmentSource)
        {
            return _options.CoverageMethod == CoverageMethod.Exact
                ? (ICoverageCalculator)new ExactCoverageCalculator()
                : alignmentSource.SourceIsCollapsed && alignmentSource.SourceIsStitched? new CollapsedCoverageCalculator() : new CoverageCalculator();
        }

        protected virtual IVariantCollapser CreateVariantCollapser(string chrName, ICoverageCalculator coverageCalculator)
        {
            return _options.Collapse ? 
                new VariantCollapser(_knownVariants.ContainsKey(chrName) ? _knownVariants[chrName] : null, _options.ExcludeMNVsFromCollapsing, coverageCalculator, _options.CollapseFreqThreshold, 
                _options.CollapseFreqRatioThreshold) 
                : null;
        }

        protected virtual IStateManager CreateStateManager(ChrIntervalSet intervalSet, bool expectStitchedReads=false, bool expectCollapsedReads=true)
        {
            if (expectStitchedReads && expectCollapsedReads)
            {
                // Create CollapsedRegionStateManager if input BAM is collapsed and stitched.
                return new CollapsedRegionStateManager(_options.VcfWritingParameters.OutputGvcfFile,
                    _options.BamFilterParameters.MinimumBaseCallQuality, intervalSet,
                    trackOpenEnded: _options.Collapse, blockSize: GlobalConstants.RegionSize,
                    trackReadSummaries: _options.CoverageMethod == CoverageMethod.Exact);
            }
            // otherwise use the base
            return new RegionStateManager(_options.VcfWritingParameters.OutputGvcfFile,
                _options.BamFilterParameters.MinimumBaseCallQuality, expectStitchedReads, 
                intervalSet,
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
            return intervalSet == null || !_options.VcfWritingParameters.OutputGvcfFile ? null : new RegionMapper(chrReference, intervalSet, _options.BamFilterParameters.MinimumBaseCallQuality);
        }

        public virtual ISomaticVariantCaller CreateSomaticVariantCaller(
            ChrReference chrReference, string bamFilePath, IVcfWriter<CalledAllele> vcfWriter, 
            IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null, List<string> chrToProcess = null)
        {
			var alignmentSource = CreateAlignmentSource(chrReference, bamFilePath, chrToProcess);
            var variantFinder = CreateVariantFinder();
            var intervalSet = GetIntervalSet(chrReference.Name, bamFilePath);
            var forceGtAlleles = SelectForcedAllele(_forcedAllelesByChrom, chrReference.Name, intervalSet);

            var alleleCaller = CreateVariantCaller(chrReference, intervalSet, alignmentSource, forceGtAlleles);
            var stateManager = CreateStateManager(intervalSet, alignmentSource.SourceIsStitched, alignmentSource.SourceIsCollapsed);
            var intervalPadder = CreateRegionPadder(chrReference, intervalSet, _options.VcfWritingParameters.OutputGvcfFile);


            return new SomaticVariantCaller(alignmentSource, variantFinder, alleleCaller,
                vcfWriter, stateManager, chrReference, intervalPadder, biasFileWriter, intervalSet,forceGtAlleles);
        }
        private HashSet<Tuple<string, int, string, string>> SelectForcedAllele(Dictionary<string, HashSet<Tuple<string, int, string, string>>> forcedAllelesByChrom, string referenceName, ChrIntervalSet intervalSet)
        {

            var forcedGtAlleles = _forcedAllelesByChrom.ContainsKey(referenceName) ? _forcedAllelesByChrom[referenceName] : new HashSet<Tuple<string, int, string, string>>();

            if (intervalSet == null) return forcedGtAlleles;
            var allelesInInterval = new HashSet<Tuple<string, int, string, string>>();
            foreach (var allele in forcedGtAlleles)
            {
                if (allele.Item1 == intervalSet.ChrName && intervalSet.ContainsPosition(allele.Item2))
                    allelesInInterval.Add(allele);
            }

            return allelesInInterval;
        }
        public VcfFileWriter CreateVcfWriter(string outputVcfPath, VcfWriterInputContext context, IRegionMapper mapper = null)
        {
            return new VcfFileWriter(outputVcfPath,
                new VcfWriterConfig(_options.VariantCallingParameters,_options.VcfWritingParameters,
                _options.BamFilterParameters, null, _options.DebugMode, _options.OutputBiasFiles,_options.ForcedAllelesFileNames.Count>0), context);
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
            var vcfOutputPath = Path.ChangeExtension(inputBamPath, _options.VcfWritingParameters.OutputGvcfFile ? "genome.vcf" : ".vcf");

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

                        using (var reader = new StreamReader(new FileStream(intervalFilePath, FileMode.Open, FileAccess.Read))) 
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


        private bool SkipPrior(CandidateAllele candidate)
        {
            return candidate.Type == AlleleCategory.Mnv && candidate.ReferenceAllele.Length != candidate.AlternateAllele.Length;
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
                                    knownVariant.ReferenceAllele = knownVariant.ReferenceAllele.Substring(1);
                                    knownVariant.AlternateAllele = knownVariant.AlternateAllele.Substring(1);
                                    knownVariant.ReferencePosition++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}