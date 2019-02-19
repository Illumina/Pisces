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
        // The four component model is based on tests based on Ampliseq exome showing that there is a fourth mean 
        // converging around 0.17 in allele frequency.  This could be a result of a sequencing artifact or ploidy 
        // differences?
        private readonly double[] _defaultMeans = new double[] { 0.04, 0.2, 0.5, 0.99 };

        private List<double[]> ModelMeans = new List<double[]>();
        private List<double[]> Priors = new List<double[]>();
        private List<RecalibratedVariantsCollection> Variants;

        public void Recalibrate(string vcfIn, string outDir, string modelFile, string quotedCmd)
        {
            // Read in VCF
            Logger.WriteToLog("Reading in vcf for variant frequencies.");
            Variants = new VariantReader().GetVariantFrequencies(vcfIn);

            // Fit new models or load models from file
            List<MixtureModel> models;
            if (modelFile == null)
                models = GetNewModels(vcfIn, outDir);
            else
                models = ApplyModels(vcfIn, modelFile);

            SummarizeModels(models);

            VcfRewriter rewriter = new VcfRewriter(Variants, ModelMeans, Priors);
            rewriter.Rewrite(vcfIn, outDir, quotedCmd);
        }

        private List<MixtureModel> GetNewModels(string vcfIn, string outDir)
        {
            var models = new List<MixtureModel>();

            Logger.WriteToLog("Finding thresholds for SNVs.");
            MixtureModel snvModel = new MixtureModel(Variants[0].Ad, Variants[0].Dp, _defaultMeans);

            // Try the 4 component mixture model first
            try
            {                
                snvModel.FitBinomialModel();
            }
            catch
            {
                // Do the 3 component one if not enough data for 4 components
                Logger.WriteToLog("Not enough data to fit 4 component mixture model, trying 3 component model...");
                snvModel = new MixtureModel(Variants[0].Ad, Variants[0].Dp);
                snvModel.FitBinomialModel();
            }
            models.Add(snvModel);

            // Perform fitting for indels
            Logger.WriteToLog("Finding thresholds for indels.");
            MixtureModel indelModel = new MixtureModel(Variants[1].Ad, Variants[1].Dp);
            indelModel.FitBinomialModel();
            models.Add(indelModel);

            MixtureModel.WriteModelFile(outDir, Path.GetFileName(vcfIn).Replace(".vcf", ".model"), models);
            return models;
            
        }

        private List<MixtureModel> ApplyModels(string vcfIn, string modelFile)
        {
            List<MixtureModelInput> modelParams = MixtureModel.ReadModelsFile(modelFile);
            var models = new List<MixtureModel>();

            Logger.WriteToLog("Applying models");
            for (int i = 0; i < Variants.Count; i++)
            {
                models.Add(new MixtureModel(Variants[i].Ad, Variants[i].Dp, modelParams[i].Means, modelParams[i].Weights));
                models[i].UpdateClusteringAndQScore();
            }

            return models;
        }

        private void SummarizeModels(List<MixtureModel> models)
        {
            for (int m = 0; m < models.Count; m++)
            {
                ModelMeans.Add(models[m].Means);
                Priors.Add(models[m].MixtureWeights);
                Variants[m].AddMixtureModelResults(models[m]);
            }
        }     

    }
}
