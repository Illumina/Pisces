
namespace Pisces.Domain.Options
{
    public class SampleAggregationParameters
    {

        public enum CombineQScoreMethod
        {
            CombinePoolsAndReCalculate,
            TakeMin
        };

        public float ProbePoolBiasThreshold = 0.5f;
        public CombineQScoreMethod HowToCombineQScore = CombineQScoreMethod.CombinePoolsAndReCalculate;
        public SampleAggregationParameters()
        {
        }
    }
}
