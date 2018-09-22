using Pisces.Domain.Types;

namespace Pisces.Domain.Options
{
    public class VcfWritingParameters
    {
        public bool OutputGvcfFile = true;
        public bool MitochondrialChrComesFirst = false; // how we order variants in the output vcf (replace some code in VcfNbhd.cs)
        public bool? ForceCrush = null; //override default crush / no crush behavior. (be default, this is governed by the ploidy)
        public bool AllowMultipleVcfLinesPerLoci = true; //to crush or not to crush
        public bool ReportNoCalls = false;
        public bool ReportRcCounts = false;
        public bool ReportTsCounts = false;
        public double StrandBiasScoreMinimumToWriteToVCF = -100; // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        public double StrandBiasScoreMaximumToWriteToVCF = 0;
        public bool ReportSuspiciousCoverageFraction = false;

        public void SetDerivedParameters(VariantCallingParameters varcallParameters)
        {
            //SetDerivedParameters these accoding to ploidy model
            if (varcallParameters.PloidyModel == PloidyModel.Diploid)
            {
                AllowMultipleVcfLinesPerLoci = false;
            }
            else
                AllowMultipleVcfLinesPerLoci = true;

            // override them if desired
            if (ForceCrush.HasValue)
            {
                AllowMultipleVcfLinesPerLoci = !((bool)ForceCrush);
                return;
            }
        }

    }

}
