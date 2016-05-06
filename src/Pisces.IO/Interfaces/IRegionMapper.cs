using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.IO.Interfaces
{
    public interface IRegionMapper
    {
        CalledReference GetNextEmptyCall(int startPosition, int? maxUpToPosition);
    }
}
