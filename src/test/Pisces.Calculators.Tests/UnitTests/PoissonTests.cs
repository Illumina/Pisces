using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Calculators;
using TestUtilities;

// http://numerics.mathdotnet.com/Probability.html
// http://numerics.mathdotnet.com/License.html
// (MIT/X11)
//using MathNet.Numerics;

using Xunit;
namespace Pisces.Calculators.Tests
{
    public class PoissonTests
    {


        public double[] ChernoffBd_AssignQValue(double depth, double noise, double callCounts)
        {

            var lambda = (noise * depth);

            double term1 = lambda * Math.Log10(Math.E);
            double term2 = callCounts * Math.Log10(Math.E * lambda);
            double term3 = callCounts * Math.Log10(callCounts);

            double Qnew = 10 * (term1 - term2 + term3);
            return new double[] { 0, Qnew };

        }



        //  Poisson.Cdf(observedCallCount - 1.0, coverage* errorRate));
        public double[] Triangle_AssignQValue(double depth, double noise, double callCounts)
        {

            var poissonDist = new MathNet.Numerics.Distributions.Poisson(noise * depth);
            double rawCDF = poissonDist.CumulativeDistribution(callCounts - 1.0);
            double P = 1 - rawCDF;

            //Approximation to get around precision issues.
            double A = poissonDist.ProbabilityLn((int)callCounts - 1);
            double correction = (callCounts - noise * depth) / callCounts;
            double Qnew = -10.0 * (A - Math.Log(2.0 * correction)) / Math.Log(10.0);

            return new double[] { P, Qnew };

        }

        [Fact]
        public void CheckQScoresWithBadInput()
        {
            //should never happen, but lets be gracefull
            //and not crash

            Assert.Equal(0, VariantQualityCalculator.AssignPoissonQScore(0, 0, 0, 100)); //call count, cov, basecallQ
            Assert.Equal(0, VariantQualityCalculator.AssignPoissonQScore(0, 0, 20, 100)); //call count, cov, basecallQ
            Assert.Equal(0, VariantQualityCalculator.AssignPoissonQScore(0, -1, 20, 100)); //call count, cov, basecallQ
            Assert.Equal(0, VariantQualityCalculator.AssignPoissonQScore(-1, 0, 20, 100)); //call count, cov, basecallQ
            Assert.Equal(0, VariantQualityCalculator.AssignPoissonQScore(-1, -1, 20, 100)); //call count, cov, basecallQ
        }

        [Fact]
        public void CheckQScores()
        {

            //normal execution
            CheckQScoresWithParameters(50);
            CheckQScoresWithParameters(500);
            CheckQScoresWithParameters(1000);
            CheckQScoresWithParameters(10000);
        }


        public List<double[]> GetExcelValuesForDepth500()
        {
            //noise depth   call count      exact P exact Q
            List<double[]> excelExpectedValues = new List<double[]>() {
                new double[] {0.01,500,5,0.559506715,2.521946969},
                new double[] {0.01,500,10,0.031828057,14.97189869},
                new double[] {0.01,500,15,0.000226254,36.45404356},
                new double[] {0.01,500,20,3.45214E-07,64.61912126},
                new double[] {0.01,500,25,1.59959E-10,97.95992099},
                new double[] {0.01,500,30,2.81997E-14,135.4975605},
                };

            return excelExpectedValues;
        }


        //an example calc wiht a very large Q score.
        public List<double[]> GetExpectedValuesForDepth10000()
        {
            //noise depth   call count  freq, chernoff Q, triangle Q
            List<double[]> expChernoffValues = new List<double[]>() {
                new double[] {0.01, 10000, 9995, 0.9995, 156884.8541,156911.8104},
                };


            return expChernoffValues;
        }
        public void CheckQScoresWithParameters(int depth)
        {

            var outputFile = Path.Combine(TestPaths.LocalScratchDirectory, "QScoreCalculations_depth" + depth + ".csv");

            if (File.Exists(outputFile))
                File.Delete(outputFile);

            List<int[]> SampleValues = new List<int[]>() { };//coverage,var calls
            double noise = 0.01;

            for (int i = 5; i < depth;)
            {
                SampleValues.Add(new int[] { depth, i });
                i = i + 5;
            }

            List<double[]> BasicReturnedValues = new List<double[]>();
            List<double[]> RawReturnedValues = new List<double[]>();
            List<double[]> TriangleValues = new List<double[]>();
            List<double[]> ChernoffReturnedValues = new List<double[]>();
            { }//p,Q

            foreach (int[] item in SampleValues)
            {
                double pValue = VariantQualityCalculator.AssignPValue(item[1], item[0], 20);
                double Qscore = MathOperations.PtoQ(pValue);
                double[] Result = new double[] { pValue, Qscore };
                BasicReturnedValues.Add(Result);

                TriangleValues.Add(
                  Triangle_AssignQValue(item[0], noise, item[1]));

                ChernoffReturnedValues.Add(ChernoffBd_AssignQValue(item[0], noise, item[1] - 1));

                RawReturnedValues.Add(new double[]
                   { VariantQualityCalculator.AssignRawPoissonQScore(item[1], item[0], 20) });
            }


            using (StreamWriter sw = new StreamWriter(new FileStream(outputFile, FileMode.CreateNew)))
            {
                sw.WriteLine("noise, depth, call count,vf, exact p, exact Q, triangle approx, chrenoff approx,new Q");

                var excelValuesForDepth500 = GetExcelValuesForDepth500();
                var cherValuesForDepth10000 = GetExpectedValuesForDepth10000();

                for (int i = 0; i < SampleValues.Count; i++)
                {
                    sw.WriteLine(string.Join(",", noise.ToString(),
                        SampleValues[i][0].ToString(), SampleValues[i][1].ToString(), ((double)SampleValues[i][1] / SampleValues[i][0]).ToString(),
                        BasicReturnedValues[i][0], BasicReturnedValues[i][1],
                        TriangleValues[i][1], ChernoffReturnedValues[i][1], RawReturnedValues[i][0]
                        ));


                    //check this never goes to NAN over our range
                    Assert.False(TriangleValues[i][1] == double.NaN);

                    if ((depth == 500) && (i < 6))//this list only has 6 values, before excel looses its lunch due to precision
                    {
                        //noise depth   call count      exact P exact Q
                        Assert.Equal(excelValuesForDepth500[i][3], BasicReturnedValues[i][0], 4);
                        Assert.Equal(excelValuesForDepth500[i][4], BasicReturnedValues[i][1], 4);
                    }

                }


                if (depth == 10000)//checks we have a good dynamic range, but have not wandered off much from the limit.
                {

                    Assert.Equal(cherValuesForDepth10000[0][4], ChernoffReturnedValues[1998][1], 4);
                    Assert.Equal(cherValuesForDepth10000[0][5], RawReturnedValues[1998][0], 4);

                    double error = (ChernoffReturnedValues[1998][1] - RawReturnedValues[1998][0]) / ChernoffReturnedValues[1998][1];
                    Assert.True(error <= 0.03);

                }
            }

        }
    }
}
