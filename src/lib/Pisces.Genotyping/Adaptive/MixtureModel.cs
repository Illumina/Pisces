using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Types;
using Common.IO.Utility;
using System.Linq;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

namespace Pisces.Genotyping
{
    public class MixtureModelParameters
    {
        public double[] Means { get; set; }
        public double[] Priors { get; set; }
    }
    public class MixtureModelResult
    {
        public SimplifiedDiploidGenotype GenotypeCategory { get; set; } // 0/0, 0/1 or 1/1
        public int QScore { get; set; }
        public float[] GenotypePosteriors { get; set; }

        public int GenotypeCategoryAsInt
        {
            get { return GetGenoptypeCategoryAsInt(); }
            set { GenotypeCategory = IntToGenotypeCategory(value); }
        }

        private int GetGenoptypeCategoryAsInt()
        {
            switch (GenotypeCategory)
            {
                case SimplifiedDiploidGenotype.HomozygousRef:
                    return 0;
                case SimplifiedDiploidGenotype.HeterozygousAltRef:
                    return 1;
                case SimplifiedDiploidGenotype.HomozygousAlt:
                    return 2;
                // This is technically unreachable for now, but it future proofs this class
                default:
                    throw new ArgumentException("The given genotype is not allowed." + GenotypeCategory);
            }
        }

        public static int GenotypeCategoryToInt(SimplifiedDiploidGenotype genotype)
        {
            return (int)genotype;
        }

        public static SimplifiedDiploidGenotype IntToGenotypeCategory(int i)
        {
            switch (i)
            {
                case 0:
                    return SimplifiedDiploidGenotype.HomozygousRef;
                case 1:
                    return SimplifiedDiploidGenotype.HeterozygousAltRef;
                case 2:
                    return SimplifiedDiploidGenotype.HomozygousAlt;
                default:
                    throw new ArgumentException("The given genotype number  is not allowed." + i);
            }
        }
    }

    public class MixtureModel
    {
        private static readonly float _maxQScore = 100F;
        private static readonly double[] _defaultMeans = new double[] { 0.01, 0.45, 0.95 };
        private static readonly int[] _defaultQScoreEffectiveN = { 25, 25, 10 };

        private readonly IList<int> AlleleDepths;
        private readonly IList<int> TotalDepths;
        private readonly double[,] Posteriors;
        private readonly int[] ClusterCounts;

        #region Properties
        public double[] Means { get; private set; }
        public IList<int> Clustering { get; }
        public double[] MixtureWeights { get; private set; }
        public int[] QScores { get; }
        public List<double> LogLikelihoods { get; } = new List<double>();
        public List<float[]> PhredPosteriors { get; } = new List<float[]>();
        public int[] QScoreEffectiveN { get; private set; }
        public MixtureModelResult PrimaryResult
        {
            get
            {
                return new MixtureModelResult()
                {
                    GenotypeCategory = MixtureModelResult.IntToGenotypeCategory(Clustering[0]),
                    QScore = QScores[0],
                    GenotypePosteriors = PhredPosteriors[0]
                };
            }
        }
        #endregion

        #region Constructors
        private MixtureModel(IList<int> ad, IList<int> dp) : this(ad, dp, _defaultMeans) { }

        private MixtureModel(IList<int> k, IList<int> n, double[] startingMeans) :
            this(k, n, startingMeans, new double[startingMeans.Length])
        {
            int minIndex = Array.IndexOf(Means, Means.Min());
            MixtureWeights[minIndex] = 0.99;
            for (int i = 0; i < MixtureWeights.Length; i++)
            {
                if (i == minIndex)
                    continue;
                MixtureWeights[i] = 0.01 / (MixtureWeights.Length - 1);
            }
        }

        private MixtureModel(IList<int> k, IList<int> n, double[] means, double[] priors)
        {
            Means = means;
            Array.Sort(Means);
            AlleleDepths = k;
            TotalDepths = n;
            QScores = new int[AlleleDepths.Count];
            ClusterCounts = new int[3];
            Clustering = new SparseArray<int>(TotalDepths.Count);
            MixtureWeights = priors;
            Posteriors = new double[AlleleDepths.Count, Means.Length];
            LogLikelihoods.Add(UpdateExpectation());
        }
        #endregion

        #region Main methods
        private void FitBinomialModel()
        {
            // Algorithm based on this page modified to be a binomial mixture model
            // http://tinyheero.github.io/2016/01/03/gmm-em.html

            LogLikelihoods.Add(UpdateExpectation());
            UpdateParameters();

            double oldLogLikelihood = LogLikelihoods[0] + 100;
            int counter = 0;
            while (Math.Abs(LogLikelihoods[counter] - oldLogLikelihood) > 0.000001 && counter < 1000)
            {
                oldLogLikelihood = LogLikelihoods[counter];
                LogLikelihoods.Add(UpdateExpectation());
                UpdateParameters();
                counter++;
            }

            UpdateClusteringAndQScore();

            for (int k = 0; k < ClusterCounts.Length; k++)
                if (ClusterCounts[k] == 0)
                    throw new MixtureModelException(
                        "Germline adative genotyper failed because there are not enough variants to " +
                        "fit the model.  Please check that the sample is diploid.  Consider enlarging the calling" +
                        "region or using a pre-fit model.");
        }

        // Expectation step
        private double UpdateExpectation()
        {
            double[] posteriorsSum = new double[AlleleDepths.Count];
            for (int i = 0; i < AlleleDepths.Count; i++)
            {

                double[] tempPosts = new double[Means.Length];
                for (int k = 0; k < Means.Length; k++)
                {
                    tempPosts[k] = Binomial.PMF(Means[k], TotalDepths[i], AlleleDepths[i]) * MixtureWeights[k];
                    posteriorsSum[i] += tempPosts[k];

                    // Use normal approximation if sum of posteriors are zero
                    if (k == (Means.Length - 1) && posteriorsSum[i] == 0)
                    {
                        for (int kk = 0; kk < Means.Length; kk++)
                        {
                            tempPosts[kk] = Normal.PDF(Means[kk], Math.Sqrt(TotalDepths[i] * Means[kk] * (1 - Means[kk])),
                                (double)AlleleDepths[i] / TotalDepths[i]);
                            posteriorsSum[i] += tempPosts[k];
                        }
                    }
                }

                for (int k = 0; k < Means.Length; k++)
                    Posteriors[i, k] = tempPosts[k] / posteriorsSum[i];
            }
            return GetLogLikelihood(posteriorsSum);
        }

        // Maximization step
        private void UpdateParameters()
        {

            double[] n = new double[Means.Length];
            double[] nUnscaled = new double[Means.Length];
            for (int i = 0; i < TotalDepths.Count; i++)
                for (int k = 0; k < Means.Length; k++)
                {
                    n[k] += Posteriors[i, k] * TotalDepths[i];
                    nUnscaled[k] += Posteriors[i, k];
                }

            // Update means
            Array.Clear(Means, 0, Means.Length);
            for (int i = 0; i < AlleleDepths.Count; i++)
                for (int k = 0; k < Means.Length; k++)
                    Means[k] += Posteriors[i, k] * AlleleDepths[i];
            for (int k = 0; k < Means.Length; k++)
                Means[k] = Means[k] / n[k];

            // Update mixture weights
            for (int k = 0; k < Means.Length; k++)
                MixtureWeights[k] = nUnscaled[k] / AlleleDepths.Count;
        }

        private void UpdateClusteringAndQScore()
        {
            // Update clustering
            Array.Clear(ClusterCounts, 0, ClusterCounts.Length);

            // Sort the means and mixture weights
            Array.Sort(Means, MixtureWeights);

            // Keep track of variant frequencies by cluster
            var vfByCluster = new List<List<double>>()
            {
                new List<double>(),
                new List<double>(),
                new List<double>()
            };

            for (int i = 0; i < Clustering.Count; i++)
            {
                int maxIndex = 0;
                double maxPost = Posteriors[i, 0];
                for (int k = 1; k < Means.Length; k++)
                {
                    if (Posteriors[i, k] > maxPost)
                    {
                        maxIndex = k;
                        maxPost = Posteriors[i, k];
                    }
                }
                Clustering[i] = maxIndex;
                ClusterCounts[maxIndex]++;
                vfByCluster[Clustering[i]].Add((double)AlleleDepths[i] / TotalDepths[i]);
            }

            // Update Q scores
            QScoreEffectiveN = new int[Means.Length];
            for (int k = 0; k < QScoreEffectiveN.Length; k++)
            {
                double variance = vfByCluster[k].Variance();
                if (double.IsNaN(variance) || double.IsInfinity(variance))
                {
                    QScoreEffectiveN = _defaultQScoreEffectiveN;
                    break;
                }
                else
                {
                    QScoreEffectiveN[k] = (int)Math.Round(Means[k] * (1 - Means[k])
                        / variance);
                }
            }

            for (int i = 0; i < Clustering.Count; i++)
            {
                (int qScore, float[] phredScores) = CalculateQScoreAndGenotypePosteriors(AlleleDepths[i], TotalDepths[i],
                    Clustering[i], Means, MixtureWeights, QScoreEffectiveN);

                QScores[i] = qScore;
                PhredPosteriors.Add(phredScores);
            }
        }

        #endregion

        #region Helper methods

        private static (int qScore, float[] phredPosteriors) CalculateQScoreAndGenotypePosteriors(int alleleDepth, 
            int totalDepth, int category, double[] means, double[] priors, int[] maxN)
        {
            float[] phredScores = new float[means.Length];
            double[] posteriors = CalculatePosteriorsWithMaxN(alleleDepth, totalDepth, means, priors, maxN);

            for (int i = 0; i < means.Length; i++)
            {
                phredScores[i] = Math.Min(_maxQScore, MathOperations.PToQ_CapAt300(posteriors[i]));
            }

            var qScore = Math.Min((int)_maxQScore, (int)Math.Round(MathOperations.PToQ_CapAt300(1 - posteriors[category])));

            return (qScore, phredScores);
        }

        private static double[] CalculatePosteriorsWithMaxN(int k, int n, double[] means, double[] priors, int[] maxN)
        {
            // If N exceeds maxN, rescale data
            var kArray = new int[maxN.Length];
            var nArray = new int[maxN.Length];
            for (int i = 0; i < maxN.Length; i++)
            {
                if (n > maxN[i])
                {
                    var vf = (double)k / n;
                    kArray[i] = (int)Math.Round(vf * maxN[i]);
                    nArray[i] = maxN[i];
                }
                else
                {
                    kArray[i] = k;
                    nArray[i] = n;
                }
            }
            return CalculatePosteriors(kArray, nArray, means, priors);
        }

        private static double[] CalculatePosteriors(int[] kArray, int[] nArray, double[] means, double[] priors)
        {
            double[] posteriors = new double[means.Length];
            double[] tempPosts = new double[means.Length];
            double posteriorsSum = 0;

            for (int i = 0; i < means.Length; i++)
            {
                tempPosts[i] = Binomial.PMF(means[i], nArray[i], kArray[i]) * priors[i];
                posteriorsSum += tempPosts[i];

                // Use normal approximation if sum of posteriors is zero
                if (i == (means.Length - 1) && posteriorsSum == 0)
                {
                    for (int ii = 0; ii < means.Length; ii++)
                    {
                        tempPosts[ii] = Normal.PDF(means[ii], Math.Sqrt(nArray[i] * means[ii] * (1 - means[ii])),
                            (double)kArray[i] / nArray[i]);
                        posteriorsSum += tempPosts[ii];
                    }
                }
            }

            for (int i = 0; i < means.Length; i++)
                posteriors[i] = tempPosts[i] / posteriorsSum;

            return posteriors;
        }

        private static double[] CalculatePosteriors(int k, int n, double[] means, double[] priors)
        {
            var kArray = new int[means.Length];
            var nArray = new int[means.Length];
            for (int i = 0; i < means.Length; i++)
            {
                kArray[i] = k;
                nArray[i] = n;
            }

            return CalculatePosteriors(kArray, nArray, means, priors);
        }

        private static double GetLogLikelihood(double[] arr)
        {
            double sum = 0;
            for (int i = 0; i < arr.Length; i++)
                sum += Math.Log(arr[i]);
            return sum;
        }

        private static double Distance(double point1, double point2)
        {
            return Math.Pow(point1 - point2, 2);
        }

        #endregion Helper methods

        #region Public static methods

        public static SimplifiedDiploidGenotype GetSimplifiedGenotype(int alleleDepth, int totalDepth, double[] means,
            double[] priors)
        {
            // Get genotype category
            double[] posteriors = CalculatePosteriors(alleleDepth, totalDepth, means, priors);
            var maxPosterior = posteriors.Max();
            int category = Array.IndexOf(posteriors, maxPosterior);

            return MixtureModelResult.IntToGenotypeCategory(category);
        }

        public static MixtureModelResult CalculateQScoreAndGenotypePosteriors(int alleleDepth, int totalDepth, double[] means,
            double[] priors)
        {
            // Get genotype category
            int category = MixtureModelResult.GenotypeCategoryToInt(GetSimplifiedGenotype(alleleDepth, 
                totalDepth, means, priors));

            // Get Q score and genotype posterior
            (int qScore, float[] genotypePosteriors) = CalculateQScoreAndGenotypePosteriors(alleleDepth, totalDepth, category,
                means, priors,  _defaultQScoreEffectiveN);

            return new MixtureModelResult
            {
                GenotypeCategoryAsInt = category,
                QScore = qScore,
                GenotypePosteriors = genotypePosteriors
            };
        }

        public static MixtureModel UsePrefitModel(IList<int> ad, IList<int> dp, double[] means, double[] priors)
        {
            var mm = new MixtureModel(ad, dp, means, priors);
            mm.UpdateClusteringAndQScore();
            return mm;
        }

        public static MixtureModel FitMixtureModel(IList<int> ad, IList<int> dp)
        {
            return FitMixtureModel(ad, dp, _defaultMeans);
        }

        public static MixtureModel FitMixtureModel(IList<int> ad, IList<int> dp, double[] startingMeans)
        {
            var mm = new MixtureModel(ad, dp, startingMeans);
            mm.FitBinomialModel();
            return mm;
        }

        /// <summary>
        /// Calculates the Q scores and phred-scaled genotype posteriors for a 1/2 locus.
        /// This method uses a multinomial distribution to calculate the Q score and posteriors for a 1/2 locus.  The probabilities
        /// of each class is estimated from the input models.
        /// </summary>
        /// <param name="ad">
        /// A three member int array that specifies the allele depths of the reference, AD1 and AD2, in that order. 
        /// </param>
        /// <param name="dp">
        /// Total read depth at the locus.
        /// </param>
        /// <param name="means">
        /// An IList containing two members, each a double array specifying the means of the models of AD1 and AD2, in that order.
        /// Each array is three elements long.
        /// </param>
        /// <returns>
        /// A RecalibratedVariant object containing the genotype posteriors and Q scores.
        /// </returns>
        /// <remarks>
        /// The prior probability is specified as: homozygous reference-99%, uniform prior for the other possible genotypes.
        /// If total read depth is greater than 500, Q score and posteriors cannot be estimated so the maximal values are returned.
        /// </remarks>
        public static MixtureModelResult GetMultinomialQScores(int[] ad, int dp, IList<double[]> means)
        {
            if (dp > 500) // Can't be calculated
                return new MixtureModelResult()
                {
                    GenotypeCategory = SimplifiedDiploidGenotype.HeterozygousAltRef,
                    QScore = (int)_maxQScore,
                    GenotypePosteriors = new float[] { _maxQScore, _maxQScore, _maxQScore, _maxQScore, 0, _maxQScore }
                };

            double[] tempPosts = new double[6];
            int postCount = 0;
            double postNorm = 0;

            for (int m2 = 0; m2 < means[1].Length; m2++)
            {
                for (int m1 = 0; m1 < means[0].Length; m1++)
                {
                    // Exclude when either allele is hom-alt and the other alelle is not hom-ref
                    if ((m1 == 2 && m2 != 0) ||
                        (m2 == 2 && m1 != 0))
                        continue;

                    var p = CalculateProbabilities(m1, m2, means);

                    Multinomial mm = new Multinomial(p, dp);
                    double prior = 0.01 / 5;
                    if (m1 == 0 && m2 == 0)
                        prior = 0.99;

                    tempPosts[postCount] = mm.Probability(ad) * prior;
                    postNorm = postNorm + tempPosts[postCount];
                    postCount++;
                }
            }

            float[] gp = new float[tempPosts.Length];
            for (int i = 0; i < tempPosts.Length; i++)
                gp[i] = Math.Min(_maxQScore, MathOperations.PToQ_CapAt300(tempPosts[i] / postNorm));

            int qScore = Math.Min((int)_maxQScore, (int)Math.Round(MathOperations.PToQ_CapAt300(1 - tempPosts[4] / postNorm)));

            return new MixtureModelResult()
            {
                GenotypeCategory = SimplifiedDiploidGenotype.HeterozygousAltRef,
                QScore = qScore,
                GenotypePosteriors = gp
            };

            // Helper method
            double[] CalculateProbabilities(int genotype1, int genotype2, IList<double[]> probs)
            {
                double[] result = new double[probs[0].Length];
                result[1] = probs[0][genotype1];
                result[2] = probs[1][genotype2];
                result[0] = 1 - result[1] - result[2];

                // Deal with when p is less than or equal to 0
                if (result[0] <= 0)
                {
                    if (genotype1 == 2)
                        result[0] = 1 - result[1];
                    else if (genotype2 == 2)
                        result[0] = 1 - result[2];
                    else if (genotype1 == 1 && genotype2 == 1)
                        result[0] = 1 - probs[0][2];
                }
                return result;
            }
        }

        /// <summary>
        /// first line is means, next is priors. it can be for any number of models
        /// </summary>
        /// <param name="modelsFile"></param>
        /// <returns></returns>
        public static List<MixtureModelParameters> ReadModelsFile(string modelsFile)
        {
            Logger.WriteToLog("Reading models file " + modelsFile);

            var mixtureModelInputList = new List<MixtureModelParameters> { };
            var models = new List<double[]>();

            using (StreamReader sr = new StreamReader(modelsFile))
            {
                while (true)
                {
                    string nextLine = sr.ReadLine();
                    if (nextLine == null)
                        break;
                    string[] line = nextLine.Split(',');
                    double[] arr = line.Select(s => double.Parse(s)).ToArray();
                    models.Add(arr);
                }
            }

            if (models.Count != 4 && models.Count != 2)
                throw new InvalidOperationException("Invalid model file.  Fix models file, or run without it.");

            mixtureModelInputList.Add(new MixtureModelParameters() { Means = models[0], Priors = models[1] });

            if (models.Count == 4)
                mixtureModelInputList.Add(new MixtureModelParameters() { Means = models[2], Priors = models[3] });

            return mixtureModelInputList;
        }

        public static void WriteModelFile(string outDir, string modelFile, List<MixtureModel> models)
        {
            Logger.WriteToLog("Writing model to file");
            modelFile = Path.Combine(outDir, modelFile);
            if (File.Exists(modelFile))
                File.Delete(modelFile);
            using (StreamWriter sw = new StreamWriter(new FileStream(modelFile, FileMode.CreateNew)))
            {
                foreach (MixtureModel model in models)
                {
                    sw.Write(string.Join(',', model.Means) + "\n" +
                             string.Join(',', model.MixtureWeights) + "\n");
                }
            }
        }

        #endregion Static methods
    }

    public class MixtureModelException : Exception
    {
        public MixtureModelException(string msg) : base(msg) { }
    }
}
