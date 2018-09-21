using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic
{

    public class AlignmentScorer
    {
        public int MismatchCoefficient { get; set; }
        public int IndelCoefficient { get; set; }
        public int IndelLengthCoefficient { get; set; }
        public int NonNSoftclipCoefficient { get; set; }
        public int AnchorLengthCoefficient { get; set; }

        public int GetAlignmentScore(AlignmentSummary summary)
        {
            return 
                MismatchCoefficient * summary.NumMismatches + 
                IndelCoefficient * summary.NumIndels +
                IndelLengthCoefficient * summary.NumIndelBases + 
                NonNSoftclipCoefficient * summary.NumNonNSoftclips + 
                AnchorLengthCoefficient * summary.AnchorLength;
        }
    }
}