using System;

namespace Pisces.Calculators
{
    public static class Poisson
    {
        //This code came unreferenced from an external website. // TODO add reference.
        //It looks like it is adapted from numerical recipes.
        //I tested it against excel.

        //TODO GB: Maybe we should add Unit Tests for this based on expected results calculated from Excel.

        #region members

        private const double Epsilon = 1.0E-20;
        private const double Fpmin = 1.0E-50;
        private const double LanczCutoff = 700.0;
        private const int Itmax = 300;

        #endregion

        /// <summary>
        ///     returns the cumulative distribution function for a Poisson distribution
        /// </summary>
        public static double Cdf(double numOccurrences, double numExpectedOccurrences)
        {
            return IncompleteGammaFunction((int)(numOccurrences + 1.0), numExpectedOccurrences);
        }

        /// <summary>
        ///     returns the incomplete gamma function
        /// </summary>
        private static double IncompleteGammaFunction(double a, double x)
        {
            if ((x < 0) || (a <= 0)) return -1.0;

            double g = (a >= LanczCutoff ? StirlingApproximation(a) : LanczosApproximation(a));

            if (x >= a + 1.0) return GammaUsingContinuedFractions(a, x, g);
            if ((g = GammaSeries(a, x, g)) < 0) return g;

            return 1.0 - g;
        }

        /// <summary>
        ///     returns the incomplete gamma function using continued fractions
        /// </summary>
        private static double GammaUsingContinuedFractions(double a, double x, double g)
        {
            double b = x + 1.0 - a;
            double c = 1.0 / Fpmin;
            double d = 1.0 / b;
            double h = d;

            int i;
            for (i = 1; i <= Itmax; i++)
            {
                double an = i * (a - i);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < Fpmin) d = Fpmin;
                c = b + an / c;
                if (Math.Abs(c) < Fpmin) c = Fpmin;
                d = 1.0 / d;
                double del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < Epsilon) break;
            }

            if (i > Itmax) return -1.0;

            return Math.Exp(a * Math.Log(x) - x - g) * h;
        }

        private static double GammaSeries(double a, double x, double g)
        {
            double retval = -1.0;

            if (x == 0.0) return 0.0;
            if (x < 0.0) return retval;

            double ap = a;
            double sum = 1.0 / a;
            double del = sum;

            for (int i = 1; i <= Itmax; i++)
            {
                ap += 1.0;
                del *= x / ap;
                sum += del;

                if (Math.Abs(del) < Math.Abs(sum) * Epsilon)
                {
                    retval = sum * Math.Exp(a * Math.Log(x) - x - g);
                    break;
                }
            }

            return retval;
        }

        /// <summary>
        ///     returns the Lanczos approximation
        /// </summary>
        private static double LanczosApproximation(double p)
        {
            double x = p;
            double tmp = x + 5.5;
            tmp = tmp - (x + 0.5) * Math.Log(tmp);

            double ser = 1.000000000190015 + 76.18009172947146 / (p + 1.0);
            ser -= 86.50532032941678 / (p + 2.0);
            ser += 24.01409824083091 / (p + 3.0);
            ser -= 1.231739572450155 / (p + 4.0);
            ser += 0.001208650973866179 / (p + 5.0);
            ser -= 5.395239384953E-06 / (p + 6.0);

            return (Math.Log(2.506628274631001 * ser / x) - tmp);
        }

        /// <summary>
        ///     returns Stirling's approximation
        /// </summary>
        private static double StirlingApproximation(double n)
        {
            return (0.5 * Math.Log(2.0 * Math.PI) + (0.5 + n) * Math.Log(n) - n);
        }

        public static double ApproximateLNofNFactorial(double a)
        {
            double g = (a >= LanczCutoff ? StirlingApproximation(a) : LanczosApproximation(a));
            if (a > 100)
                return g;

            g = 0;
            for (int i = 1; i <= a; i++)
            {
                g += Math.Log(i);
            }
            return g;
        }
    }
}