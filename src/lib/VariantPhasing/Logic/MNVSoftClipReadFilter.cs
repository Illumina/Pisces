using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using VariantPhasing.Interfaces;

namespace VariantPhasing.Logic
{
    public class MNVSoftClipReadFilter : IMNVSoftClipReadFilter
    {

        public (bool, bool) IsReadClippedAtMNVSite(Read clippedRead, CalledAllele mnv)
        {
            // Return (readPrefixClippedAtMNVSite, readSuffixClippedAtMNVSite)
            var readPrefixClippedAtMNVSite = false;
            var readSuffixClippedAtMNVSite = false;
            // Soft clipping starts at a position that MNV deviates from reference:
            //  Pos:          1   5    10
            //  Ref:          ACTAACGCACTT
            //  Alt Haplo:    ACTAAGGTACTT
            //                RRRRRSSS      -> If read ends with soft clip
            //                    SSSSRRRR  -> If read starts with soft clip

            // First difference position between ref and alt haplotypes gives potential end position of a clipped read
            var expectedClippedReadEndPosition = -1;
            // Case 1: First character of ref and alt is the same 
            //         second characters (if exists) must be different (AAG > AG should be trimmed to AG > A)
            // Example: GGGG > G, GAC > G, G > GGG
            if (mnv.ReferenceAllele[0] == mnv.AlternateAllele[0])
            {
                expectedClippedReadEndPosition = mnv.ReferencePosition;     // Last matching position before soft clip starts
            }
            else // Case 2: if ref and alt have different first characters (i.e., SNV or sth like GC > TA)
            {
                expectedClippedReadEndPosition = mnv.ReferencePosition - 1; // Last matching position before soft clip starts
            }

            // Last difference position between ref and alt haplotypes gives potential start position of a clipped read
            var expectedClippedReadPosition = -1;
            // Case 1: Last character of ref and alt is the same 
            //         second to last characters (if exists) must be different (otherwise trimmed before)
            // Example: GGGG > G, G > GGG
            if (mnv.ReferenceAllele[mnv.ReferenceAllele.Length - 1] == mnv.AlternateAllele[mnv.AlternateAllele.Length - 1])
            {
                expectedClippedReadPosition = mnv.ReferencePosition + mnv.ReferenceAllele.Length - 1;
            }
            else // Case 2: if ref and alt have different first characters (i.e., SNV)
            {
                expectedClippedReadPosition = mnv.ReferencePosition + mnv.ReferenceAllele.Length;
            }


            // Find out which direction the read is clipped
            if (clippedRead.EndsWithSoftClip)
            {
                // Clipped read's end position must be equal to expected clipped read end position
                if (clippedRead.EndPosition == expectedClippedReadEndPosition)
                {
                    readSuffixClippedAtMNVSite = true;
                }
            }
            if (clippedRead.StartsWithSoftClip)
            {
                // Clipped read's position must be equal to expected clipped read position
                if (clippedRead.Position == expectedClippedReadPosition)
                {
                    readPrefixClippedAtMNVSite = true;
                }
            }
            return (readPrefixClippedAtMNVSite, readSuffixClippedAtMNVSite);
        }
    }
}