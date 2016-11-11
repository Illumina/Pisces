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
            Frequency = support / coverage;
            Support = support;
            Coverage = coverage;
        }

        public static StrandBiasStats DeepCopy(StrandBiasStats originalStats)
        {
            if (originalStats == null)
                return null;

            var newStats = new StrandBiasStats(originalStats.Support,originalStats.Coverage)
            {
                ChanceFalseNeg = originalStats.ChanceFalseNeg,
                ChanceFalsePos = originalStats.ChanceFalsePos,
                ChanceVarFreqGreaterThanZero = originalStats.ChanceVarFreqGreaterThanZero,
                Frequency = originalStats.Frequency,
            };

            return newStats;
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

        public static StrandBiasResults DeepCopy(StrandBiasResults originalSBresults)
        {
            if (originalSBresults == null)
                return null;

            var sb = new StrandBiasResults()
            {
                BiasAcceptable = originalSBresults.BiasAcceptable,
                BiasScore = originalSBresults.BiasScore,
                GATKBiasScore = originalSBresults.GATKBiasScore,
                VarPresentOnBothStrands = originalSBresults.VarPresentOnBothStrands,
                CovPresentOnBothStrands = originalSBresults.CovPresentOnBothStrands,
                TestAcceptable = originalSBresults.TestAcceptable,
                TestScore = originalSBresults.TestScore,
                ForwardStats = StrandBiasStats.DeepCopy(originalSBresults.ForwardStats),
                OverallStats = StrandBiasStats.DeepCopy(originalSBresults.OverallStats),
                ReverseStats = StrandBiasStats.DeepCopy(originalSBresults.ReverseStats),
                StitchedStats = StrandBiasStats.DeepCopy(originalSBresults.StitchedStats),
            };

            return sb;
        }
    }
}

