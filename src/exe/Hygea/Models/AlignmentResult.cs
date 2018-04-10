using System;
using Alignment.Domain.Sequencing;

namespace RealignIndels.Models
{
    public class AlignmentSummary
    {
        public int NumMismatches { get; set; }
        public int NumIndels { get; set; }
        public int NumIndelBases { get; set; }
        public int NumSoftclips { get; set; }
        public int NumNonNSoftclips { get; set; }
        public int NumNonNMismatches { get; set; }
        public int AnchorLength { get; set; }
        public CigarAlignment Cigar { get; set; }
    }


    public class RealignmentResult : AlignmentSummary
    {
        public int Position { get; set; }
        public bool FailedForLeftAnchor { get; set; }
        public bool FailedForRightAnchor { get; set; }
    }
}
