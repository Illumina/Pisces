using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Utility;

namespace Gemini
{
    public class GeminiFactory : IGeminiFactory
    {
        private readonly GeminiOptions _geminiOptions;
        private readonly IndelFilteringOptions _indelFilteringOptions;


        public GeminiFactory(GeminiOptions geminiOptions, IndelFilteringOptions indelFilteringOptions)
        {
            _geminiOptions = geminiOptions;
            _indelFilteringOptions = indelFilteringOptions;
        }


        public virtual IndelPruner GetIndelPruner()
        {
            return new IndelPruner(_geminiOptions.Debug, _indelFilteringOptions.BinSize);
        }

        public virtual BasicIndelFilterer GetIndelFilterer()
        {
            return new BasicIndelFilterer(_indelFilteringOptions.FoundThreshold, 
                (_indelFilteringOptions.MinAnchor), _geminiOptions.Debug,
                _indelFilteringOptions.StrictAnchorThreshold,
                _indelFilteringOptions.StrictFoundThreshold,
                _indelFilteringOptions.MaxMess);
        }

    }
}