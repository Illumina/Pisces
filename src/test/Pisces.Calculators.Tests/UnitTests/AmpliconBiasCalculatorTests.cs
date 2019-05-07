using System;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Calculators.Tests
{
    public class AmpliconBiasCalculatorTests
    {
        static string[] twoAmpliconNames = new string[] { "amp1", "amp2" };


        [Fact]
        public void HappyPath_VaryingDepthWithBias()
        {
            const double amp1Freq = 0.05;
            const double amp2Freq = 0.0;

            const double amp1Depth = 1000;
            double amp2Depth = 0;
        
            for (var i = 0; i < 1000; i++)
            {
                AmpliconCounts supportForAmplicons = new AmpliconCounts()
                        { AmpliconNames=twoAmpliconNames, CountsForAmplicon = new int[]{ (int) (amp1Freq*amp1Depth) , (int)(amp2Freq * amp2Depth) } };

                AmpliconCounts coverageForAmplicons = new AmpliconCounts() 
                        { AmpliconNames = twoAmpliconNames, CountsForAmplicon = new int[] { (int)(amp1Depth), (int)(amp2Depth) } };

                var results = ExecuteTest(supportForAmplicons, coverageForAmplicons);

                if (amp2Depth < 100)
                    Assert.False(results.BiasDetected); // not enough coverage on one amplicon, to expect to see the varinat on both.
                else
                {
                    Assert.True(results.BiasDetected); //  we have coverage on both amplicons, but its support only shows up on one
                }

                amp2Depth++;
            }
        }

        [Fact]
        public void HappyPath_VaryingDepthWithNoBias()
        {
            double amp1Freq = 0.09;
            const double amp2Freq = 0.09;
            const int amp2Depth = 1000;


            for (var amp1Depth = 10; amp1Depth < 2000;)
            {
                amp1Depth = amp1Depth + 100;


                AmpliconCounts supportForAmplicons = new AmpliconCounts()
                { AmpliconNames = twoAmpliconNames, CountsForAmplicon = new int[] { (int)(amp1Freq * amp1Depth), (int)(amp2Freq * amp2Depth) } };

                AmpliconCounts coverageForAmplicons = new AmpliconCounts()
                { AmpliconNames = twoAmpliconNames, CountsForAmplicon = new int[] { (int)(amp1Depth), (int)(amp2Depth) } };

                var results = ExecuteTest(supportForAmplicons, coverageForAmplicons);

                bool freqAreSimliar = Math.Abs(results.ResultsByAmpliconName["amp1"].Frequency - results.ResultsByAmpliconName["amp2"].Frequency) < 0.05;

                if (freqAreSimliar)
                    Assert.False(results.BiasDetected);
                else
                    Assert.True(results.BiasDetected);
            }

        }

        
        /// <summary>
        /// Verify Amplicon Bias can be calculated on forcedGT variants that may not (shock!) be present on any amplicon at the 1% cutoff.
        /// </summary>
        [Fact]
        public void TestAmpliconBiasCalculationsForForcedVariants()
        {
            ExecuteTwoAmpTest(0.0001f, 500000, 0.0001f, 500000, false);
        }


        [Fact]
        public void TestAmpBiasWhenAmpNamesDontMatchUp()
        {
            //case where one amplicon has no support (or even a valid entry)

            AmpliconCounts supportForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { "B"},
                CountsForAmplicon = new int[] { 150 }
            };

            AmpliconCounts coverageForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { "A", "B" },
                CountsForAmplicon = new int[] { 100, 300 }
            };

            var results = ExecuteTest(supportForAmplicons, coverageForAmplicons);
            Assert.Equal(true, results.BiasDetected);


            //case where the support array is totally empty. (hard to image this happening, but we'll be defensive)

           supportForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { },
                CountsForAmplicon = new int[] { }
            };

            coverageForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { "A", "B" },
                CountsForAmplicon = new int[] { 100, 150 }
            };

            //will automatically check is null
            results = ExecuteTest(supportForAmplicons, coverageForAmplicons, true);
           

            //case where the support array has totally diffent amplicons than the coverage array
            //(hard to image this happening, but we'll be defensive. Indels and ForcedReport can be odd)

            supportForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { "C", "D" },
                CountsForAmplicon = new int[] { 100, 150 }
            };

            coverageForAmplicons = new AmpliconCounts()
            {
                AmpliconNames = new string[] { "A", "B" },
                CountsForAmplicon = new int[] { 100, 150 }
            };

            results = ExecuteTest(supportForAmplicons, coverageForAmplicons);
            Assert.Equal(false, results.BiasDetected);

        }


        public void TestPresentOnBothStrands()
        {
            // ------------------------------
            // Expected: var and coverage present on both strands
            // ------------------------------

            // Detected in all directions
            ExecuteTwoAmpTest(0.1f, 500, 0.1f, 500, false);
            // Detected in one direction 
            ExecuteTwoAmpTest(0.1f, 500, 0f, 0, false);
 
            //No coverage anywhere
            ExecuteTwoAmpTest(0f, 0, 0f, 0, false);

            //No support anywhere
            ExecuteTwoAmpTest(0f, 100, 0f, 100, false);

            // range of scenarios
            ExecuteTwoAmpTest(0f, 0, 0.2f, 500, false);
            ExecuteTwoAmpTest(0f, 5000, 0.2f, 500, true);
            ExecuteTwoAmpTest(0.001f, 5000, 0.9f, 500, true);
            ExecuteTwoAmpTest(0.1f, 500, 0f, 500,true);
            ExecuteTwoAmpTest(0.1f, 500, 0f, 0, false);
    
        }

        private void ExecuteTwoAmpTest(float ampAFreq, int ampADepth, float ampBFreq, int ampBDepth, bool isBiased)
        {
            AmpliconCounts supportForAmplicons = new AmpliconCounts()
            { AmpliconNames = twoAmpliconNames, CountsForAmplicon = new int[] { (int)(ampAFreq * ampADepth), (int)(ampBFreq * ampBDepth) } };

            AmpliconCounts coverageForAmplicons = new AmpliconCounts()
            { AmpliconNames = twoAmpliconNames, CountsForAmplicon = new int[] { (int)(ampADepth), (int)(ampBDepth) } };


            var results = ExecuteTest(supportForAmplicons, coverageForAmplicons);
            Assert.Equal(isBiased, results.BiasDetected);

        }

        private AmpliconCounts ReverseAmpliconData(AmpliconCounts inputCounts)
        {
            var newNames = new string[inputCounts.AmpliconNames.Length];
            var newCounts = new int[inputCounts.CountsForAmplicon.Length];

            Array.Copy(inputCounts.AmpliconNames, newNames, inputCounts.AmpliconNames.Length);
            Array.Copy(inputCounts.CountsForAmplicon, newCounts, inputCounts.AmpliconNames.Length);

            Array.Reverse(newNames);
            Array.Reverse(newCounts);

            AmpliconCounts outputCounts = new AmpliconCounts() { AmpliconNames = newNames, CountsForAmplicon = newCounts };

            return outputCounts;
        }
            private BiasResultsAcrossAmplicons ExecuteTest(AmpliconCounts supportForAmplicons, 
                AmpliconCounts coverageForAmplicons, bool expectNull = false)
        {
            
             var variant1 = new CalledAllele(AlleleCategory.Snv)
            {
                SupportByAmplicon = supportForAmplicons,
                CoverageByAmplicon = coverageForAmplicons
            };

            AmpliconBiasCalculator.Compute(variant1, 100, 0.01F);

            var variant2 = new CalledAllele(AlleleCategory.Snv)
            {
                SupportByAmplicon = ReverseAmpliconData(supportForAmplicons),
                CoverageByAmplicon = ReverseAmpliconData(coverageForAmplicons)
            };

            AmpliconBiasCalculator.Compute(variant2, 100, 0.01F);

            //sanity check, reversing the input must always give the same result.

            if (expectNull)
            {
                Assert.Null(variant1.AmpliconBiasResults);
                Assert.Null(variant2.AmpliconBiasResults);
            }
            else
                Assert.Equal(variant1.AmpliconBiasResults.BiasDetected, variant2.AmpliconBiasResults.BiasDetected);
        
            return variant1.AmpliconBiasResults;
        }

       
    }
}
