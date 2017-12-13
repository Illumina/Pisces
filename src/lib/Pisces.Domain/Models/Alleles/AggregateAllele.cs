using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;
using Pisces.Domain.Models;

namespace Pisces.Domain.Models.Alleles
{
    public class AggregateAllele : CalledAllele
    {
        public BiasResults PoolBiasResults { get; set; }

        public List<CalledAllele> ComponentAlleles { get; set; }

        public AggregateAllele(List<CalledAllele> componentAlleles) : base()
        {
            ComponentAlleles = componentAlleles;
        }
        public AggregateAllele(CalledAllele originalAllele, List<CalledAllele> componentAlleles) : base(originalAllele)
        {
            ComponentAlleles = componentAlleles;
        }

        public static AggregateAllele SafeCopy(CalledAllele originalAllele, List<CalledAllele> componentAlleles)
        {
            if (originalAllele == null)
                return null;

            return new AggregateAllele(originalAllele, componentAlleles);
        }
    }
}