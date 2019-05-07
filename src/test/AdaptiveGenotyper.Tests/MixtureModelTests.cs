using System;
using System.Collections.Generic;
using System.IO;
using Pisces.Genotyping;
using Pisces.Calculators;
using Xunit;

namespace AdaptiveGenotyper.Tests
{
    // All assertion data on mixture model tests are obtained from R.
    // Scripts can be found at https://git.illumina.com/Bioinformatics/PiscesTestScripts
    public class MixtureModelTests
    {
        [Fact]
        public void TestMixtureModelOnThreeCoins()
        {
            string file = Path.Combine(TestPaths.LocalTestDataDirectory, "ThreeCoins.csv");
            List<int> k = new List<int>();
            List<int> n = new List<int>();

            using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
            {
                while (true)
                {
                    string line = sr.ReadLine();
                    if (line == null)
                        break;

                    string[] arr = line.Split(',');
                    k.Add(int.Parse(arr[0]));
                    n.Add(int.Parse(arr[1]));
                }
            }

            MixtureModel model;
            model = MixtureModel.FitMixtureModel(k, n,
                new double[] { 0.5686903, 0.3308862, 0.4617437 });



            Assert.True(Math.Abs(model.Means[0] - 0.2335885) < 0.001);
            Assert.True(Math.Abs(model.Means[1] - 0.4100772) < 0.001);
            Assert.True(Math.Abs(model.Means[2] - 0.5074295) < 0.001);
        }

        [Fact]
        public void OutOfOrderStartingMeansTest()
        {
            string file = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr1.csv");
            SparseArray<int> AD = new SparseArray<int>();
            SparseArray<int> DP = new SparseArray<int>();

            using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
            {
                int counter = 0;
                while (true)
                {
                    string line = sr.ReadLine();
                    if (line == null || counter > 100000)
                        break;

                    string[] arr = line.Split(',');
                    int dp = int.Parse(arr[arr.Length - 1]);
                    DP.Add(dp);

                    if (arr.Length == 2)
                        AD.Add(dp - int.Parse(arr[0]));
                    else
                        AD.Add(int.Parse(arr[arr.Length - 2]));
                    counter++;
                }
            }

            var model1 = MixtureModel.FitMixtureModel(AD, DP, new double[] { 0.01, 0.45, 0.99 });
            var model2 = MixtureModel.FitMixtureModel(AD, DP, new double[] { 0.45, 0.01, 0.99 });

            for (int i = 0; i < model1.Means.Length; i++)
            {
                Assert.Equal(model1.Means[i], model2.Means[i], 4);
                Assert.Equal(model1.MixtureWeights[i], model2.MixtureWeights[i], 4);
            }
        }

        [Fact]
        public void TestMixtureModelOnChr1()
        {
            string file = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr1.csv");
            SparseArray<int> AD = new SparseArray<int>();
            SparseArray<int> DP = new SparseArray<int>();

            using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
            {
                var counter = 0;
                while (counter < 20000)
                {
                    string line = sr.ReadLine();
                    string[] arr = line.Split(',');
                    int dp = int.Parse(arr[arr.Length - 1]);
                    DP.Add(dp);

                    if (arr.Length == 2)
                        AD.Add(dp - int.Parse(arr[0]));
                    else
                        AD.Add(int.Parse(arr[arr.Length - 2]));

                    counter++;
                }
            }

            MixtureModel model = MixtureModel.FitMixtureModel(AD, DP);

            Assert.Equal(0.000656, model.Means[0], 3);
            Assert.Equal(0.366, model.Means[1], 3);
            Assert.Equal(0.998, model.Means[2], 3);
        }

        [Fact]
        public void MalformedDataTest()
        {
            SparseArray<int> alleleDepth = new SparseArray<int>();
            SparseArray<int> totalDepth = new SparseArray<int>();
            for (int i = 0; i < 10; i++)
            {
                alleleDepth.Add(0);
                totalDepth.Add(10);
            }

            Assert.Throws<MixtureModelException>(() => MixtureModel.FitMixtureModel(alleleDepth, totalDepth));
        }
    }
}
