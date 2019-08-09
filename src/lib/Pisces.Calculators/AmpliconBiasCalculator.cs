using System;
using System.Collections.Generic;
using Pisces.Domain.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public static class AmpliconBiasCalculator
    {
        /// <summary>
        /// Still working out what these valeus should be and if we want to expose them as arguments
        /// </summary>
        public static class Constants
        {
            public const int MinNumObservations = 5;
            public const double FreePassObservationFreq = 0.1;
        }

        public static void Compute(CalledAllele variant, int maxQScore, float? filterThreshold)
        {
            if (filterThreshold.HasValue)
            {
                var acceptanceCriteria = (float)filterThreshold;

                //restrict use to SNPs for now. We have not done any testing on this for indels, and indel coverage calc for amplicons would need special handling.
                if (variant.Type == AlleleCategory.Snv) 
                    variant.AmpliconBiasResults = CalculateAmpliconBias(variant.SupportByAmplicon, variant.CoverageByAmplicon,
                        acceptanceCriteria, maxQScore);
            }
        }

        /// <summary>
        /// This method looks for bias in the variant support / total coverage ratios, by amplicon.
        /// This method is agnostic about where these support and coverage calculations come from, so it is up to the user
        /// to make sure the counts are appropriate for the variant in question.
        /// Note that for SNPs this is fairly straight forward, but for indels and MNVs it can become terribly difficult.
        /// This method should be used with appropriate caution.
        /// </summary>
        /// <param name="supportByAmplicon">the support counts, for each named amplicon</param>
        /// <param name="coverageByAmplicon">the coverage counts, for each named amplicon<</param>
        /// <param name="acceptanceCriteria">the minimumn probabilty we accept for the varaint being real, given the model</param>
        /// <param name="maxQScore">the max cap for a qscore. This parameter safegaurds against reporting insanely high confidence, given the limitations of a simple model that only addresses sampling error</param>
        /// <returns></returns>
        public static BiasResultsAcrossAmplicons CalculateAmpliconBias(AmpliconCounts supportByAmplicon, 
                AmpliconCounts coverageByAmplicon, float acceptanceCriteria, int maxQScore)
        {

           //if we have no amplicon information, don't worry about it.
           if ((supportByAmplicon.AmpliconNames == null) ||
                (supportByAmplicon.AmpliconNames.Length == 0) ||
                (supportByAmplicon.AmpliconNames[0]==null))
                return null;

            //If we only have coverage on one amplicon, don't worry about it. There is no "bias" to detect.
            //We might later on, add a check to require extra evidence for variants only covered by one amplicon. TBD
            if (coverageByAmplicon.AmpliconNames.Length < 2)
                return null;

            var resultDict = new BiasResultsAcrossAmplicons() { ResultsByAmpliconName = new Dictionary<string, AmpliconBiasResult>() };
            var maxFreq = 0.0;
          
            for (int i = 0; i < coverageByAmplicon.AmpliconNames.Length; i++)
            {
                var name = coverageByAmplicon.AmpliconNames[i];
                if (name == null)
                    break;

                double support = supportByAmplicon.GetCountsForAmplicon(name);
                double coverage = coverageByAmplicon.CountsForAmplicon[i];
                double freq = (coverage > 0) ? support / coverage : 0;

                if (freq >= maxFreq)
                {
                    resultDict.AmpliconWithCandidateArtifact = name;
                    maxFreq = freq;
                }

                var resultForAmplicon = new AmpliconBiasResult() { Frequency = freq, Name = name, ObservedSupport = support, Coverage = coverage };
                resultDict.ResultsByAmpliconName.Add(name, resultForAmplicon);
            }

            bool shouldFailVariant = false;
            foreach (var amplicon in resultDict.ResultsByAmpliconName.Keys)
            {
                double coverage = resultDict.ResultsByAmpliconName[amplicon].Coverage;
                double support = resultDict.ResultsByAmpliconName[amplicon].ObservedSupport;
                double freq = resultDict.ResultsByAmpliconName[amplicon].Frequency;
                int qScore = 0;
                bool biasDetected = false;
                var allowableProb = acceptanceCriteria;

                double expectedNumObservationsOfVariant = maxFreq * coverage;
                var pChanceItsReal = 1.0;

                if (expectedNumObservationsOfVariant < Constants.MinNumObservations)
                    qScore = maxQScore; //we'd never see it anyway. Seems fine.
                else if ((expectedNumObservationsOfVariant <= support) || (freq > Constants.FreePassObservationFreq))
                {
                    //we saw this variant quite a lot for this amplicon
                    qScore = maxQScore; // it certainly seems to be in this amplicon!
                }
                else //we didnt see this variant much for this amplicon. Hm.... Perhaps its not real...?
                { 
                 //What is the chance this variant exists but just happened not to show up much on this amplicon's reads?
                 //Lets look at the chance that we observed it at "support" or less, given the estimated frequency.

                    pChanceItsReal = Math.Max(0.0, Poisson.Cdf(support, expectedNumObservationsOfVariant));
                    //biasProb = 1.0 - pChanceItsReal;
                    var q = MathOperations.PtoQ(1.0 - pChanceItsReal);
                    qScore = (int)q;
                }

                //if acceptanceCriteria = Q20, thats 1/100.
                //so, if we even have 1/100 chance of this happening, lets allow it.
                // Ie, a true variant would (generally) 50% of the time show up at its expected frequency.
                // Sometimes, it would show up less. At (1/100)% of the time, it only shows up at Z frequency.
                // So, if the observation is less likely than (1/100) for a real variant, we fail it.

                if (pChanceItsReal < allowableProb)
                {
                    biasDetected = true;
                    shouldFailVariant = true;
                }
                resultDict.ResultsByAmpliconName[amplicon].ChanceItsReal = pChanceItsReal;
                resultDict.ResultsByAmpliconName[amplicon].ConfidenceQScore = qScore;
                resultDict.ResultsByAmpliconName[amplicon].BiasDetected = biasDetected;
                resultDict.ResultsByAmpliconName[amplicon].ExpectedSupport = expectedNumObservationsOfVariant;
                resultDict.BiasDetected = shouldFailVariant;
            }


            return resultDict;
        }
    }
}
