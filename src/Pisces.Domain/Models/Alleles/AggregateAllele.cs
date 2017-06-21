using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class AggregateAllele : CalledAllele
    {
        public BiasResults PoolBiasResults { get; set; }
    }
}
