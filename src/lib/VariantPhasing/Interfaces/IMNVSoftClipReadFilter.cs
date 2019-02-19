using Pisces.Domain.Models;	
using Pisces.Domain.Models.Alleles;	
using System;	
using System.Collections.Generic;	
using System.Text;	
	
namespace VariantPhasing.Interfaces
{	
    public interface IMNVSoftClipReadFilter
    {	
        (bool, bool) IsReadClippedAtMNVSite(Read clippedRead, CalledAllele mnv);	
    }	
}