using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using CommandLine.Options;
using Xunit;

namespace Pisces.Domain.Tests
{
    public class VariantCallingParameterTests
    {

        [Fact]
        [Trait("ReqID", "SDS-1")]
        public void CommandLineVarCallParse()
        {
            var expectations1 = GetOriginalOptionsExpectations();
            Action<VariantCallingParameters> expectations = null;
            foreach (var option in expectations1.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", expectations1.Keys), expectations);

            var expectations2 = GetOptionsExpectations2();
            expectations = null;
            foreach (var option in expectations2.Values)
            {
                expectations += option;
            }

            ExecuteParsingTest(string.Join(" ", expectations2.Keys), expectations);
        }

        [Fact]

        public void CheckAdaptiveGTFileParsing()
        {
            var file = Path.Combine(TestPaths.LocalTestDataDirectory, "example.model");
            var arguments = "-adaptivegenotypeparameters_fromfile " + file;
            var parsedOptions = GetParsedApplicationOptions(arguments);
            var varcallingParams = ((PiscesApplicationOptions)parsedOptions.Options).VariantCallingParameters;

            Assert.Equal(1, varcallingParams.AdaptiveGenotypingParameters.SnvModel[0]);
            Assert.Equal(2, varcallingParams.AdaptiveGenotypingParameters.SnvModel[1]);
            Assert.Equal(3, varcallingParams.AdaptiveGenotypingParameters.SnvModel[2]);

            Assert.Equal(4, varcallingParams.AdaptiveGenotypingParameters.IndelPrior[0]);
            Assert.Equal(5, varcallingParams.AdaptiveGenotypingParameters.IndelPrior[1]);
            Assert.Equal(6, varcallingParams.AdaptiveGenotypingParameters.IndelPrior[2]);
        }

            private Dictionary<string, Action<VariantCallingParameters>> GetOriginalOptionsExpectations()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<VariantCallingParameters>>();
            
            optionsExpectationsDict.Add("-minvq 40", (o) => Assert.Equal(40, o.MinimumVariantQScore));
            optionsExpectationsDict.Add("-variantqualityfilter 80", (o) => Assert.Equal(80, o.MinimumVariantQScoreFilter));
           optionsExpectationsDict.Add("-mindepth 11", (o) => Assert.Equal(11, o.MinimumCoverage));
            optionsExpectationsDict.Add("-mindepthfilter 15", (o) => Assert.Equal(15, o.LowDepthFilter));     
            optionsExpectationsDict.Add("-minimumvariantfrequency 0.42", (o) => Assert.Equal(0.42F, o.MinimumFrequency));
            optionsExpectationsDict.Add("-minvariantfrequencyfilter 0.45", (o) => Assert.Equal(0.45F, o.MinimumFrequencyFilter));

            optionsExpectationsDict.Add("-mingenotypeqscore 10", (o) => Assert.Equal(10, o.MinimumGenotypeQScore));
            optionsExpectationsDict.Add("-genotypequalityfilter 35", (o) => Assert.Equal(35, o.LowGenotypeQualityFilter));
            optionsExpectationsDict.Add("-maxgenotypeqscore 120", (o) => Assert.Equal(120, o.MaximumGenotypeQScore));
            optionsExpectationsDict.Add("-repeatfilter_ToBeRetired 4", (o) => Assert.Equal(4, o.IndelRepeatFilter));

            optionsExpectationsDict.Add("-rmxnfilter 6,7,0.75", (o) => Assert.True(
                        (6 == o.RMxNFilterMaxLengthRepeat) &&
                        (7 == o.RMxNFilterMinRepetitions) &&
                        (0.75F == o.RMxNFilterFrequencyLimit)));

            optionsExpectationsDict.Add("-enablesinglestrandfilter true", (o) => Assert.Equal(true, o.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add("-sbmodel poisson", (o) => Assert.Equal(StrandBiasModel.Poisson, o.StrandBiasModel));
            optionsExpectationsDict.Add("-maxacceptablestrandbiasfilter 0.75", (o) => Assert.Equal(0.75F, o.StrandBiasAcceptanceCriteria));            
            optionsExpectationsDict.Add("-noisemodel window", (o) => Assert.Equal(NoiseModel.Window, o.NoiseModel));
            optionsExpectationsDict.Add("-NL 72", (o) => Assert.Equal(72, o.ForcedNoiseLevel));
            optionsExpectationsDict.Add("-ploidy diploid", (o) => Assert.Equal(PloidyModel.DiploidByThresholding, o.PloidyModel));
            
            optionsExpectationsDict.Add("-diploidsnvgenotypeparameters 12,13,14", (o) => Assert.True(
                        (12 == o.DiploidSNVThresholdingParameters.MinorVF) &&
                        (13 == o.DiploidSNVThresholdingParameters.MajorVF) &&
                        (14 == o.DiploidSNVThresholdingParameters.SumVFforMultiAllelicSite)));
                    
            optionsExpectationsDict.Add("-gender male", (o) => Assert.Equal(true, o.IsMale));

            optionsExpectationsDict.Add("-adaptivegenotypeparameters_snvmodel 1,2,3", (o) => Assert.True(
                      (3 == o.AdaptiveGenotypingParameters.SnvModel.Length) &&
                      (1 == o.AdaptiveGenotypingParameters.SnvModel[0]) &&
                      (2 == o.AdaptiveGenotypingParameters.SnvModel[1]) &&
                      (3 == o.AdaptiveGenotypingParameters.SnvModel[2])));
            
            optionsExpectationsDict.Add("-adaptivegenotypeparameters_indelmodel 4,5,6,7", (o) => Assert.True(
                               (4 == o.AdaptiveGenotypingParameters.IndelModel.Length) &&
                               (4 == o.AdaptiveGenotypingParameters.IndelModel[0]) &&
                               (5 == o.AdaptiveGenotypingParameters.IndelModel[1]) &&
                               (6 == o.AdaptiveGenotypingParameters.IndelModel[2]) &&
                               (7 == o.AdaptiveGenotypingParameters.IndelModel[3])));
           
            optionsExpectationsDict.Add("-adaptivegenotypeparameters_snvprior .1,.2,.3,.5,.6", (o) => Assert.True(
          (5 == o.AdaptiveGenotypingParameters.SnvPrior.Length) &&
          (.1 == o.AdaptiveGenotypingParameters.SnvPrior[0]) &&
          (.2 == o.AdaptiveGenotypingParameters.SnvPrior[1]) &&
          (.3 == o.AdaptiveGenotypingParameters.SnvPrior[2]) &&
          (.5 == o.AdaptiveGenotypingParameters.SnvPrior[3]) &&
          (.6 == o.AdaptiveGenotypingParameters.SnvPrior[4])));

            optionsExpectationsDict.Add("-adaptivegenotypeparameters_indelprior 8,9", (o) => Assert.True(
                               (2 == o.AdaptiveGenotypingParameters.IndelPrior.Length) &&
                               (8 == o.AdaptiveGenotypingParameters.IndelPrior[0]) &&
                               (9 == o.AdaptiveGenotypingParameters.IndelPrior[1])));

            optionsExpectationsDict.Add("-maxgp 20", (o) => Assert.True(
                   (20 == o.AdaptiveGenotypingParameters.MaxGenotypePosteriors)));

            return optionsExpectationsDict;
        }


        //A slightly diff set of inputs, changing the capitalizations, parameter names, etc
        private Dictionary<string, Action<VariantCallingParameters>> GetOptionsExpectations2()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<VariantCallingParameters>>();

            optionsExpectationsDict.Add("-minvariantqscore 1", (o) => Assert.Equal(1, o.MinimumVariantQScore));
            optionsExpectationsDict.Add("-maxvariantqscore 1100", (o) => Assert.Equal(1100, o.MaximumVariantQScore));
            optionsExpectationsDict.Add("--mindepth 11", (o) => Assert.Equal(11, o.MinimumCoverage));
            optionsExpectationsDict.Add("--mindepthfilter 15", (o) => Assert.Equal(15, o.LowDepthFilter));
            optionsExpectationsDict.Add("-miNimumvariantfrequency 0.42", (o) => Assert.Equal(0.42F, o.MinimumFrequency));
            optionsExpectationsDict.Add("-Minvariantfrequencyfilter 0.45", (o) => Assert.Equal(0.45F, o.MinimumFrequencyFilter));

            optionsExpectationsDict.Add("-mingenotypeqscore 10", (o) => Assert.Equal(10, o.MinimumGenotypeQScore));
            optionsExpectationsDict.Add("-genotypequalityfilter 35", (o) => Assert.Equal(35, o.LowGenotypeQualityFilter));
            optionsExpectationsDict.Add("-maxgenotypeqscore 120", (o) => Assert.Equal(120, o.MaximumGenotypeQScore));
            optionsExpectationsDict.Add("--repeaTfilter_ToBeRetired 40", (o) => Assert.Equal(40, o.IndelRepeatFilter));

            optionsExpectationsDict.Add("--rmxnfilter 6,7,0.75", (o) => Assert.True(
                        (6 == o.RMxNFilterMaxLengthRepeat) &&
                        (7 == o.RMxNFilterMinRepetitions) &&
                        (0.75F == o.RMxNFilterFrequencyLimit)));

            optionsExpectationsDict.Add("-enablesinglestrandfilter true", (o) => Assert.Equal(true, o.FilterOutVariantsPresentOnlyOneStrand));
            optionsExpectationsDict.Add("-sbmodel diPloiD", (o) => Assert.Equal(StrandBiasModel.Diploid, o.StrandBiasModel));
            optionsExpectationsDict.Add("-maxacceptablestrandbiasfilter 0.75", (o) => Assert.Equal(0.75F, o.StrandBiasAcceptanceCriteria));
            optionsExpectationsDict.Add("-noisemodel flat", (o) => Assert.Equal(NoiseModel.Flat, o.NoiseModel));
            optionsExpectationsDict.Add("-noiselevelforqmodel 32", (o) => Assert.Equal(32, o.ForcedNoiseLevel));
            optionsExpectationsDict.Add("-PLOIDY somatic", (o) => Assert.Equal(PloidyModel.Somatic, o.PloidyModel));

            optionsExpectationsDict.Add("-diploidSNVgenotypeparameters 12,13,24", (o) => Assert.True(
                        (12 == o.DiploidSNVThresholdingParameters.MinorVF) &&
                        (13 == o.DiploidSNVThresholdingParameters.MajorVF) &&
                        (24 == o.DiploidSNVThresholdingParameters.SumVFforMultiAllelicSite)));
           
            optionsExpectationsDict.Add("-diploidINDELgenotypeparameters 122,133,244", (o) => Assert.True(
                      (122 == o.DiploidINDELThresholdingParameters.MinorVF) &&
                      (133 == o.DiploidINDELThresholdingParameters.MajorVF) &&
                      (244 == o.DiploidINDELThresholdingParameters.SumVFforMultiAllelicSite)));
                     
            optionsExpectationsDict.Add("-gender FeMALE", (o) => Assert.Equal(false, o.IsMale));
            return optionsExpectationsDict;
        }




        private void ExecuteParsingTest(string arguments, Action<VariantCallingParameters> assertions = null)
        {
            var options = GetParsedOptions(arguments).VariantCallingParameters;
            assertions(options);
        }

        private PiscesApplicationOptions GetParsedOptions(string arguments)
        {
            var parsedOptions = GetParsedApplicationOptions(arguments);
            return parsedOptions.PiscesOptions;
        }

        private PiscesOptionsParser GetParsedApplicationOptions(string arguments)
        {
            var parsedOptions = new PiscesOptionsParser();
            parsedOptions.ParseArgs(arguments.Split(' '), false);
            return parsedOptions;
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