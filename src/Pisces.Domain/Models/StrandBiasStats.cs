namespace Pisces.Domain.Models
{
    public class StrandBiasStats
    {
        public double ChanceFalseNeg { get; set; } //(if we did not call the SNP, but it really is a SNP)
        public double ChanceFalsePos { get; set; } //p-value, chance variant freq == Zero given our obs (or greater).
        public double ChanceVarFreqGreaterThanZero { get; set; } // 1 - (chance variant freq == Zero).
        public double Coverage { get; set; }
        public double Frequency { get; set; }
        public double Support { get; set; }

        public StrandBiasStats(double support, double coverage)
        {
            Frequency = support/coverage;
            Support = support;
            Coverage = coverage;
        }
    }

    public class StrandBiasResults
    {
        public bool BiasAcceptable { get; set; }
        public double BiasScore { get; set; }
        public double GATKBiasScore { get; set; }

        public bool VarPresentOnBothStrands { get; set; }
        public bool CovPresentOnBothStrands { get; set; }

        public bool TestAcceptable { get; set; }
        public double TestScore { get; set; }

        public StrandBiasStats ForwardStats { get; set; }
        public StrandBiasStats OverallStats { get; set; }
        public StrandBiasStats ReverseStats { get; set; }
        public StrandBiasStats StitchedStats { get; set; }
    }
}
