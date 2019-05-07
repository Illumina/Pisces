using System.IO;
using Pisces.Domain.Options;

namespace AdaptiveGenotyper
{
    public class AdaptiveGtOptions : VcfConsumerAppOptions
    {
        public string LogFileName { get; set; } = "AdaptiveGTLog.txt";
        public string ModelFile { get; set; }

        public AdaptiveGtOptions()
        {
            // Set defaults
            VariantCallingParams = new VariantCallingParameters
            {
                PloidyModel = Pisces.Domain.Types.PloidyModel.DiploidByAdaptiveGT,
                IsMale = false
            };

            VcfWritingParams = new VcfWritingParameters
            {
                OutputGvcfFile = false,
                ForceCrush = true,
                ReportGp = true
            };

            SetDerivedValues();
        }

        public override string GetMainInputDirectory()
        {
            return Path.GetDirectoryName(VcfPath);
        }
    }
}
