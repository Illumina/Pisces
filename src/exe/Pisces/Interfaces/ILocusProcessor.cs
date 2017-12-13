using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;

namespace Pisces.Interfaces
{
    public interface ILocusProcessor
    {
        void Process(List<CalledAllele> calledAllelesInPosition);

    }
}   