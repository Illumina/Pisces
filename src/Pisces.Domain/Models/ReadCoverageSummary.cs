using SequencingFiles;

namespace Pisces.Domain.Models
{
    public struct ReadCoverageSummary
    {
        public int ClipAdjustedStartPosition;
        public int ClipAdjustedEndPosition;

        public string CigarString  // e.g. 5M2D5M
        {
            get { return Cigar.ToString(); }
            set { Cigar = new CigarAlignment(value); }
        }

        public CigarAlignment Cigar;  
        public string DirectionString  // e.g. 2F6S2R
        {
            get { return DirectionInfo.ToString(); }
            set { DirectionInfo = new DirectionInfo(value); }
        }

        public DirectionInfo DirectionInfo;
    }
}
