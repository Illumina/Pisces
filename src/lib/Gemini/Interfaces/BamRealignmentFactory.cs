using System.Collections.Generic;
using BamStitchingLogic;
using Gemini.FromHygea;
using Gemini.IndelCollection;
using Gemini.Infrastructure;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Stitching;
using Gemini.Utility;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini
{
    public class BamRealignmentFactory : IBamRealignmentFactory
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly RealignmentAssessmentOptions _realignmentAssessmentOptions;
        private readonly StitcherOptions _stitcherOptions;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly string _outputDir;

        public BamRealignmentFactory(GeminiOptions geminiOptions,
            RealignmentAssessmentOptions realignmentAssessmentOptions, StitcherOptions stitcherOptions, RealignmentOptions realignmentOptions, string outputDir)
        {
            _geminiOptions = geminiOptions;
            _realignmentAssessmentOptions = realignmentAssessmentOptions;
            _stitcherOptions = stitcherOptions;
            _realignmentOptions = realignmentOptions;
            _outputDir = outputDir;
        }

        
        public ReadPairRealignerAndCombiner GetRealignPairHandler(bool tryRestitch, bool alreadyStitched,
            bool pairAwareRealign,
            Dictionary<int, string> refIdMapping, ReadStatusCounter statusCounter, bool isSnowball,
            IChromosomeIndelSource indelSource, string chromosome, Dictionary<string, IndelEvidence> masterLookup,
            bool hasIndels, Dictionary<HashableIndel, int[]> outcomesLookup, bool skipRestitchIfNothingChanged)
        {
            var stitcher = GetStitcher();

            var stitchedPairHandler = new PairHandler(refIdMapping, stitcher, tryStitch: tryRestitch);

            var judger = new RealignmentJudger(GetAlignmentComparer());

            var readRealigner = new GeminiReadRealigner(GetAlignmentComparer(), remaskSoftclips: _geminiOptions.RemaskMessySoftclips,
                keepProbeSoftclips: _geminiOptions.KeepProbeSoftclip, keepBothSideSoftclips: _geminiOptions.KeepBothSideSoftclips || (_geminiOptions.KeepProbeSoftclip && alreadyStitched),
                trackActualMismatches: _realignmentAssessmentOptions.TrackActualMismatches, checkSoftclipsForMismatches: _realignmentAssessmentOptions.CheckSoftclipsForMismatches,
                debug: _geminiOptions.Debug, maskNsOnly: !(_geminiOptions.RemaskMessySoftclips || _geminiOptions.KeepProbeSoftclip || _geminiOptions.KeepBothSideSoftclips), maskPartialInsertion: _realignmentOptions.MaskPartialInsertion, 
                minimumUnanchoredInsertionLength: _realignmentOptions.MinimumUnanchoredInsertionLength, 
                minInsertionSizeToAllowMismatchingBases: 4, maxProportionInsertSequenceMismatch: 0.2); // TODO fix // TODO figure out what I was saying to fix here...

            IStatusHandler statusHandler = new DebugSummaryStatusHandler(statusCounter);

            if (_geminiOptions.Debug)
            {
                statusHandler = new DebugStatusHandler(statusCounter);
            }

            // Only softclip unknowns if it is not stitched to begin with (we believe in these more, plus it makes our lives simpler for dealing with stitched directions)
            var softclipUnknownIndels = _geminiOptions.SoftclipUnknownIndels && !alreadyStitched;

            //var regionFilterer = new RegionFilterer(chromosome, indelSource.Indels);
            var regionFilterer = new DummyRegionFilterer();
            var collector = GetCollector(isSnowball);
            var realignmentEvaluator = new RealignmentEvaluator(indelSource.DeepCopy(), statusHandler, readRealigner, judger, chromosome,
                _realignmentAssessmentOptions.TrackActualMismatches, _realignmentAssessmentOptions.CheckSoftclipsForMismatches, _geminiOptions.AllowRescoringOrigZero, softclipUnknownIndels, 
                regionFilterer, _geminiOptions.LightDebug);

            return new ReadPairRealignerAndCombiner(
                collector,
                GetRestitcher(stitchedPairHandler, statusHandler),
                realignmentEvaluator,
                GetIndelFinder(pairAwareRealign, chromosome, indelSource), chromosome, alreadyStitched, pairAwareRealign, 
                masterLookup: masterLookup, hasExistingIndels: hasIndels, 
                masterOutcomesLookup: outcomesLookup, skipRestitchIfNothingChanged: skipRestitchIfNothingChanged, allowedToStitch: !_geminiOptions.SkipStitching);
        }

        private IPairSpecificIndelFinder GetIndelFinder(bool pairAwareRealign, string chromosome, IChromosomeIndelSource indelSource)
        {
            if (pairAwareRealign)
            {
                return new PairSpecificIndelFinder();
            }
            else
            {
                return new NonPairSpecificIndelFinder();
            }
        }

        private IReadRestitcher GetRestitcher(PairHandler pairHandler, IStatusHandler statusHandler)
        {
            // TODO also allow not restitch
            return new PostRealignmentStitcher(pairHandler, statusHandler, _stitcherOptions.StringTagsToKeepFromR1);
        }

        private IEvidenceCollector GetCollector(bool isSnowball)
        {
            if (isSnowball)
            {
                return new SnowballEvidenceCollector(new IndelTargetFinder());
            }
            else
            {
                return new NonSnowballEvidenceCollector();
            }
        }

        private AlignmentComparer GetAlignmentComparer()
        {
            if (_geminiOptions.UseHygeaComparer)
            {
                return new BasicAlignmentComparer();
            }
            else
            {
                return new GemBasicAlignmentComparer(_realignmentAssessmentOptions.TrackActualMismatches, _geminiOptions.TrustSoftclips || _geminiOptions.KeepBothSideSoftclips || _geminiOptions.KeepProbeSoftclip);
            }
        }

        private BasicStitcher GetStitcher()
        {
            var stitcher = new BasicStitcher(_stitcherOptions.MinBaseCallQuality,
                useSoftclippedBases: false,
                nifyDisagreements: _stitcherOptions.NifyDisagreements, debug: _stitcherOptions.Debug,
                nifyUnstitchablePairs: _stitcherOptions.NifyUnstitchablePairs,
                ignoreProbeSoftclips: !_stitcherOptions.StitchProbeSoftclips, maxReadLength: _stitcherOptions.MaxReadLength,
                ignoreReadsAboveMaxLength: _stitcherOptions.IgnoreReadsAboveMaxLength,
                thresholdNumDisagreeingBases: _stitcherOptions.MaxNumDisagreeingBases, minMapQuality: _stitcherOptions.FilterMinMapQuality, dontStitchHomopolymerBridge: _stitcherOptions.DontStitchHomopolymerBridge, countNsTowardNumDisagreeingBases: _stitcherOptions.CountNsTowardDisagreeingBases);
            return stitcher;
        }


    }
}