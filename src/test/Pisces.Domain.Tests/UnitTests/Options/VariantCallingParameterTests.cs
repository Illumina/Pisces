using System;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Xunit;

namespace Pisces.Domain.Tests
{
    public class VariantCallingParameterTests
    {
        [Fact]
        [Trait("ReqID", "SDS-1")]
        public void CommandLineParse()
        {
            var VariantCallingParameters = new VariantCallingParameters();

            VariantCallingParameters.Parse(new string[] {

              "-minvariantqscore", "1",
              "-variantqualityfilter", "80",
              "-maxvariantqscore", "1100",    //perhaps more of a vcf-writing parameter

              "-mindepth", "11",
              "-mindepthfilter", "15",

              "-minimumvariantfrequency", "0.42",
              "-minvariantfrequencyfilter", "0.45",

              "-mingenotypeqscore", "10",   //perhaps more of a vcf-writing parameter
              "-genotypequalityfilter", "35",
              "-maxgenotypeqscore", "120",    //perhaps more of a vcf-writing parameter

              "-repeatfilter", "4",

              "-rmxnfilter", "6,7,0.75",

              "-enablesinglestrandfilter", "true",
              "-sbmodel", "poisson",
              "-maxacceptablestrandbiasfilter", "0.75",

              "-noisemodel","window",
              "-noiselevelforqmodel", "72",

              "-ploidy", "diploid",
              "-diploidgenotypeparameters", "12,13,14",

              "-gender", "male",
        });

            Assert.Equal(VariantCallingParameters.MinimumVariantQScore, 1);
            Assert.Equal(VariantCallingParameters.MinimumVariantQScoreFilter, 80);
            Assert.Equal(VariantCallingParameters.MaximumVariantQScore, 1100);

            Assert.Equal(VariantCallingParameters.MinimumCoverage, 11);
            Assert.Equal(VariantCallingParameters.LowDepthFilter, 15);

            Assert.Equal(VariantCallingParameters.MinimumGenotypeQScore, 10);
            Assert.Equal(VariantCallingParameters.LowGenotypeQualityFilter, 35);
            Assert.Equal(VariantCallingParameters.MaximumGenotypeQScore, 120);

            Assert.Equal(VariantCallingParameters.IndelRepeatFilter, 4);

            Assert.Equal(VariantCallingParameters.RMxNFilterMaxLengthRepeat, 6);
            Assert.Equal(VariantCallingParameters.RMxNFilterMinRepetitions, 7);
            Assert.Equal(VariantCallingParameters.RMxNFilterFrequencyLimit, 0.75);

            Assert.Equal(VariantCallingParameters.FilterOutVariantsPresentOnlyOneStrand, true);
            Assert.Equal(VariantCallingParameters.StrandBiasModel, StrandBiasModel.Poisson);
            Assert.Equal(VariantCallingParameters.StrandBiasAcceptanceCriteria, 0.75F);

            Assert.Equal(VariantCallingParameters.NoiseModel, NoiseModel.Window);
            Assert.Equal(VariantCallingParameters.NoiseLevelUsedForQScoring, 20);
            Assert.Equal(VariantCallingParameters.ForcedNoiseLevel, 72);

            Assert.Equal(VariantCallingParameters.PloidyModel, PloidyModel.Diploid);
            Assert.Equal(VariantCallingParameters.DiploidThresholdingParameters.MinorVF, 12);
            Assert.Equal(VariantCallingParameters.DiploidThresholdingParameters.MajorVF, 13);
            Assert.Equal(VariantCallingParameters.DiploidThresholdingParameters.SumVFforMultiAllelicSite, 14);

            Assert.Equal(VariantCallingParameters.IsMale, true);
        }

        [Fact]
        [Trait("ReqID", "SDS-2")]
        public void Validate_HappyPath()
        {

            //check defaults are reasonable (after validation):
            var VariantCallingParameters = new VariantCallingParameters();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.01F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, -1);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency,-1);

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.01F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.01F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.01F);

            //check defaults for low-freq var calling
            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.0001F;

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.0001F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, -1);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, -1);

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.0001F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.0001F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.0001F);

            //check defaults for high-freq calling
            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.20F;

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.20F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, -1);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, -1);

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.20F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.20F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.20F);

            //check defaults for typical case calling
            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency= 0.01F;
            VariantCallingParameters.MinimumFrequencyFilter= 0.026F;
            VariantCallingParameters.TargetLODFrequency= 0.05F;

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.01F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.026F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.05F);

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.01F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.026F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.05F);
        }

        [Fact]
        [Trait("ReqID", "SDS-2")]
        public void Validate_Pathological()
        {

            //check it fixes the LOD

            var VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.03F;
            VariantCallingParameters.MinimumFrequencyFilter = 0.03F;
            VariantCallingParameters.TargetLODFrequency = 0.005F;

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.03F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.03F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.03F);

            //check it fixes the filter

            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.03F;
            VariantCallingParameters.MinimumFrequencyFilter = 0.02F;
            VariantCallingParameters.TargetLODFrequency = 0.02F;

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.03F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.03F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.03F);

            //check it catches the LOD mess

            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.03F;
            VariantCallingParameters.MinimumFrequencyFilter = -2F;
            VariantCallingParameters.TargetLODFrequency = -3F;

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.03F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.03F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.03F);


            //check it catches the Filter mess

            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = 0.03F;
            VariantCallingParameters.MinimumFrequencyFilter = -3F;
            VariantCallingParameters.TargetLODFrequency = 0.04F;

            VariantCallingParameters.Validate();

            Assert.Equal(VariantCallingParameters.MinimumFrequency, 0.03F);
            Assert.Equal(VariantCallingParameters.MinimumFrequencyFilter, 0.03F);
            Assert.Equal(VariantCallingParameters.TargetLODFrequency, 0.04F);

            //check it catches the Freq mess (nothing we can do here but throw)

            VariantCallingParameters = new VariantCallingParameters();
            VariantCallingParameters.MinimumFrequency = -3F;
            VariantCallingParameters.MinimumFrequencyFilter = 0.03F;
            VariantCallingParameters.TargetLODFrequency = 0.04F;

            Assert.Throws<ArgumentException>(() => VariantCallingParameters.Validate());


        }
    }
}