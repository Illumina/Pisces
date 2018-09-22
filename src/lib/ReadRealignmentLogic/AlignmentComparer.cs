using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic
{
    public abstract class AlignmentComparer
    {
        public abstract int CompareAlignments(AlignmentSummary preferred, AlignmentSummary other);
        public abstract int CompareAlignmentsWithOriginal(AlignmentSummary preferred, AlignmentSummary other);

        public RealignmentResult GetBetterResult(RealignmentResult preferred, RealignmentResult other)
        {
            if (preferred != null && other != null)
            {
                return CompareAlignments(preferred, other) >= 0 ? preferred : other;  // prefer first if equal
            }

            if (preferred != null)
                return preferred;

            if (other != null)
                return other;

            return null;

        }
    }
}