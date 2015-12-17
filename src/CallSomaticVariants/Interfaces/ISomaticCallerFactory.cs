using CallSomaticVariants.Models;

namespace CallSomaticVariants.Interfaces
{
    public interface ISomaticCallerFactory
    {
        SomaticVariantCaller Get(ChrReference chrReference, string bamFilePath, IVcfWriter vcfWriter);
    }
}
