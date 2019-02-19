using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Calculators;
using Pisces.Domain.Types;
using Common.IO.Utility;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace Pisces.Genotyping
{
    public class MixtureModelInput
    {
        public double[] Means;
        public double[] Weights;
    }
        public class MixtureModelResult
    {
        public SimplifiedDiploidGenotype GenotypeCategory; //{0/0,0/1,1/1}
        public int QScore;
        public float[] GenotypePosteriors;

        public int GentoptypeCategoryAsInt
        {
            get
            {
                switch (GenotypeCategory)
                {
                    case SimplifiedDiploidGenotype.HomozygousRef:
                        return 0;
                    case SimplifiedDiploidGenotype.HeterozygousAltRef:
                        return 1;
                    case SimplifiedDiploidGenotype.HomozygousAlt:
                        return 2;
                    default:
                        throw (new ArgumentException("The given genotype is not allowed." + GenotypeCategory));
                }
            }
        }

        public static SimplifiedDiploidGenotype IntToGentoptypeCategory(int i)
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
                    throw (new ArgumentException("The given genotype number  is not allowed." + i));
            }
        }

    }
    

    public class MixtureModel
    {
        private static readonly float _maxQScore = 100F;
        private static readonly double[] _defaultMeans = new double[] { 0.01, 0.45, 0.95 };

        private readonly IList<int> AlleleDepths;
        private readonly IList<int> TotalDepths;
        private readonly double[,] Posteriors;
        private readonly int[] ClusterCounts;

        #region Properties
        public double[] Means { get; }
        public IList<int> Clustering { get; }
        public double[] MixtureWeights { get; private set; }
        public int[] QScores { get; }
        public List<double> LogLikelihoods { get; } = new List<double>();
        public List<float[]> PhredPosteriors { get; } = new List<float[]>();
        #endregion

        public MixtureModelResult PrimaryResult
        {
            get
            {
                return new MixtureModelResult() {GenotypeCategory = MixtureModelResult.IntToGentoptypeCategory( Clustering[0]), QScore = QScores[0], GenotypePosteriors = PhredPosteriors[0] };

            }
        }

        #region Constructors
        public MixtureModel() { }
        public MixtureModel(IList<int> ad, IList<int> dp) : this(ad, dp, _defaultMeans) { }

        public MixtureModel(IList<int> k, IList<int> n, double[] startingMeans) : 
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

        // If the model has already been fit, use this constructor to specify means and priors and then call 
        // UpdateClusteringAndQScore()
        public MixtureModel(IList<int> k, IList<int> n, double[] means, double[] priors)
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
        public void FitBinomialModel()
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
                    throw new Exception(
                        "Germline adative genotyper failed because there are not enough variants to " +
                        "fit the model.  Please check that the sample is diploid.  Consider enlarging the calling" +
                        "region or using a pre-fit model.");
        }
        public void UpdateClusteringAndQScore()
        {
            Array.Clear(ClusterCounts, 0, ClusterCounts.Length);

            // Get the 3 indices closest to 0, 0.5 and 1
            int[] clusterCategories = GetClusterCategories(Means);

            for (int i = 0; i < Clustering.Count; i++)
            {
                int maxIndex = clusterCategories[0];

                // Include possible somatic mutation when calculating posteriors
                // 2.7E-7 from paper Differences between germline and somatic mutation rates in humans and mice
                double[] qScorePosterior = CalculatePosteriors(AlleleDepths[i], TotalDepths[i],
                    Means.Concat(new double[] { (double)AlleleDepths[i] / TotalDepths[i] }).ToArray(),
                    MixtureWeights.Concat(new double[] { 2.7E-7 / ((double)AlleleDepths[i] / TotalDepths[i] / 0.1)}).ToArray());

                double maxPost = Posteriors[i, 0];
                float[] phredScores = new float[clusterCategories.Length];
                phredScores[0] = Math.Min(_maxQScore, MathOperations.PToQ_CapAt300(qScorePosterior[0]));
                for (int k = 1; k < Means.Length; k++)
                {
                    if (Posteriors[i, k] > maxPost && clusterCategories.Contains(k))
                    {
                        maxIndex = k;
                        maxPost = Posteriors[i, k];
                    }
                    if (clusterCategories.Contains(k))
                        phredScores[Array.IndexOf(clusterCategories, k)] = Math.Min(_maxQScore, MathOperations.PToQ_CapAt300(qScorePosterior[k]));
                }
                Clustering[i] = Array.IndexOf(clusterCategories, maxIndex);
                ClusterCounts[Array.IndexOf(clusterCategories, maxIndex)]++;
                QScores[i] = Math.Min((int)_maxQScore, (int)Math.Round(MathOperations.PToQ_CapAt300(1 - qScorePosterior[maxIndex])));
                PhredPosteriors.Add(phredScores);
            }
        }

        public void UpdateWithNewMixtureWeights(double[] newMixes)
        {
            MixtureWeights = newMixes;
            LogLikelihoods.Add(UpdateExpectation());
            UpdateClusteringAndQScore();
        }
        #endregion

        #region Helper methods
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
        /*private double[] DownsamplePosterior(int i)
        {
            double[] downsampledPosterior = new double[Means.Length];
            int newSupport = (int)Math.Round((double)AlleleDepths[i] / TotalDepths[i] * MaxDepth);
            double[] tempPosts = new double[Means.Length];
            double posteriorsSum = 0;
            for (int j = 0; j < Means.Length; j++)
            {
                tempPosts[j] = PBinom.dbinom(newSupport, MaxDepth, Means[j]) * MixtureWeights[j];
                posteriorsSum += tempPosts[j];
            }

            for (int j = 0; j < Means.Length; j++)
                downsampledPosterior[j] = tempPosts[j] / posteriorsSum;

            return downsampledPosterior;
        }*/
        #endregion Helper methods

        #region Static methods
        /// <summary>
        /// When using a model with more than 3 discrete models, this method finds the three closest points to 0, 0.5 and 1.
        /// </summary>
        /// <param name="models">
        /// An IList of models that need to be reduced to 3 elements each.  Each model is specified via a double array.
        /// </param>
        /// <returns>
        /// A list of models, each model is a three member double array.
        /// </returns>
        public static List<double[]> GetActualMeans(IList<double[]> models)
        {
            // if model means exceed three elements, ignore other ones
            List<double[]> tempMeans = new List<double[]>();
            for (int i = 0; i < models.Count; i++)
            {
                tempMeans.Add(new double[models[i].Length]);
                Array.Copy(models[i], tempMeans[i], models[i].Length);
                if (models[i].Length > 3)
                {
                    int[] idx = MixtureModel.GetClusterCategories(models[i]);
                    double[] newMeans = new double[]
                    {
                            models[i][idx[0]],
                            models[i][idx[1]],
                            models[i][idx[2]]
                    };
                    tempMeans[i] = newMeans;
                }
            }
            return tempMeans;
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
        //public static RecalibratedVariant GetMultinomialQScores(int[] ad, int dp, IList<double[]> means)
        public static MixtureModelResult GetMultinomialQScores(int[] ad, int dp, IList<double[]> means)
        {
            if (dp > 500) // Can't be calculated
                 return new MixtureModelResult() { GenotypeCategory = SimplifiedDiploidGenotype.HeterozygousAltRef, QScore = (int)_maxQScore,
                     GenotypePosteriors = new float[] {_maxQScore, _maxQScore, _maxQScore, _maxQScore, 0, _maxQScore} };   

            means = GetActualMeans(means);

            double[] p = new double[means[0].Length];
            double[] tempPosts = new double[6];
            int postCount = 0;
            double postNorm = 0;

            for (int m2 = 0; m2 < means[1].Length; m2++)
            {
                for (int m1 = 0; m1 < means[0].Length; m1++)
                {
                    if (m1 == 2 && m2 != 0 || m2 == 2 && m1 != 0)
                        continue;
                    p[1] = means[0][m1];
                    p[2] = means[1][m2];
                    p[0] = 1 - p[1] - p[2];

                    if (p[0] < 0)
                    {
                        if (m1 == 2)
                            p[0] = (1 - p[1]);
                        else if (m2 == 2)
                            p[0] = (1 - p[2]);
                        else if (m1 == 1 && m2 == 1)
                            p[0] = 1 - means[0][2];
                    }

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

            return new MixtureModelResult() { GenotypeCategory = SimplifiedDiploidGenotype.HeterozygousAltRef, QScore = qScore, GenotypePosteriors = gp };
        }

      
        private static double[] CalculatePosteriors(int k, int n, double[] means, double[] priors)
        {
            double[] posteriors = new double[means.Length];
            double[] tempPosts = new double[means.Length];
            double posteriorsSum = 0;

            for (int i = 0; i < means.Length; i++)
                {
                    tempPosts[i] = Binomial.PMF(means[i], n, k) * priors[i];
                    posteriorsSum += tempPosts[i];

                    // Use normal approximation if sum of posteriors are zero
                    if (i == (means.Length - 1) && posteriorsSum == 0)
                    {
                        for (int ii = 0; ii < means.Length; ii++)
                        {
                            tempPosts[ii] = Normal.PDF(means[ii], Math.Sqrt(n * means[ii] * (1 - means[ii])),
                                (double)k / n);
                            posteriorsSum += tempPosts[ii];
                        }
                    }
                }

            for (int i = 0; i < means.Length; i++)
                posteriors[i] = tempPosts[i] / posteriorsSum;

            return posteriors;
        }
        private static int[] GetClusterCategories(double[] means)
        {
            double target = 0;
            int[] result = new int[3];
            for (int k = 0; k < result.Length; k++)
            {
                if (k == 1)
                    target = 0.5;
                else if (k == 2)
                    target = 1;

                int currIdx = 0;
                double currDist = Distance(target, means[0]);
                for (int i = 1; i < means.Length; i++)
                {
                    if (Distance(target, means[i]) < currDist)
                    {
                        currIdx = i;
                        currDist = Distance(target, means[i]);
                    }
                }
                result[k] = currIdx;
            }

            return result;
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

        /// <summary>
        /// first line is means, next is priors. it can be for any number of models
        /// </summary>
        /// <param name="modelsFile"></param>
        /// <returns></returns>
        public static new List<MixtureModelInput> ReadModelsFile(string modelsFile)
        {
            Logger.WriteToLog("Reading models file " + modelsFile);

           var mixtureModelInputList = new List<MixtureModelInput> { };
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

            mixtureModelInputList.Add(new MixtureModelInput()  { Means = models[0], Weights = models[1] });

            if (models.Count==4)
                mixtureModelInputList.Add(new MixtureModelInput() { Means = models[2], Weights = models[3] });

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
}
