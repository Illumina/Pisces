namespace ReadRealignmentLogic.Models
{
    public class RealignmentResult : AlignmentSummary
    {
        public int Position { get; set; }
        public bool FailedForLeftAnchor { get; set; }
        public bool FailedForRightAnchor { get; set; }
        public string Indels { get; set; }

    }
}
