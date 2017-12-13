using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Calculators;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Calculators.Tests
{
    public class RMxNFilterCalculatorTests
    {
        double variantFrequncy = 0.20;

        [Fact]
        public void RMxN()
        {
            // ------------------------------------
            // SNV/MNV Cases
            // ------------------------------------
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3);
            ExecuteRMxNMnvTest("GG", "CCC*CGGG", 3);
            ExecuteRMxNMnvTest("CC", "CCCCG*GG", 3);

            ExecuteRMxNMnvTest("GG", "ACACA*CGGGG", 3);
            ExecuteRMxNMnvTest("AC", "ACACACG*GGG", 3);

            ExecuteRMxNMnvTest("AAA", "CAGCAGC*AGAAAAAA", 3);
            ExecuteRMxNMnvTest("CAG", "CAGCAGCAGA*AAAAA", 3);

            // ------------------------------------
            // Wiki Examples
            // ------------------------------------
            // A.1 (no example?)
            // A.2
            ExecuteRMxNIndelTest("ACACACACACAC", "N*ACACGGAC", 2);

            // A.3
            ExecuteRMxNMnvTest("T", "ACACAC*ACACAC", 0);
            ExecuteRMxNIndelTest("TCA", "ACACAC*ACACAC", 1); // 1 for each of Cs and As
            ExecuteRMxNIndelTest("TAC", "ACACAC*ACACAC", 6);

            // A.4
            ExecuteRMxNIndelTest("AC", "N*ACACAC", 3);
            ExecuteRMxNIndelTest("AC", "N*ACACGGACAC", 2);
            ExecuteRMxNIndelTest("AC", "N*ACCACCACC", 1);
            ExecuteRMxNIndelTest("AC", "N*ACACACAC", 4);
            ExecuteRMxNIndelTest("AC", "N*ACACA", 2);
            ExecuteRMxNIndelTest("AC", "N*AAA", 3);

            // B.1	 
            // TODO : SCYLLA CASE AC > G  -- ExecuteRMxNTest("G", "ACACACACACA*CGGGGG", 5, AlleleCategory.Mnv);
            ExecuteRMxNMnvTest("GG", "ACACACACACA*CGGGGG", 5);
            ExecuteRMxNMnvTest("AC", "ACACACACACACG*GGGG", 5);
            ExecuteRMxNMnvTest("ACAC", "ACACACACACACG*GGGG", 5);

            // B.2
            ExecuteRMxNIndelTest("GGACAC", "ACAC*ACACACAC", 6);
            ExecuteRMxNIndelTest("ACACG", "ACAC*ACACACAC", 6);
            ExecuteRMxNIndelTest("ACACGAC", "ACAC*ACACACAC", 6);

            // Both ends are repeats. Take the larger one.
            ExecuteRMxNIndelTest("ACACGG", "ACACACACACAC*GGGGG", 6);
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGG", 6);

            ExecuteRMxNIndelTest("ACACGG", "ACACACACACAC*GGGGGGG", 7);
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGGGG", 7);

            //  TODO : SCYLLA CASE C > ACACGGGG --  ExecuteRMxNTest("ACACGGGG", "ACACACACACAC*GGGGGGGGGGGGGGGG", 5);

            // ------------------------------------
            // Test the repeat unit length limits
            // ------------------------------------
            // Indel
            ExecuteRMxNIndelTest("ACG", "N*ACGACGACG", 3, 3);
            ExecuteRMxNIndelTest("ACG", "N*ACGACGACG", 1, 2); // Get just 1 (for the single base edges)
            ExecuteRMxNIndelTest("ACACG", "ACACACACACAC*GGGGG", 5, 1); // Doesn't find the AC-repeat (6) but does see the G-repeat (5)

            // MNV
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3, 1);
            ExecuteRMxNMnvTest("G", "CCCC*GGG", 3, 3);
            ExecuteRMxNMnvTest("GG", "ACACA*CGGGG", 1, 1); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("AC", "ACACACG*GGG", 1, 1); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("AAA", "CAGCAGC*AGAAAAAA", 1, 2); // Get just 1 (for the single base edges)
            ExecuteRMxNMnvTest("CAG", "CAGCAGCAGA*AAAAA", 1, 2); // Get just 1 (for the single base edges)

        }


        private void ExecuteRMxNMnvTest(string variantBases, string referenceSequence, int expectedRepeatLength, int? maxRepeatUnitLength = null)
        {
            ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, variantBases.Length > 1 ? AlleleCategory.Mnv : AlleleCategory.Snv, variantFrequncy, maxRepeatUnitLength ?? variantBases.Length);
        }

        private void ExecuteRMxNIndelTest(string variantBases, string referenceSequence, int expectedRepeatLength, int? maxRepeatUnitLength = null)
        {
            ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, AlleleCategory.Insertion, variantFrequncy, maxRepeatUnitLength ?? variantBases.Length);
            ExecuteRMxNTest(variantBases, referenceSequence, expectedRepeatLength, AlleleCategory.Deletion, variantFrequncy, maxRepeatUnitLength ?? variantBases.Length);
        }


        private void ExecuteRMxNTest(string variantBases, string referenceSequence, int expectedRepeatLength, AlleleCategory category, double allelefrequency, int maxRepeatUnitLength)
        {
            var alleleCoordinate = referenceSequence.IndexOf('*');
            var cleanReferenceSequence = referenceSequence.Replace("*", "");

            var refAllele = "";
            var altAllele = "";

            if (category == AlleleCategory.Insertion)
            {
                refAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, 1);
                altAllele = refAllele + variantBases;
            }
            else if (category == AlleleCategory.Deletion)
            {
                altAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, 1);
                refAllele = altAllele + variantBases;
            }
            else
            {
                refAllele = cleanReferenceSequence.Substring(alleleCoordinate - 1, variantBases.Length);
                altAllele = variantBases;
            }

            var allele = new CalledAllele(category)
            {
                ReferenceAllele = refAllele,
                AlternateAllele = altAllele,
                ReferencePosition = alleleCoordinate
            };

            allele.TotalCoverage = 1000;
            allele.AlleleSupport = (int) (1000.0 * variantFrequncy);

            RMxNFilterSettings rmxnFilterSettings = new RMxNFilterSettings();
            rmxnFilterSettings.RMxNFilterFrequencyLimit = 1.1f;
            rmxnFilterSettings.RMxNFilterMaxLengthRepeat = maxRepeatUnitLength;
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength;

            // If expected repeats == N, flag
            Assert.True(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

            // If expected repeats > N, flag
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength - 1;
            Assert.True(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

            // If expected repeats < N, flag
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength + 1;
            Assert.False(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

            // RMxN isn't valid on reference calls
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength + 1;
            Assert.False(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

            // Even if expected repeats == N, dont flag if VF is too high
            rmxnFilterSettings.RMxNFilterFrequencyLimit = 0.10f;
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength;
            Assert.False(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

            // Even if expected repeats > N, flag, dont flag if VF is too high
            rmxnFilterSettings.RMxNFilterFrequencyLimit = 0.10f;
            rmxnFilterSettings.RMxNFilterMinRepetitions = expectedRepeatLength - 1;
            Assert.False(RMxNCalculator.ShouldFilter(allele, rmxnFilterSettings, cleanReferenceSequence));

        }

    }
}