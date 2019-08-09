using Gemini.CandidateIndelSelection;

namespace Gemini.Interfaces
{
    public interface IGeminiFactory
    {
        IndelPruner GetIndelPruner();
        BasicIndelFilterer GetIndelFilterer();
    }
}