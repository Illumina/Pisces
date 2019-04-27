using Pisces.Domain.Options;
using Pisces.Domain.Types;

namespace Pisces.Genotyping
{
    public class GenotypeCreator
    {
        public static IGenotypeCalculator CreateGenotypeCalculator(PloidyModel ploidyModel, float minimumFrequencyFilter,
             int minEmitDepth, DiploidThresholdingParameters snvParameters, DiploidThresholdingParameters indelParameters,
             AdaptiveGenotypingParameters adaptiveGenotypingParameters,
             int minGQscore, int maxGQscore, float targetLODVariantFrequency,
             float minimumEmitFrequency = 0,
             string refName = null, bool? isMale = null)
        {

            var ploidyModelForThisChr = GetPloidyForThisChr(ploidyModel, isMale, refName);

            switch (ploidyModelForThisChr)
            {

                case PloidyModel.Haploid:
                    return new HaploidGenotyper(minEmitDepth, minGQscore, maxGQscore, snvParameters.MinorVF, snvParameters.MajorVF);

                case PloidyModel.DiploidByAdaptiveGT:
                    return new DiploidAdaptiveGenotyper(minEmitDepth, minGQscore, maxGQscore, adaptiveGenotypingParameters);

                case PloidyModel.DiploidByThresholding:
                    return new DiploidThresholdingGenotyper(snvParameters, snvParameters, minEmitDepth, minGQscore, maxGQscore);

                case PloidyModel.Somatic:
                default:
                    return new SomaticGenotyper(minimumFrequencyFilter, minEmitDepth, minGQscore, maxGQscore,
                    minimumEmitFrequency, targetLODVariantFrequency);

            }

        }

        public static PloidyModel GetPloidyForThisChr(PloidyModel samplePloidy, bool? isMale, string refName)
        {
            //if the sample is somatic, we always call it as somatic.
            //if the sample is Mitochondrial, we call as heteroplasmic (somatic in our implementaion)
            if ((samplePloidy == PloidyModel.Somatic) || (refName == "chrM") || (refName == "M"))
                return PloidyModel.Somatic;


            //if we say its haploid, or its a sex chr, call as haploid
            if (samplePloidy == PloidyModel.Haploid)
                return PloidyModel.Haploid;

            //if we did not set a gender, treat all chr equal
            if (isMale == null)
                return samplePloidy;

            if (isMale.Value && (refName == "chrY" || refName == "chrX" || refName == "Y" || refName == "X"))
                return PloidyModel.Haploid;

            if (!isMale.Value && (refName == "chrY" || refName == "Y"))
            {
                Common.IO.Utility.Logger.WriteWarningToLog("chrY exists in Female samples");
                return PloidyModel.Haploid;
            }

            //The only remaining option is that this is meant to be a diploid sample, 
            // and we are processing a chr that is meant to be diploid. We can call it directly, as requested..
            return samplePloidy;

        }
    }

}
