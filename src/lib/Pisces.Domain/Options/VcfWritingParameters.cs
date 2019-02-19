using Pisces.Domain.Types;

namespace Pisces.Domain.Options
{
    public class VcfWritingParameters
    {
        public bool OutputGvcfFile = true;
        public bool? ForceCrush = null; //override default crush / no crush behavior. (be default, this is governed by the ploidy)
        public bool AllowMultipleVcfLinesPerLoci = true; //to crush or not to crush
        public bool ReportNoCalls = false;
        public bool ReportRcCounts = false;
        public bool ReportTsCounts = false;
        public bool ReportGp = false;
        public double StrandBiasScoreMinimumToWriteToVCF = -100; // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        public double StrandBiasScoreMaximumToWriteToVCF = 0;
        public bool ReportSuspiciousCoverageFraction = false;

        public void SetDerivedParameters(VariantCallingParameters varcallParameters)
        {
            //SetDerivedParameters these according to ploidy model.
            //tjd: I WISH we just never crushed vcfs... Crushing is apparently preffered by germline customers.
            if ((varcallParameters.PloidyModel == PloidyModel.DiploidByThresholding) ||
                (varcallParameters.PloidyModel == PloidyModel.DiploidByAdaptiveGT))
            {
                AllowMultipleVcfLinesPerLoci = false;
        
            }
            else
                AllowMultipleVcfLinesPerLoci = true;

            // override them if desired
            if (ForceCrush.HasValue)
            {
                AllowMultipleVcfLinesPerLoci = !((bool)ForceCrush);
            }

            //if we have the posteriors data from the adaptive GT model, lets provide it!
            if (varcallParameters.PloidyModel == PloidyModel.DiploidByAdaptiveGT)
                ReportGp = true;
        }

    }

}
