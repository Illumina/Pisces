using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.IO.Interfaces
{
    public interface IRegionMapper
    {
        CalledAllele GetNextEmptyCall(int startPosition, int? maxUpToPosition);
    }
}
