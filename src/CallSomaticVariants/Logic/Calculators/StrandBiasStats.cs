using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants.Logic.Calculators
{
    public class StrandBiasStats
    {
        public double ChanceFalseNeg { get; set; } //(if we did not call the SNP, but it really is a SNP)
        public double ChanceFalsePos { get; set; } //p-value, chance variant freq == Zero given our obs (or greater).
        public double ChanceVarFreqGreaterThanZero { get; set; } // 1 - (chance variant freq == Zero).
        public double Coverage { get; set; }
        public double Frequency { get; set; }
        public double Support { get; set; }

        public StrandBiasStats(double support, double coverage, double noiseFreq, double minDetectableSNP,
            StrandBiasModel strandBiasModel)
        {
            Frequency = support / coverage;
            Support = support;
            Coverage = coverage;

            if (support == 0)
            {
                if (strandBiasModel == StrandBiasModel.Poisson)
                {
                    ChanceFalsePos = 1;
                    ChanceVarFreqGreaterThanZero = 0;
                    ChanceFalseNeg = 0;
                }
                else if (strandBiasModel == StrandBiasModel.Extended)
                {


                    //the chance that we observe the SNP is (minDetectableSNPfreq) for one observation.
                    //the chance that we do not is (1- minDetectableSNPfreq) for one observation.
                    //the chance that we do not observe it, N times in a row is:
                    ChanceVarFreqGreaterThanZero = (Math.Pow(1 - minDetectableSNP, coverage)); //used in SB metric

                    //liklihood that variant really does not exist
                    //= 1 - chance that it does but you did not see it
                    ChanceFalsePos = 1 - ChanceVarFreqGreaterThanZero; //used in SB metric

                    //Chance a low freq variant is at work in the model, and we did not observe it:
                    ChanceFalseNeg = ChanceVarFreqGreaterThanZero;
                }
            }
            else
            {
                // chance of these observations or less, given min observable variant distribution
                ChanceVarFreqGreaterThanZero = Poisson.Cdf(support - 1, coverage * noiseFreq); //used in SB metric
                ChanceFalsePos = 1 - ChanceVarFreqGreaterThanZero; //used in SB metric
                ChanceFalseNeg = Poisson.Cdf(support, coverage * minDetectableSNP);
            }

            //Note:
            //
            // Type 1 error is when we rejected the null hypothesis when we should not have. (we have noise, but called a SNP)
            // Type 2 error is when we accepected the alternate when we should not have. (we have a variant, but we did not call it.)
            //
            // Type 1 error is our this.ChanceFalsePos aka p-value.
            // Type 2 error is out this.ChanceFalseNeg
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
