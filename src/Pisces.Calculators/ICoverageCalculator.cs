using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Calculators
{
    public interface ICoverageCalculator
    {
        void Compute(CalledAllele allele, IAlleleSource alleleCountSource);
    }
}
