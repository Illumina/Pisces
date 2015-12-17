using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.Remoting;
using CallSomaticVariants.Logic.Calculators;
using CallSomaticVariants.Logic.VariantCalling;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.VariantCalling
{
    public class VariantProcessorTests
    {
        [Fact]
        public void HappyPath()
        {
            var variant = CreatePassingVariant(false);
            ((CalledVariant) variant).ReferenceSupport = 10;
            AlleleProcessor.Process(variant, GenotypeModel.Symmetrical, 0.01f, 100, 20, false);

            Assert.False(variant.Filters.Any());
            Assert.Equal(0.02f, variant.FractionNoCalls);
            Assert.Equal(Genotype.HeterozygousAlt, variant.Genotype);
        }

        [Fact]
        [Trait("ReqID", "SDS-49")]
        public void FractionNoCallScenarios()
        {
            ExecuteNoCallTest(900, 100, 0.1f); // standard
            ExecuteNoCallTest(0, 0, 0f); // nothing
            ExecuteNoCallTest(500, 0, 0f); // no no calls
            ExecuteNoCallTest(0, 500, 1f);  // all no calls
        }

        private void ExecuteNoCallTest(int totalCoverage, int numNoCalls, float expectedFraction)
        {
            var variant = CreatePassingVariant(false);
            variant.NumNoCalls = numNoCalls;
            variant.TotalCoverage = totalCoverage;

            AlleleProcessor.Process(variant, GenotypeModel.Symmetrical, 0.01f, 100, 20, false);
            Assert.Equal(expectedFraction, variant.FractionNoCalls);
        }

        [Fact]
        [Trait("ReqID", "SDS-48")]
        public void Filtering()
        {
            ExecuteFilteringTest(500, 30, false, 500, 30, false, new List<FilterType>());
            ExecuteFilteringTest(499, 30, false, 500, 30, false, new List<FilterType>() { FilterType.LowDepth });
            ExecuteFilteringTest(500, 24, false, 500, 25, false, new List<FilterType>() { FilterType.LowQscore });
            ExecuteFilteringTest(500, 30, true, 500, 25, false, new List<FilterType>() { FilterType.StrandBias }); // less than threshold
            ExecuteFilteringTest(500, 30, false, 500, 30, true, new List<FilterType>() { FilterType.StrandBias }); // variant on one strand only
            ExecuteFilteringTest(0, 0, true, 101, 20, false, new List<FilterType>() { FilterType.StrandBias, FilterType.LowQscore, FilterType.LowDepth });
        }

        private void ExecuteFilteringTest(int totalCoverage, int qscore, bool strandBias, int minCoverage, int minQscore, bool singleStrandVariant, 
            List<FilterType> expectedFilters)
        {
            var variant = CreatePassingVariant(false);
            variant.TotalCoverage = totalCoverage;
            variant.Qscore = qscore;
            variant.StrandBiasResults.BiasAcceptable = !strandBias;
            variant.StrandBiasResults.VarPresentOnBothStrands = !singleStrandVariant;

            AlleleProcessor.Process(variant, GenotypeModel.Symmetrical, 0.01f, minCoverage, minQscore, true);

            Assert.Equal(variant.Filters.Count, expectedFilters.Count);
            foreach(var filter in variant.Filters)
                Assert.True(expectedFilters.Contains(filter));
        }

        [Fact]
        [Trait("ReqID", "SDS-50")]
        public void GenotypeScenarios()
        {
            ExecuteGenotypeTest(99, 0.5f, false, GenotypeModel.Symmetrical, Genotype.AltLikeNoCall);
            ExecuteGenotypeTest(99, 0.5f, true, GenotypeModel.Symmetrical, Genotype.RefLikeNoCall);

            ExecuteGenotypeTest(100, 0, true, GenotypeModel.Symmetrical, Genotype.HomozygousRef); // shouldnt matter what freqs are

            ExecuteGenotypeTest(100, 0.009f, false, GenotypeModel.Symmetrical, Genotype.HomozygousAlt);
            ExecuteGenotypeTest(100, 0.24f, false, GenotypeModel.Thresholding, Genotype.HomozygousAlt);

            ExecuteGenotypeTest(100, 0.01f, false, GenotypeModel.Symmetrical, Genotype.HeterozygousAlt);
            ExecuteGenotypeTest(100, 0.25f, false, GenotypeModel.Thresholding, Genotype.HeterozygousAlt);
            ExecuteGenotypeTest(100, 0, false, GenotypeModel.None, Genotype.HeterozygousAlt);

        }

        private void ExecuteGenotypeTest(int totalCoverage, float refFrequency, bool isReference, GenotypeModel model, Genotype expectedGenotype)
        {
            var variant = CreatePassingVariant(isReference);
            variant.TotalCoverage = totalCoverage;
            if (!isReference)
            {
                var refSupport = (int) (refFrequency*totalCoverage);
                variant.AlleleSupport = totalCoverage - refSupport;
                ((CalledVariant)variant).ReferenceSupport = refSupport;                
            }
            AlleleProcessor.Process(variant, model, 0.01f, 100, 20, false);

            Assert.Equal(expectedGenotype, variant.Genotype);
        }

        private BaseCalledAllele CreatePassingVariant(bool isReference)
        {
            var calledAllele = isReference ? (BaseCalledAllele)new CalledReference() :
                new CalledVariant(AlleleCategory.Snv);

            calledAllele.Alternate = "C";
            calledAllele.Reference = "A";
            calledAllele.AlleleSupport = isReference ? 490 : 10;
            calledAllele.TotalCoverage = 490;
            calledAllele.NumNoCalls = 10;
            calledAllele.StrandBiasResults = new StrandBiasResults() {BiasAcceptable = true};
            calledAllele.Qscore = 30;

            return calledAllele;
        }
    }
}
