using System;
using System.Collections.Generic;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class VariantPhasingResultTests
    {
        [Fact]
        public void VariantPhasingResult()
        {
            // VariantA should be the same VariantSite we passed in
        }

        [Fact]
        public void AddSupport()
        {
            //TODO : Right now AddSupport checks the dictionary and adds first. Do we want to be restricting the variant sites to those we initialize with? Or do we want to not initialize the variant sites and just track those that have support?
 
            // This method tests AddSupportForB and AddSupportForAandB

            // Should not throw any exceptions (null ref or key not found are ones to look out for since we're tracking in dictionaries)
            // SupportOfB should be incremented by 1
            // WeightedSupportOfB should be incremented by weight

            var variantA = new VariantSite(1);
            var variantB = new VariantSite(2);
            var variantB2 = new VariantSite(3);
            var variantBOutsideGroup = new VariantSite(4);

            // We can verify that these were incremented by checking for the prob of A given B, since we know how many clusters we have
            var phasingResult = new VariantPhasingResult(variantA, new List<VariantSite> {variantB, variantB2}, 1);

            // Add support for a variant not being tracked already: should not throw an exception (see TODO above)
            phasingResult.AddSupportForB(variantBOutsideGroup, 30);

            //There is no support for B on its own (without A)
            phasingResult.AddSupportForB(variantB, 3);
            
            //Haven't added support for AandB yet, so we'll get a 0.
            Assert.Equal(0, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(0, phasingResult.GetWeightedProbOfAGivenB(variantB));

            //Adding support for AandB should bring us into the positive
            phasingResult.AddSupportForAandB(variantB, 12);
            //Now we should get 1/1 raw and 12/3 weighted
            Assert.Equal(1, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(4, phasingResult.GetWeightedProbOfAGivenB(variantB));

            //Adding more support for B alone should change our results
            phasingResult.AddSupportForB(variantB, 3);
            //Now we should get 1/2 raw and 12/6 weighted
            Assert.Equal(.5, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(2, phasingResult.GetWeightedProbOfAGivenB(variantB));

            //Adding more support for AandB should change our results
            phasingResult.AddSupportForAandB(variantB, 6);
            //Now we should get 2/2 raw and 18/6 weighted
            Assert.Equal(1, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(3, phasingResult.GetWeightedProbOfAGivenB(variantB));

            //Adding support to a different variant should not change our results
            phasingResult.AddSupportForB(variantB2,5);
            Assert.Equal(1, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(3, phasingResult.GetWeightedProbOfAGivenB(variantB));
            phasingResult.AddSupportForAandB(variantB2, 5);
            Assert.Equal(1, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(3, phasingResult.GetWeightedProbOfAGivenB(variantB));

        }

        [Fact]
        public void GetProbOfAGivenB()
        {
            var variantA = new VariantSite(1);
            var variantB = new VariantSite(2);
            var variantB2 = new VariantSite(3);
            var variantBOutsideGroup = new VariantSite(4);

            var phasingResult = new VariantPhasingResult(variantA, new List<VariantSite> { variantB, variantB2 }, 100);

            // Should return 0 if nothing has been added
            Assert.Equal(0, phasingResult.GetProbOfAGivenB(variantB));
            Assert.Equal(0, phasingResult.GetProbOfAGivenB(variantB2));

            // Should return 0 if there is no support for AandB
            phasingResult.AddSupportForB(variantB, 20);
            phasingResult.AddSupportForB(variantB, 10);
            Assert.Equal(0, phasingResult.GetProbOfAGivenB(variantB));

            // Should return 0 if there is no support for B alone
            phasingResult.AddSupportForAandB(variantB2, 20);
            Assert.Equal(0, phasingResult.GetProbOfAGivenB(variantB2));

            // Should calculate probability of B as Support(B)/TotalClusters and probability of AandB as Support(AandB)/TotalClusters
            // And then divide Prob(AandB)/Prob(B)
            phasingResult.AddSupportForAandB(variantB, 10);
            Assert.True(ApproximatelyEqual(0.5, phasingResult.GetProbOfAGivenB(variantB)));

            phasingResult.AddSupportForB(variantB2, 50);
            phasingResult.AddSupportForB(variantB2, 10);
            phasingResult.AddSupportForB(variantB2, 30);
            phasingResult.AddSupportForB(variantB2, 40);
            Assert.True(ApproximatelyEqual(0.25, phasingResult.GetProbOfAGivenB(variantB2)));

            // Should throw exception for variant not tracked
            Assert.Throws<System.IO.InvalidDataException>(() => phasingResult.GetProbOfAGivenB(variantBOutsideGroup));
        }

        private bool ApproximatelyEqual(double baseline, double test)
        {
            Console.WriteLine("{0} vs {1}", baseline, test);
            return Math.Abs(baseline - test) < .00001;    
        }

        [Fact]
        public void GetWeightedProbOfAGivenB()
        {
            var variantA = new VariantSite(1);
            var variantB = new VariantSite(2);
            var variantB2 = new VariantSite(3);
            var variantBOutsideGroup = new VariantSite(4);

            var phasingResult = new VariantPhasingResult(variantA, new List<VariantSite> { variantB, variantB2 }, 100);

            // Should return 0 if nothing has been added
            Assert.Equal(0, phasingResult.GetWeightedProbOfAGivenB(variantB));
            Assert.Equal(0, phasingResult.GetWeightedProbOfAGivenB(variantB2));

            // Should return 0 if there is no support for AandB
            phasingResult.AddSupportForB(variantB, 20);
            Assert.Equal(0, phasingResult.GetWeightedProbOfAGivenB(variantB));

            // Should return 0 if there is no support for B alone
            phasingResult.AddSupportForAandB(variantB2, 20);
            Assert.Equal(0, phasingResult.GetWeightedProbOfAGivenB(variantB2));

            // Should calculate probability of B as Support(B)/TotalClusters and probability of AandB as Support(AandB)/TotalClusters
            // And then divide Prob(AandB)/Prob(B)
            phasingResult.AddSupportForAandB(variantB, 10);
            Assert.True(ApproximatelyEqual(0.5, phasingResult.GetWeightedProbOfAGivenB(variantB)));

            phasingResult.AddSupportForB(variantB2, 50);
            Assert.True(ApproximatelyEqual(0.4, phasingResult.GetWeightedProbOfAGivenB(variantB2)));

            // Should throw exception for variant not tracked
            Assert.Throws<System.IO.InvalidDataException>(() => phasingResult.GetWeightedProbOfAGivenB(variantBOutsideGroup));
        }
    }
}
