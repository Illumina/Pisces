using System;
using Common.IO.Utility;

namespace Pisces.Domain.Options
{
    public class VcfConsumerAppOptions : BaseApplicationOptions
    {

        public VcfWritingParameters VcfWritingParams = new VcfWritingParameters();
        public VariantCallingParameters VariantCallingParams = new VariantCallingParameters();
        public BamFilterParameters BamFilterParams = new BamFilterParameters();

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
