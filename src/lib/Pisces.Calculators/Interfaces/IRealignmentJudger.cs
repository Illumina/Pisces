using Alignment.Domain.Sequencing;
using ReadRealignmentLogic.Models;

namespace Gemini.Interfaces
{
    public interface IRealignmentJudger
    {
        bool RealignmentIsUnchanged(RealignmentResult realignResult, BamAlignment originalAlignment);
        bool RealignmentBetterOrEqual(RealignmentResult realignResult, AlignmentSummary originalAlignmentSummary,
            bool isPairAware);

        bool IsVeryConfident(AlignmentSummary realignResult);
    }
}