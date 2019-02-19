using Alignment.Domain.Sequencing;
using Gemini.Interfaces;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;

namespace Gemini.FromHygea
{
    public class RealignmentJudger : IRealignmentJudger
    {
        private readonly AlignmentComparer _alignmentComparer;
        private readonly int _maxRealignShift;
        private readonly bool _tryRealignCleanSoftclippedReads;

        public RealignmentJudger(AlignmentComparer alignmentComparer, int maxRealignShift = 200, bool tryRealignSoftclippedReads = true)
        {
            _alignmentComparer = alignmentComparer;
            _maxRealignShift = maxRealignShift;
            _tryRealignCleanSoftclippedReads = tryRealignSoftclippedReads;
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

        public bool RealignmentBetterOrEqual(RealignmentResult realignResult, AlignmentSummary originalAlignmentSummary, bool isPairAware)
        {
            if (isPairAware)
            {
                return realignResult.NumMismatches - originalAlignmentSummary.NumMismatches <= 2 && 
                    realignResult.NumMatches - originalAlignmentSummary.NumMatches >= 0;
            }
            return _alignmentComparer.CompareAlignmentsWithOriginal(realignResult, originalAlignmentSummary) >= 0;
        }

        public bool PassesSuspicion(AlignmentSummary originalResult)
        {
            var isRealignableSoftclip = _tryRealignCleanSoftclippedReads && originalResult.NumNonNSoftclips > 0;

            if (isRealignableSoftclip) return false;

            if (originalResult.NumMismatches == 0 && originalResult.NumIndels == 0) return true;

            // need to try against one of the priors
            // if (originalResult.NumIndels > 0) return false; 

            // if there are only just mismatches and some are at the tail end of the read, flag it!
            // jg todo make this threshold configurable
            //return originalResult.MinNumAnchorMatches.HasValue 
            //    && originalResult.MinNumAnchorMatches > _anchorSizeThreshold; 

            return false;
        }
    }
}