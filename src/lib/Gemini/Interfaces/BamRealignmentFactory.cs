using System.Collections.Generic;
using System.IO;
using Alignment.IO;
using BamStitchingLogic;
using Gemini.FromHygea;
using Gemini.IndelCollection;
using Gemini.Infrastructure;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Stitching;
using Gemini.Utility;
using ReadRealignmentLogic;
using StitchingLogic;

namespace Gemini
{
    public class GeminiDataOutputFactory : IGeminiDataOutputFactory
    {
        private readonly int _numThreads;

        public GeminiDataOutputFactory(int numThreads)
        {
            _numThreads = numThreads;
        }
        public IBamWriterFactory GetBamWriterFactory(string inBam)
        {
            var bamWriterFactory = new BamWriterFactory(_numThreads, inBam);
            return bamWriterFactory;
        }

        public TextWriter GetTextWriter(string outFile)
        {
            return new StreamWriter(outFile);
        }
    }
    public class BamRealignmentFactory : IBamRealignmentFactory
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly RealignmentAssessmentOptions _realignmentAssessmentOptions;
        private readonly StitcherOptions _stitcherOptions;

        public BamRealignmentFactory(GeminiOptions geminiOptions,
            RealignmentAssessmentOptions realignmentAssessmentOptions, StitcherOptions stitcherOptions)
        {
            _geminiOptions = geminiOptions;
            _realignmentAssessmentOptions = realignmentAssessmentOptions;
            _stitcherOptions = stitcherOptions;
        }

        
        public IReadPairHandler GetRealignPairHandler(bool tryRestitch, bool alreadyStitched, bool pairAwareRealign,
            Dictionary<int, string> refIdMapping, ReadStatusCounter statusCounter, bool isSnowball, IChromosomeIndelSource indelSource, string chromosome, Dictionary<string, int[]> masterLookup)
        {
            var stitcher = GetStitcher();

            var stitchedPairHandler = new PairHandler(refIdMapping, stitcher, statusCounter, tryStitch: tryRestitch);

            var judger = new RealignmentJudger(GetAlignmentComparer());

            var readRealigner = new FromHygea.GeminiReadRealigner(GetAlignmentComparer(),
                keepProbeSoftclips: _geminiOptions.KeepProbeSoftclip, keepBothSideSoftclips: _geminiOptions.KeepBothSideSoftclips || (_geminiOptions.KeepProbeSoftclip && alreadyStitched),
                trackActualMismatches: _realignmentAssessmentOptions.TrackActualMismatches, checkSoftclipsForMismatches: _realignmentAssessmentOptions.CheckSoftclipsForMismatches,
                debug: _geminiOptions.Debug); // TODO fix

            IStatusHandler statusHandler = new DebugSummaryStatusHandler(statusCounter);

            if (_geminiOptions.Debug)
            {
                statusHandler = new DebugStatusHandler(statusCounter);
            }

            // Only softclip unknowns if it is not stitched to begin with (we believe in these more, plus it makes our lives simpler for dealing with stitched directions)
            var softclipUnknownIndels = _geminiOptions.SoftclipUnknownIndels && !alreadyStitched;

            var collector = GetCollector(isSnowball);
            var realignmentEvaluator = new RealignmentEvaluator(indelSource.DeepCopy(), statusHandler, readRealigner, judger, chromosome,
                _realignmentAssessmentOptions.TrackActualMismatches, _realignmentAssessmentOptions.CheckSoftclipsForMismatches, _geminiOptions.AllowRescoringOrigZero, softclipUnknownIndels);

            return new ReadPairRealignerAndCombiner(
                collector,
                GetRestitcher(stitchedPairHandler, statusHandler),
                realignmentEvaluator,
                GetIndelFinder(pairAwareRealign, chromosome, indelSource), chromosome, alreadyStitched, pairAwareRealign);
        }

        private IPairSpecificIndelFinder GetIndelFinder(bool pairAwareRealign, string chromosome, IChromosomeIndelSource indelSource)
        {
            if (pairAwareRealign)
            {
                return new PairSpecificIndelFinder(chromosome, indelSource.DeepCopy());
            }
            else
            {
                return new NonPairSpecificIndelFinder();
            }
        }

        private IReadRestitcher GetRestitcher(PairHandler pairHandler, IStatusHandler statusHandler)
        {
            // TODO also allow not restitch
            return new PostRealignmentStitcher(pairHandler, statusHandler);
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
                return new GemBasicAlignmentComparer(_realignmentAssessmentOptions.TrackActualMismatches);
            }
        }

        private BasicStitcher GetStitcher()
        {
            var stitcher = new BasicStitcher(_stitcherOptions.MinBaseCallQuality,
                useSoftclippedBases: _stitcherOptions.UseSoftClippedBases,
                nifyDisagreements: _stitcherOptions.NifyDisagreements, debug: _stitcherOptions.Debug,
                nifyUnstitchablePairs: _stitcherOptions.NifyUnstitchablePairs,
                ignoreProbeSoftclips: !_stitcherOptions.StitchProbeSoftclips, maxReadLength: _stitcherOptions.MaxReadLength,
                ignoreReadsAboveMaxLength: _stitcherOptions.IgnoreReadsAboveMaxLength,
                thresholdNumDisagreeingBases: _stitcherOptions.MaxNumDisagreeingBases);
            return stitcher;
        }


    }
}