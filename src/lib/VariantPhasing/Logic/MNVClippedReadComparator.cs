using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Models;
using VariantPhasing.Interfaces;

namespace VariantPhasing.Logic
{
    public class MNVClippedReadComparator : IMNVClippedReadComparator
    {
        private IMNVSoftClipReadFilter _scReadFilter;
        public MNVClippedReadComparator(IMNVSoftClipReadFilter scReadFilter)
        {
            _scReadFilter = scReadFilter;
        }

        public bool DoesClippedReadSupportMNV(Read clippedRead, CalledAllele mnv)
        {
            var (readPrefixClippedAtMNVSite, readSuffixClippedAtMNVSite) = _scReadFilter.IsReadClippedAtMNVSite(clippedRead, mnv);
            var variantHaplotype = mnv.AlternateAllele;
            var variantLength = variantHaplotype.Length;
            if (readPrefixClippedAtMNVSite)
            {
                if (clippedRead.StartsWithSoftClip)
                {
                    var clippedPrefix = clippedRead.ClippedPrefix;
                    if (clippedPrefix.Length >= variantLength)
                    {
                        if (clippedPrefix.Substring(clippedPrefix.Length - variantLength) == variantHaplotype)
                        {
                            return true;
                        }
                    }
                }
            }
            if (readSuffixClippedAtMNVSite)
            {
                if (clippedRead.EndsWithSoftClip)
                {
                    var clippedSuffix = clippedRead.ClippedSuffix;
                    if (clippedSuffix.Length >= variantLength)
                    {
                        if (clippedSuffix.Substring(0, variantLength) == variantHaplotype)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        }
    }
}