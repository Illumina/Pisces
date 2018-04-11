using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hygea.Logic;
using RealignIndels.Interfaces;
using RealignIndels.Logic.TargetCalling;
using Pisces.IO.Sequencing;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.Processing;
using Pisces.Processing.RegionState;
using RealignIndels.Models;

namespace RealignIndels.Logic
{
    public class Factory : WorkFactory
    {
        private readonly HygeaOptions _options;
        public Dictionary<string, List<CandidateAllele>> _knownVariants = new Dictionary<string, List<CandidateAllele>>();
 
        public Factory(HygeaOptions options) : base(options)
        {
            _options = options;

            UpdateWorkRequests();
            UpdateKnownPriors();
        }

        public override string GetOutputFile(string inputBamPath)
        {
            var outputPath = inputBamPath;

            if (!string.IsNullOrEmpty(_options.OutputDirectory))
            {
                outputPath = Path.Combine(_options.OutputDirectory, Path.GetFileName(outputPath));
            }

            return outputPath;
        }

        private void UpdateKnownPriors()
        {
            if (!string.IsNullOrEmpty(_options.PriorsPath))
            {
                using (var reader = new VcfReader(_options.PriorsPath))
                {
                    _knownVariants = reader.GetVariantsByChromosome(true, true, new List<AlleleCategory> { AlleleCategory.Insertion, AlleleCategory.Deletion });
                }
            }
        }

        protected override List<string> FilterBamChromosomes(List<string> bamChromosomes, string bamFilePath)
        {
            return string.IsNullOrEmpty(_options.ChromosomeFilter) ? bamChromosomes : bamChromosomes.Where(c => c == _options.ChromosomeFilter).ToList();
        }

        public virtual IChrRealigner CreateRealigner(ChrReference chrReference, string bamFilePath, IRealignmentWriter writer)
        {
            var knownIndels = _knownVariants == null || !_knownVariants.ContainsKey(chrReference.Name) ? 
                null : _knownVariants[chrReference.Name];

            AlignmentScorer alignmentScorer = null;
            if (_options.UseAlignmentScorer)
            {
                alignmentScorer = new AlignmentScorer()
                {
                    AnchorLengthCoefficient = _options.AnchorLengthCoefficient,
                    MismatchCoefficient = _options.MismatchCoefficient,
                    IndelCoefficient = _options.IndelCoefficient,
                    NonNSoftclipCoefficient = _options.SoftclipCoefficient,
                    IndelLengthCoefficient = _options.IndelLengthCoefficient
                };
            }

            return new ChrRealigner(chrReference,
                CreateAlignmentExtractor(bamFilePath, chrReference.Name),
                CreateAlignmentExtractor(bamFilePath, chrReference.Name),
                CreateCandidateFinder(), 
                CreateRanker(), 
                CreateCaller(),
                new RealignStateManager(_options.MinimumBaseCallQuality, _options.RealignWindowSize), 
                writer, 
                knownIndels, 
                _options.MaxIndelSize,
                _options.TryThree,
                skipDuplicates: _options.SkipDuplicates,
                skipAndRemoveDuplicates: _options.SkipAndRemoveDuplicates,
                remaskSoftclips: _options.RemaskSoftclips, maskPartialInsertion: _options.MaskPartialInsertion, allowRescoringOrig0: _options.AllowRescoringOrigZero,
                maxRealignShift: _options.MaxRealignShift,
                tryRealignCleanSoftclippedReads: _options.TryRealignSoftclippedReads,
                alignmentScorer: alignmentScorer,
                debug: _options.Debug

                );

        }

        public virtual IAlignmentExtractor CreateAlignmentExtractor(string bamFilePath, string chromosomeName)
        {
            return new BamFileAlignmentExtractor(bamFilePath, chromosomeName);
        }

        public virtual IIndelCandidateFinder CreateCandidateFinder()
        {
            return new IndelTargetFinder(_options.MinimumBaseCallQuality);
        }

        public virtual IIndelRanker CreateRanker()
        {
            return new IndelRanker();
        }

        public virtual ITargetCaller CreateCaller()
        {
            return new IndelTargetCaller(_options.IndelFreqCutoff);
        }

        public virtual IRealignmentWriter CreateWriter(string bamFilePath, string outputFilePath)
        {
            return new RemapBamWriter(bamFilePath, outputFilePath, createIndex: !_options.InsideSubProcess, copyUnaligned: !_options.InsideSubProcess, maxRealignShift: _options.MaxRealignShift);  
        }
    }
}
