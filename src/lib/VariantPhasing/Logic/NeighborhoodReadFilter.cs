using System;	
using System.IO;	
using System.Collections.Generic;	
using System.Text;	
using System.Linq;	
using Pisces.Domain.Interfaces;	
using Pisces.Domain.Models;	
using Pisces.Domain.Options;	
using VariantPhasing.Interfaces;	
using VariantPhasing.Models;	
using Common.IO.Utility;	
	
	
namespace VariantPhasing.Logic
{	
    public class NeighborhoodReadFilter
    {	
        private readonly BamFilterParameters _options;	
	
        public NeighborhoodReadFilter(BamFilterParameters options)
        {	
            _options = options;	
        }	
	
        public bool PastNeighborhood(Read read, CallableNeighborhood neighborhood)
        {	
            return read.Position > neighborhood.LastPositionOfInterestWithLookAhead;	
        }	
	
        public bool ShouldSkipRead(Read read, CallableNeighborhood neighborhood)
        {	
            if (_options.RemoveDuplicates)	
            {	
                if (read.IsPcrDuplicate) return true;	
            }	
	
            if (_options.OnlyUseProperPairs)	
            {	
                if (!read.IsProperPair) return true;	
            }	
	
            if (read.MapQuality<_options.MinimumMapQuality) return true;	
            if (read.EndPosition<neighborhood.FirstPositionOfInterest)	
                return true;	
	
            return false;	
        }	
	
        public bool IsClippedWithinNeighborhood(Read read, CallableNeighborhood neighborhood)
        {	
            // Check if clipped at beginning of read, and position of read (end of clipping) falls into neighborhood	
            if (read.StartsWithSoftClip && 	
                (read.Position >= neighborhood.SoftClipEndBeforeNbhd && read.Position <= neighborhood.SoftClipPosAfterNbhd))	
            {	
                return true;	
            }	
            // Check if clipped at end of read, and end position of read (beginning of clip) falls into neighborhood	
            else if (read.EndsWithSoftClip && 	
                (read.EndPosition >= neighborhood.SoftClipEndBeforeNbhd && read.EndPosition <= neighborhood.SoftClipPosAfterNbhd))	
            {	
                return true;	
            }	
            	
            return false;	
        }	
	
    }	
}