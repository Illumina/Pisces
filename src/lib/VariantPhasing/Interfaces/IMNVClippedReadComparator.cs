using Pisces.Domain.Models;	
using Pisces.Domain.Models.Alleles;	
	
namespace VariantPhasing.Interfaces
{	
    public interface IMNVClippedReadComparator
    {	
        bool DoesClippedReadSupportMNV(Read clippedRead, CalledAllele mnv);	
    }	
}