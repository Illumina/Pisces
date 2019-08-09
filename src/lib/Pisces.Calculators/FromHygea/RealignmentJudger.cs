using Alignment.Domain.Sequencing;
using Gemini.Interfaces;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;

namespace Gemini.FromHygea
{
    public class RealignmentJudger : IRealignmentJudger
    {
        private readonly AlignmentComparer _alignmentComparer;

        public RealignmentJudger(AlignmentComparer alignmentComparer)
        {
            _alignmentComparer = alignmentComparer;
        }

        public bool RealignmentIsUnchanged(RealignmentResult realignResult,
            BamAlignment originalAlignment)
        {
            if (realignResult.Position - 1 != originalAlignment.Position) return false;

            if (realignResult.Cigar.Count != originalAlignment.CigarData.Count) return false;

            for (int i = 0; i < realignResult.Cigar.Count; i++)
            {
                if (realignResult.Cigar[i].Type != originalAlignment.CigarData[i].Type)
                {
                    return false;
                }
                if (realignResult.Cigar[i].Length != originalAlignment.CigarData[i].Length)
                {
                    return false;
                }
            }

            return true;
        }

        public bool RealignmentBetterOrEqual(RealignmentResult realignResult,
            AlignmentSummary originalAlignmentSummary, bool isPairAware)
        {
            return _alignmentComparer.CompareAlignmentsWithOriginal(realignResult, originalAlignmentSummary, isPairAware) >= 0;
        }

        public bool IsVeryConfident(AlignmentSummary realignResult)
        {
            return realignResult.AnchorLength > 10 && realignResult.NumMismatches <= 1;
        }
    }
}