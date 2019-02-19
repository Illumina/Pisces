using Gemini.CandidateIndelSelection;
using Gemini.Realignment;

namespace Gemini.Interfaces
{
    public interface IGeminiFactory
    {
        IRealigner GetRealigner(string chrom, bool isSnowball, IChromosomeIndelSource indelSource);
        ICategorizedBamAndIndelEvidenceSource GetCategorizationAndEvidenceSource();
        ISamtoolsWrapper GetSamtoolsWrapper();
        IndelPruner GetIndelPruner();
        BasicIndelFilterer GetIndelFilterer();
        ICategorizedBamRealigner GetCategorizedBamRealigner();
    }
}