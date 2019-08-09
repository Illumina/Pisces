using System;
using System.Collections.Generic;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class VcfAllele 
    {
        public string OriginalVcfString;
        public bool OriginalVcfStringIsNotValid;
        public CalledAllele CalledAllele;
    }
}
