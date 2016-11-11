using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;


namespace Pisces.IO.Tests.UnitTests
{
    public class VcfFormatterTests
    {
        VcfFormatter _formatter;
        CalledAllele _v1;
        CalledAllele _v2;
        CalledAllele _v3;
        int _estimatedBaseCallQuality = 23;

        public void Initialize()
        {

            VcfWriterConfig config = new VcfWriterConfig
                    {
                        DepthFilterThreshold = 500,
                        VariantQualityFilterThreshold = 20,
                        StrandBiasFilterThreshold = 0.5f,
                        FrequencyFilterThreshold = 0.007f,
                        MinFrequencyThreshold = 0.007f,
                        ShouldOutputNoCallFraction = true,
                        ShouldOutputStrandBiasAndNoiseLevel = true,
                        ShouldFilterOnlyOneStrandCoverage = true,
                        EstimatedBaseCallQuality = _estimatedBaseCallQuality,
                        //AllowMultipleVcfLinesPerLoci = true
                    };

            _formatter = new VcfFormatter(config);

            _v1 = TestHelper.CreatePassingVariant(false);
            _v2 = TestHelper.CreatePassingVariant(false);
            _v3 = TestHelper.CreatePassingVariant(false);
        }

        [Fact]
        [Trait("ReqID", "SDS-VCF-9-FILTER")]
        public void FilterMerge()
        {
            Initialize();

            _v1.Filters = new List<FilterType> { FilterType.LowDepth, FilterType.LowVariantQscore};
            _v2.Filters = new List<FilterType> {FilterType.MultiAllelicSite };
            _v3.Filters = new List<FilterType> {FilterType.LowDepth };

            var mergedFilters = VcfFormatter.MergeFilters(new List<CalledAllele> { _v1, _v2, _v3 });
            var expectedFilters = new List<FilterType> { FilterType.LowDepth, FilterType.LowVariantQscore, FilterType.MultiAllelicSite };
            Assert.Equal(expectedFilters, mergedFilters);
     
        }


        [Fact]
        [Trait("ReqID", "SDS-VCF-9-REF-and-ALT")]
        public void ReferenceMerge()
        {
            Initialize();

            _v1.Reference = "CA";
            _v2.Reference = "CAA";

            _v1.Alternate = "C";
            _v2.Alternate = "C";

            string[] mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2 });

            string expectedReference = "CAA";
            string expectedAlt = "CA,C";

            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);

            _v1.Reference = "C";
            _v2.Reference = "CAA";

            _v1.Alternate = "CA";
            _v2.Alternate = "C";

            mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2 });

            expectedReference = "CAA";
            expectedAlt = "CAAA,C";

            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);

            _v1.Reference = "C";
            _v2.Reference = "C";

            _v1.Alternate = "CA";
            _v2.Alternate = "CAA";

            mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2 });

            expectedReference = "C";
            expectedAlt = "CA,CAA";

            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);

            _v1.Reference = "C";
            _v2.Reference = "C";

            _v1.Alternate = ".";
            _v2.Alternate = "T";

            mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2 });

            expectedReference = "C";
            expectedAlt = ".,T";

            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);
        }

        [Fact]
        [Trait("ReqID", "SDS-??")]
        public void MergeFromBug185()
        {
            Initialize();
            _v1.Reference = "A";
            _v2.Reference = "AC";
            _v3.Reference = "ACGTTT";

            _v1.Alternate = "C";
            _v2.Alternate = "A";
            _v3.Alternate = "A";


            string[] mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2, _v3 });

            string expectedReference = "ACGTTT";
            string expectedAlt = "CCGTTT,AGTTT,A";

            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);
        }


        [Fact]
        [Trait("ReqID", "SDS-??")]
        public void AltMerge()
        {
            Initialize();

            _v1.Reference = "A";
            _v2.Reference = "A";
            _v3.Reference = "A";

            _v1.Alternate = "C";
            _v2.Alternate = ".";
            _v3.Alternate = "ACGTTT";

            string[] mergedRefAndAlt = _formatter.MergeReferenceAndAlt(new List<CalledAllele> { _v1, _v2, _v3 });

            //string expectedReference = "A,A,A";
            string expectedReference = "A";
            string expectedAlt = "C,.,ACGTTT";
            Assert.Equal(expectedReference, mergedRefAndAlt[0]);
            Assert.Equal(expectedAlt, mergedRefAndAlt[1]);

        }

        [Fact]
        [Trait("ReqID", "SDS-VCF-9-QUAL")]
        public void QMerge()
        {
            Initialize();

            _v1.VariantQscore = 200;
            _v2.VariantQscore = 20;
            _v3.VariantQscore = 50;

            int mergedQ = _formatter.MergeVariantQScores(new List<CalledAllele> { _v1, _v2, _v3 });
             Assert.Equal(20, mergedQ);

        }

        [Fact]
        [Trait("ReqID", "SDS-VCF-9-INFO-and-FORMAT")]
        public void InfoAndFormatMerge()
        {
            Initialize();

            CalledAllele _v0 = TestHelper.CreatePassingVariant(true);

            _v0.GenotypeQscore = 42;
            _v1.GenotypeQscore = 200;
            _v2.GenotypeQscore = 20;
            _v3.GenotypeQscore = 50;

            _v1.AlleleSupport = 10;
            _v2.AlleleSupport = 20;
            _v3.AlleleSupport = 30;

            _v1.TotalCoverage = 100;
            _v2.TotalCoverage = 100;
            _v3.TotalCoverage = 100;

            _v0.NoiseLevelApplied = _estimatedBaseCallQuality;
            _v1.NoiseLevelApplied = _estimatedBaseCallQuality;
            _v2.NoiseLevelApplied = _estimatedBaseCallQuality;
            _v3.NoiseLevelApplied = _estimatedBaseCallQuality;


            _v1.Genotype = Genotype.HomozygousRef;
            string[] oneVariantTest = _formatter.ConstructFormatAndSampleString(new List<CalledAllele> { _v0, }, 490);

            _v1.Genotype = Genotype.HeterozygousAltRef;
            string[] twoVariantTestAltRef = _formatter.ConstructFormatAndSampleString(new List<CalledAllele> { _v1 }, 63);

            _v1.Genotype = Genotype.HeterozygousAlt1Alt2;
            string[] twoVariantTestAlt1Alt2 = _formatter.ConstructFormatAndSampleString(new List<CalledAllele> { _v1, _v2, }, 65);
            
            string[] threeVariantTest = _formatter.ConstructFormatAndSampleString(new List<CalledAllele> { _v1, _v2, _v3 }, 78);

            string expectedFormat = "GT:GQ:AD:DP:VF:NL:SB:NC";
            string expectedRefSample = "0/0:42:490:490:0.0000:23:0.0000:0.0000";
            string expectedAltRefSample = "0/1:200:0,10:63:0.1000:23:0.0000:0.0000";
            string expectedAlt1Alt2Sample = "1/2:20:10,20:65:0.3000:23:0.0000:0.0000";
            string expected3VarSample = "1/2:20:10,20,30:78:0.6000:23:0.0000:0.0000";

            Assert.Equal(expectedFormat, oneVariantTest[0]);
            Assert.Equal(expectedRefSample, oneVariantTest[1]);

            Assert.Equal(expectedFormat, twoVariantTestAltRef[0]);
            Assert.Equal(expectedAltRefSample, twoVariantTestAltRef[1]);

            Assert.Equal(expectedFormat, twoVariantTestAlt1Alt2[0]);
            Assert.Equal(expectedAlt1Alt2Sample, twoVariantTestAlt1Alt2[1]);

            Assert.Equal(expectedFormat, threeVariantTest[0]);
            Assert.Equal(expected3VarSample, threeVariantTest[1]);

        }
    }
}
