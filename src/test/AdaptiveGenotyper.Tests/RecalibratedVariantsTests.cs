using Pisces.IO.Sequencing;
using System.Collections.Generic;
using System.IO;
using Pisces.Genotyping;
using Pisces.Domain.Types;
using Xunit;

namespace AdaptiveGenotyper.Tests
{
    public class RecalibratedVariantsTests
    {
        RecalibratedVariantsCollection RecalCollection;

        public RecalibratedVariantsTests()
        {
            RecalCollection = new RecalibratedVariantsCollection();
            var vcfPath = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantDepthReaderTest.vcf");
            using (var reader = new VcfReader(vcfPath))
            {
                var variant = new VcfVariant();
                var lastVariant = new VcfVariant();
                while (reader.GetNextVariant(variant))
                {
                    if (lastVariant.ReferencePosition == variant.ReferencePosition)
                        continue;

                    RecalCollection.AddLocus(variant);
                    lastVariant = variant;
                    variant = new VcfVariant();
                }
            }
        }

        [Fact]
        public void RecalibratedVariantsRemoveAndContainsTests()
        {
            Assert.Equal(14, RecalCollection.Count);
            Assert.True(RecalCollection.ContainsKey("chr1:115252188"));
            Assert.False(RecalCollection.ContainsKey("chr1:20"));
            RecalCollection.RemoveLastEntry();
            Assert.False(RecalCollection.ContainsKey("chr1:115252188"));
        }

        [Fact]
        public void RecalibratedVariantModelAndIndexerTests()
        {
            var modelsFile = Path.Combine(TestPaths.LocalTestDataDirectory, "example.model");
            List<MixtureModelInput> models = MixtureModel.ReadModelsFile(modelsFile);

            var mm = new MixtureModel(RecalCollection.Ad, RecalCollection.Dp, models[0].Means, models[0].Weights);
            mm.UpdateClusteringAndQScore();

            RecalCollection.AddMixtureModelResults(mm);

            Assert.Equal(14, RecalCollection.Categories.Count);
            Assert.Equal(14, RecalCollection.QScores.Count);

            RecalibratedVariant variant1 = RecalCollection["chr1:115252176"];
            Assert.Equal(SimplifiedDiploidGenotype.HeterozygousAltRef, variant1.MixtureModelResult.GenotypeCategory);
            RecalibratedVariant variant2 = RecalCollection["chr1:115252177"];
            Assert.Equal(SimplifiedDiploidGenotype.HomozygousAlt, variant2.MixtureModelResult.GenotypeCategory);
        }
    }
}
