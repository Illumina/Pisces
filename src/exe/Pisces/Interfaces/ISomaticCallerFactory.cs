﻿using Pisces.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;

namespace Pisces.Interfaces
{
    public interface ISomaticCallerFactory
    {
        SmallVariantCaller Get(ChrReference chrReference, string bamFilePath, IVcfWriter<CalledAllele> vcfWriter);
    }
}
