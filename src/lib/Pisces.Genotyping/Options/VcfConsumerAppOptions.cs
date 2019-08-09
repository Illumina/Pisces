using System;
using Common.IO.Utility;

namespace Pisces.Domain.Options
{
    public class VcfConsumerAppOptions : BaseApplicationOptions
    {
        public string VcfPath { get; set; }
        public VcfWritingParameters VcfWritingParams { get; set; } = new VcfWritingParameters();
        public VariantCallingParameters VariantCallingParams { get; set; } = new VariantCallingParameters();
        public BamFilterParameters BamFilterParams { get; set; } = new BamFilterParameters();

        public void SetDerivedValues()
        {        
            VariantCallingParams.SetDerivedParameters(BamFilterParams);
            VcfWritingParams.SetDerivedParameters(VariantCallingParams);
        }

        public void Validate()
        {
            BamFilterParams.Validate();
            VariantCallingParams.Validate();
        }

       

    }
}
