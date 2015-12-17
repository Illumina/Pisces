using System;

namespace CallSomaticVariants.Utility
{
    public static class MathOperations
    {
        public static double QtoP(double q)
        {
            return Math.Pow(10, -1 * q / 10f);
        }

        public static double PtoQ(double p)
        {
            return (-10 * Math.Log10(p));
        }

        public static double PtoGATKBiasScale(double p)
        {
            return 10 * Math.Log10(p);
        }

        public static double GATKBiasScaleToP(double GATK_value)
        {
            return Math.Pow(10, GATK_value / 10.0);
        }

        /// <summary>
        ///     Returns Sp^2
        /// </summary>
        /// <param name="n1">total num samples for data point 1</param>
        /// <param name="n2">total num samples for data point 2</param>
        /// <param name="s12">sigma^2, support for data point 1</param>
        /// <param name="s22">sigma^2, support for data point 2</param>
        /// <returns></returns>
        public static double PooledEstimatorForSigma(double n1, double n2, double s12, double s22)
        {
            return (((n1 - 1) * s12) + ((n2 - 1) * s22)) / (n1 + n2 - 2);
        }

        public static double TwoPopulationTTest(double m1, double m2,
                                                double n1, double n2, double sp)
        {
            return (m1 - m2) / (sp * Math.Sqrt((1 / n1) + (1 / n2)));
        }

        /// <summary>
        /// </summary>
        /// <param name="m1">Support for data point 1</param>
        /// <param name="m2">Support for data point 2</param>
        /// <param name="n1">Coverage for data point 1</param>
        /// <param name="n2">Coverage for data point 2</param>
        /// <param name="levelOfSignificance"> </param>
        /// <returns></returns>
        public static double[] GetTValue(double m1, double m2, double n1, double n2, double levelOfSignificance)
        {
            double degreesOfFreedom = n1 + n2 - 2;

            //if we have  very low depth, refuse to do anything.
            //If we do decide to include t-test values, then i would need to input values for the lookup table here.
            if (degreesOfFreedom < 30)
            {
                return new[] { double.NaN, double.NaN };
            }

            double sp2 = PooledEstimatorForSigma(n1, n2, m1, m2);

            double tvalue = TwoPopulationTTest(m1, m2, n1, n2, sp2);

            return new[] { tvalue, degreesOfFreedom };
        }
    }
}
