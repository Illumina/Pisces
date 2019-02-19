using BamStitchingLogic;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Utility;

namespace Gemini
{
    public class GeminiFactory : IGeminiFactory
    {
        private readonly StitcherOptions _stitcherOptions;
        private readonly GeminiOptions _geminiOptions;
        private readonly GeminiSampleOptions _sampleOptions;
        private readonly IndelFilteringOptions _indelFilteringOptions;
        private readonly RealignmentOptions _realignmentOptions;
        private readonly IGeminiDataSourceFactory _dataSourceFactory;
        private readonly IBamRealignmentFactory _realignmentFactory;
        private readonly IGeminiDataOutputFactory _dataOutputFactory;


        public GeminiFactory(StitcherOptions stitcherOptions, GeminiOptions geminiOptions, GeminiSampleOptions sampleOptions, IndelFilteringOptions indelFilteringOptions, RealignmentOptions realignmentOptions, IGeminiDataSourceFactory dataSourceFactory, IBamRealignmentFactory realignmentFactory, IGeminiDataOutputFactory dataOutputFactory)
        {
            _stitcherOptions = stitcherOptions;
            _geminiOptions = geminiOptions;
            _sampleOptions = sampleOptions;
            _indelFilteringOptions = indelFilteringOptions;
            _realignmentOptions = realignmentOptions;
            _dataSourceFactory = dataSourceFactory;
            _realignmentFactory = realignmentFactory;
            _dataOutputFactory = dataOutputFactory;
        }

        public virtual ICategorizedBamAndIndelEvidenceSource GetCategorizationAndEvidenceSource()
        {
            if (_sampleOptions.IntermediateDir != null)
            {
                return new CategorizedBamAndIndelEvidenceSourceFromIntermediateDir(_sampleOptions.IntermediateDir, _geminiOptions.IndelsCsvName);
            }

            else return
            new ReadEvaluator(_stitcherOptions, _sampleOptions.InputBam, _sampleOptions.OutputBam, _geminiOptions, refId: _sampleOptions.RefId);
        }

        public virtual ISamtoolsWrapper GetSamtoolsWrapper()
        {
            var samtoolsWrapper = new SamtoolsWrapper(_geminiOptions.SamtoolsPath, _geminiOptions.IsWeirdSamtools);
            return samtoolsWrapper;
        }

        public virtual IndelPruner GetIndelPruner()
        {
            return new IndelPruner(_geminiOptions.Debug, _indelFilteringOptions.BinSize);
        }

        public virtual BasicIndelFilterer GetIndelFilterer()
        {
            return new BasicIndelFilterer(_indelFilteringOptions.FoundThreshold, (int)
                (_indelFilteringOptions.MinAnchor), _geminiOptions.Debug,
                _indelFilteringOptions.StrictAnchorThreshold,
                _indelFilteringOptions.StrictFoundThreshold,
                _indelFilteringOptions.MaxMess);
        }

        public ICategorizedBamRealigner GetCategorizedBamRealigner()
        {
            // TODO meh I don't like this
            return new CategorizedBamRealigner(_geminiOptions, _sampleOptions, _realignmentOptions, this, _dataSourceFactory);
        }

        public virtual IRealigner GetRealigner(string chrom, bool isSnowball, IChromosomeIndelSource indelSource)
        {
            // TODO 2 different realigners, one that just doesn't realign at all if user specifies stitchOnly?
            var realigner = new SimpleRealigner(_stitcherOptions, indelSource, chrom, _dataSourceFactory, _dataOutputFactory, _realignmentFactory, isSnowball);
            return realigner;
        }
    }
}