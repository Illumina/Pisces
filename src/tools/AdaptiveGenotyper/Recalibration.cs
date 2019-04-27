using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Genotyping;
using Common.IO.Utility;

namespace AdaptiveGenotyper
{
    public class Recalibration
    {
        private readonly AdaptiveGtOptions _options;

        public Recalibration(AdaptiveGtOptions options)
        {
            _options = options;
        }

        public void Recalibrate()
        {
            // Read in VCF
            Logger.WriteToLog("Reading in vcf for variant frequencies.");
            List<RecalibratedVariantsCollection> variants = VariantReader.GetVariantFrequencies(_options.VcfPath);

            // Fit new models or load models from file
            List<MixtureModel> models;
            if (_options.ModelFile == null)
                models = GetNewModels(_options.VcfPath, _options.OutputDirectory, variants);
            else
                models = ApplyModels(_options.VcfPath, _options.ModelFile, variants);

            RecalibrationResults recalibrationResults = SummarizeModels(models, variants);
            AdaptiveGtWriter.RewriteVcf(_options.VcfPath, _options.OutputDirectory, _options, recalibrationResults);            
        }

        private static List<MixtureModel> GetNewModels(string vcfIn, string outDir, List<RecalibratedVariantsCollection> variants)
        {
            var models = new List<MixtureModel>();

            // Perform fitting for SNVs
            Logger.WriteToLog("Finding thresholds for SNVs.");
            MixtureModel snvModel = MixtureModel.FitMixtureModel(variants[0].Ad, variants[0].Dp);                       
            models.Add(snvModel);

            // Perform fitting for indels
            Logger.WriteToLog("Finding thresholds for indels.");
            MixtureModel indelModel = MixtureModel.FitMixtureModel(variants[1].Ad, variants[1].Dp);
            models.Add(indelModel);

            MixtureModel.WriteModelFile(outDir, Path.GetFileName(vcfIn).Replace(".vcf", ".model"), models);
            return models;
            
        }

        private static List<MixtureModel> ApplyModels(string vcfIn, string modelFile, List<RecalibratedVariantsCollection> variants)
        {
            List<MixtureModelParameters> modelParams = MixtureModel.ReadModelsFile(modelFile);
            var models = new List<MixtureModel>();

            Logger.WriteToLog("Applying models");
            for (int i = 0; i < variants.Count; i++)
            {
                models.Add(MixtureModel.UsePrefitModel(variants[i].Ad, variants[i].Dp, modelParams[i].Means, modelParams[i].Priors));
            }

            return models;
        }

        private static RecalibrationResults SummarizeModels(List<MixtureModel> models, List<RecalibratedVariantsCollection> variants)
        {
            for (int m = 0; m < models.Count; m++)
                variants[m].AddMixtureModelResults(models[m]);

            return new RecalibrationResults
            {
                SnvResults = new RecalibrationResult
                {
                    Means = models[0].Means,
                    Priors = models[0].MixtureWeights,
                    Variants = variants[0]
                },
                IndelResults = new RecalibrationResult
                {
                    Means = models[1].Means,
                    Priors = models[1].MixtureWeights,
                    Variants = variants[1]
                }
            };
        }     
    }

    public class RecalibrationResults
    {
        public RecalibrationResult SnvResults { get; set; }
        public RecalibrationResult IndelResults { get; set; }
    }

    public class RecalibrationResult
    {
        public double[] Means { get; set; }
        public double[] Priors { get; set; }
        public RecalibratedVariantsCollection Variants { get; set; }
    }
}
