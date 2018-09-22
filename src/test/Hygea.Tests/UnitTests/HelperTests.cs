using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic;
using Alignment.Domain.Sequencing;
using ReadRealignmentLogic;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class AlignmentComparerTests
    {
        [Fact]
        public void GetBetterResult()
        {
            var comparer = new BasicAlignmentComparer();
            var preferred = new RealignmentResult();
            var other = new RealignmentResult();

            Assert.Equal(preferred, comparer.GetBetterResult(preferred, other));
            Assert.Equal(other, comparer.GetBetterResult(other, preferred));

            Assert.Equal(preferred, comparer.GetBetterResult(preferred, null));
            Assert.Equal(preferred, comparer.GetBetterResult(null, preferred));

            Assert.Equal(null, comparer.GetBetterResult(null, null));

            preferred.NumMismatches = 5;
            Assert.Equal(other, comparer.GetBetterResult(preferred, other));
        }

    }
    public class HelperTests
    {
        [Fact]
        public void GetEditDistance()
        {
            var readSequence = "ACGTA";
            var referenceSequence = "ACGTACGTACGT";
            var positionMap = new[] { 5, 6, 7, 8, 9 };
            var readSequenceWithNs = "NCGTA";
            var readSequenceMismatch = "TCGTA";
            var refSequenceWithNs = "ACGTNCGTACGT";

            // exact match
            Assert.Equal(0, Helper.GetEditDistance(readSequence, positionMap, referenceSequence));

            // position map exact, with mismatch
            Assert.Equal(1, Helper.GetEditDistance(readSequenceMismatch, positionMap, referenceSequence));
            
            // position map exact, with Ns
            Assert.Equal(0, Helper.GetEditDistance(readSequenceWithNs, positionMap, referenceSequence));
            Assert.Equal(0, Helper.GetEditDistance(readSequence, positionMap, refSequenceWithNs));

            positionMap = new[] { 9, 10, 11, 12, 13 };
            Assert.Equal(null, Helper.GetEditDistance(readSequence, positionMap, referenceSequence));

            positionMap = new[] { 5, -1, 7, 9, 10 };
            Assert.Equal(2, Helper.GetEditDistance(readSequence, positionMap, referenceSequence));
        }


      
        [Fact]
        public void SoftclipCigar()
        {
            // ---- Softclip Prefix ---- //
            // Original cigar = 2S3M, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    TTAAG
            var rawRealignedCigar = new CigarAlignment("4M1I");
            var mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Unmapped };
            var softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0);
            Assert.Equal("2S2M1I", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal("4M1I", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0, maskNsOnly: true, prefixNs: 2, suffixNs: 0);
            Assert.Equal("2S2M1I", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns for only part of original softclip
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0, maskNsOnly: true, prefixNs: 1, suffixNs: 0);
            Assert.Equal("1S3M1I", softclippedCigar.ToString());

            // Original cigar = 2S3M, With terminal Ns, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    NNAAG
            mismatchMap = new[]
            { MatchType.NMismatch, MatchType.NMismatch, MatchType.Match, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0);
            Assert.Equal("2S2M1I", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 2, 0, maskNsOnly: true, prefixNs: 2, suffixNs: 0);
            Assert.Equal("2S2M1I", softclippedCigar.ToString());

            // Original cigar = 5M, Realignment adds I, realigned cigar should be unchanged by softclipping
            //  Ref:    AAAAA
            //  Alt:    TTAAG
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 0);
            Assert.Equal("4M1I", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal("4M1I", softclippedCigar.ToString());

            // Original cigar = 5M, still 5M, realigned cigar should be unchanged by softclipping
            //  Ref:    AAAAA
            //  Alt:    TTAAG
            var rawCigarAllMatches = new CigarAlignment("5M");
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawCigarAllMatches, mismatchMap, 2, 0);
            Assert.Equal("5M", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawCigarAllMatches, mismatchMap, 2, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal("5M", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    TATAG
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 3, 0);
            Assert.Equal("3S1M1I", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal("4M1I", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("3S1M1I", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns for only part of original softclip
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 1, suffixNs: 0);
            Assert.Equal("1S3M1I", softclippedCigar.ToString());
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 2, suffixNs: 0);
            Assert.Equal("2S2M1I", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TTTAG
            var rawRealignedCigar_StoI = new CigarAlignment("2M1I2M");
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0);
            Assert.Equal("2S1I2M", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal("2M1I2M", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("2S1I2M", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TATAG
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0);
            Assert.Equal("1S1M1I2M", softclippedCigar.ToString()); // If allow shortening of softclip if bases match
            //Assert.Equal("2S1I2M", softclippedCigar.ToString()); // If mask whole original S that became M, regardless of matchiness
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoI.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("2S1I2M", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TTTAG
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Unmapped };
            var rawRealignedCigar_StoID = new CigarAlignment("2M1D1I2M");
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0);
            Assert.Equal("2S1D1I2M", softclippedCigar.ToString()); // If mask whole original S that became M, regardless of matchiness
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoID.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("2S1D1I2M", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TTTAG
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Match, MatchType.Unmapped };
            rawRealignedCigar_StoID = new CigarAlignment("2M1I1D2M");
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0);
            Assert.Equal("2S1I1D2M", softclippedCigar.ToString()); // If mask whole original S that became M, regardless of matchiness
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoID.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID, mismatchMap, 3, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("2S1I1D2M", softclippedCigar.ToString());

            // Original cigar = 3S2M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TTTAG
            var rawRealignedCigar_StoD = new CigarAlignment("1M2D4M");
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoD, mismatchMap, 4, 0);
            Assert.Equal("1S2D4M", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoD, mismatchMap, 4, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoD.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoD, mismatchMap, 4, 0, maskNsOnly: true, prefixNs: 3, suffixNs: 0);
            Assert.Equal("1S2D4M", softclippedCigar.ToString());

            // Original cigar = 4S1M, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    TTTAG
            var rawRealignedCigar_noM = new CigarAlignment("4M1I");
            mismatchMap = new[]
            { MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch, MatchType.Unmapped };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_noM, mismatchMap, 4, 0);
            Assert.Equal("3S1M1I", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_noM, mismatchMap, 4, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_noM.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_noM, mismatchMap, 4, 0, maskNsOnly: true, prefixNs: 4, suffixNs: 0);
            Assert.Equal("3S1M1I", softclippedCigar.ToString());

            // ---- Softclip Suffix ---- //
            // Original cigar = 3M2S, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    GAATT
            rawRealignedCigar = new CigarAlignment("1I4M");
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Match, MatchType.Mismatch, MatchType.Mismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2);
            Assert.Equal("1I2M2S", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2, maskNsOnly: true, prefixNs: 0, suffixNs: 2);
            Assert.Equal("1I2M2S", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns for only part of original softclip
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2, maskNsOnly: true, prefixNs: 0, suffixNs: 1);
            Assert.Equal("1I3M1S", softclippedCigar.ToString());

            // Original cigar = 3M2S, With terminal Ns, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    GAANN
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Match, MatchType.NMismatch, MatchType.NMismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2);
            Assert.Equal("1I2M2S", softclippedCigar.ToString());
            // Remask Ns Only - has Ns, so should be same
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 2, maskNsOnly: true, prefixNs: 0, suffixNs: 2);
            Assert.Equal("1I2M2S", softclippedCigar.ToString());

            // Original cigar = 5M, Realignment adds I, realigned cigar should be unchanged by softclipping
            //  Ref:    AAAAA
            //  Alt:    GAATT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Match, MatchType.Mismatch, MatchType.Mismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 0);
            Assert.Equal("1I4M", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 0, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar.ToString(), softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I outside of S region
            //  Ref:    AAAAA
            //  Alt:    GATTT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3);
            Assert.Equal("1I1M3S", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("1I1M3S", softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns for only part of original softclip
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 2);
            Assert.Equal("1I2M2S", softclippedCigar.ToString());
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 1);
            Assert.Equal("1I3M1S", softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I outside of S region; MXM-type-softclip. No shortening of softclip.
            //  Ref:    AAAAA
            //  Alt:    GATAT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Mismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3);
            Assert.Equal("1I1M3S", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("1I1M3S", softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    GATTT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch };
            var rawRealignedCigar_StoI_suffix = new CigarAlignment("2M1I2M");
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI_suffix, mismatchMap, 0, 3);
            Assert.Equal("2M1I2S", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoI.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("2M1I2S", softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    GATAT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Mismatch, MatchType.Match, MatchType.Mismatch };
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 0, 3);
            Assert.Equal("2M1I1M1S", softclippedCigar.ToString()); // If allow shortening of softclip if bases match
            //Assert.Equal("2M1I2S", softclippedCigar.ToString()); // If mask whole original S that became M, regardless of matchiness
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoI.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoI, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("2M1I2S", softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    GATAT
            var rawRealignedCigar_StoID_suffix = new CigarAlignment("2M1I1D2M");
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3);
            Assert.Equal("2M1I1D1M1S", softclippedCigar.ToString()); // If allow shortening of softclip if bases match
            //Assert.Equal("2M1I1D2S", softclippedCigar.ToString()); // If mask whole original S that became M, regardless of matchiness
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoID_suffix.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("2M1I1D2S", softclippedCigar.ToString());

            // Original cigar = 2M3S, Realignment adds I overlapping S region -> Shortening of softclip due to I
            //  Ref:    AAAAA
            //  Alt:    GATTT
            mismatchMap = new[]
            { MatchType.Unmapped, MatchType.Match, MatchType.Mismatch, MatchType.Mismatch, MatchType.Mismatch };
            rawRealignedCigar_StoID_suffix = new CigarAlignment("2M1I1D2M");
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3);
            Assert.Equal("2M1I1D2S", softclippedCigar.ToString());
            // Remask Ns Only
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 0);
            Assert.Equal(rawRealignedCigar_StoID_suffix.ToString(), softclippedCigar.ToString());
            // Remask Ns Only - pretend we had Ns where orig softclip was
            softclippedCigar = Helper.SoftclipCigar(rawRealignedCigar_StoID_suffix, mismatchMap, 0, 3, maskNsOnly: true, prefixNs: 0, suffixNs: 3);
            Assert.Equal("2M1I1D2S", softclippedCigar.ToString());

            // Real example
            mismatchMap =
                CreateMatchTypeArray(new List<Tuple<int, MatchType>>()
                {
                    new Tuple<int, MatchType>(1, MatchType.NMismatch),
                    new Tuple<int, MatchType>(100, MatchType.Match),
                });
            softclippedCigar = Helper.SoftclipCigar(new CigarAlignment("90M18D11M"), mismatchMap, 5, 14, maskNsOnly: true,
                prefixNs: 1, suffixNs: 0);
            Assert.Equal("1S89M18D11M", softclippedCigar.ToString());

            mismatchMap =
                CreateMatchTypeArray(new List<Tuple<int, MatchType>>()
                {
                    new Tuple<int, MatchType>(100, MatchType.Match),
                    new Tuple<int, MatchType>(1, MatchType.NMismatch)
                });
            softclippedCigar = Helper.SoftclipCigar(new CigarAlignment("96M18D5M"), mismatchMap, 0, 8, maskNsOnly: true,
                prefixNs: 0, suffixNs: 1);
            Assert.Equal("96M18D4M1S", softclippedCigar.ToString());
        }

        private MatchType[] CreateMatchTypeArray(List<Tuple<int, MatchType>> sequence)
        {
            var map = new List<MatchType>();

            foreach (var item in sequence)
            {
                for (var i = 0; i < item.Item1; i++)
                {
                    map.Add(item.Item2);
                }
            }

            return map.ToArray();
        }

        [Fact]
        public void ConstructCigar()
        {
            // happy path
            var positionMap = new[] { 4,5,6,7,8 };
            Assert.Equal("5M", Helper.ConstructCigar(positionMap).ToString());
            Assert.Equal("5M", Helper.ConstructCigar(positionMap, true).ToString());

            // insertions
            positionMap = new[] { 4, 5, -1, -1, 6 };
            Assert.Equal("2M2I1M", Helper.ConstructCigar(positionMap).ToString());
            Assert.Equal("2M2I1M", Helper.ConstructCigar(positionMap, true).ToString());

            // insertions at end
            positionMap = new[] { -1, 5, 6, -1, 7, -1 };
            Assert.Equal("1I2M1I1M1I", Helper.ConstructCigar(positionMap).ToString());
            Assert.Equal("1S2M1I1M1S", Helper.ConstructCigar(positionMap, true).ToString());

            // deletions
            positionMap = new[] { 4, 5, 8, 10, 11 };
            Assert.Equal("2M2D1M1D2M", Helper.ConstructCigar(positionMap).ToString());
            Assert.Equal("2M2D1M1D2M", Helper.ConstructCigar(positionMap, true).ToString());

            // mixed
            positionMap = new[] { 4, -1, 8, 10, -1, -1 };
            Assert.Equal("1M1I3D1M1D1M2I", Helper.ConstructCigar(positionMap).ToString());
            Assert.Equal("1M1I3D1M1D1M2S", Helper.ConstructCigar(positionMap, true).ToString());
        }

        [Fact]
        public void GetMismatchMap()
        {
            //                 123456789012
            var refSequence = "AAATTTNNNGGG";

            // Happy path: should be able to distinguish matches, mismatches, and n-type mismatches
            // for both consecutive and gapped stretches. 
            CheckMismatchMap(new MatchType[] {MatchType.Match, MatchType.Match, MatchType.Match},
                Helper.GetMismatchMap("AAA", new int[] {1, 2, 3}, refSequence));
            CheckMismatchMap(new MatchType[] { MatchType.Match, MatchType.Mismatch, MatchType.Match },
                Helper.GetMismatchMap("ATA", new int[] { 1, 2, 3 }, refSequence));
            CheckMismatchMap(new MatchType[] { MatchType.Match, MatchType.NMismatch, MatchType.Match },
                Helper.GetMismatchMap("ANA", new int[] { 1, 2, 3 }, refSequence));
            CheckMismatchMap(new MatchType[] { MatchType.Mismatch, MatchType.Match, MatchType.NMismatch },
                Helper.GetMismatchMap("ATA", new int[] { 5, 6, 7 }, refSequence));
            CheckMismatchMap(new MatchType[] { MatchType.Match, MatchType.Match, MatchType.Match },
                Helper.GetMismatchMap("AGG", new int[] { 1, 11, 12 }, refSequence));

            // Should be able to reach to very end of chromosome (see bug in ZB-1535 and ZRTWO-2147).
            CheckMismatchMap(new MatchType[] { MatchType.Match, MatchType.Match, MatchType.Match },
                Helper.GetMismatchMap("GGG", new int[] { 10, 11, 12 }, refSequence));
            
            // Read off the edge of the chromosome: null
            Assert.Equal(null,
                Helper.GetMismatchMap("GGGG", new int[] { 10, 11, 12, 13 }, refSequence));
        }

        private void CheckMismatchMap(MatchType[] expectedMismatchMap, MatchType[] actualMismatchMap)
        {
            Assert.Equal(expectedMismatchMap.Length, actualMismatchMap.Length);

            for (int i = 0; i < expectedMismatchMap.Length; i++)
            {
                Assert.Equal(expectedMismatchMap[i], actualMismatchMap[i]);
            }
        }

        [Fact]
        public void IsValidMap()
        {
            var positionMap = new[] { 4, 5, 6, 7, 8 };
            Assert.True(Helper.IsValidMap(positionMap, "ACGTACGT"));
            Assert.False(Helper.IsValidMap(positionMap, "ACGTACG"));

            positionMap = new[] { -1, -1, -1 };
            Assert.False(Helper.IsValidMap(positionMap, "ACGTACG"));

            positionMap = new[] { 1 };
            Assert.True(Helper.IsValidMap(positionMap, "ACGTACG"));

            positionMap = new[] { -5 };
            Assert.False(Helper.IsValidMap(positionMap, "ACGTACG"));

            positionMap = new[] { 0 };
            Assert.False(Helper.IsValidMap(positionMap, "ACGTACG"));
        }

        [Fact]
        public void GetNumMismatches()
        {
            Assert.Equal(null, Helper.GetNumMismatches("ACGT", "ACGTA"));
            Assert.Equal(null, Helper.GetNumMismatches("ACGTA", "ACGT"));

            Assert.Equal(0, Helper.GetNumMismatches("ACGT", "ACGT"));
            Assert.Equal(1, Helper.GetNumMismatches("ACTT", "ACGT"));
            Assert.Equal(4, Helper.GetNumMismatches("CGTG", "ACGT"));

            // Ns shouldn't count as mismatches by default
            Assert.Equal(0, Helper.GetNumMismatches("NNNN","ACGT"));
            Assert.Equal(0, Helper.GetNumMismatches("ACGT", "NNNN"));
            Assert.Equal(1, Helper.GetNumMismatches("ACGT", "NNNA"));
            Assert.Equal(0, Helper.GetNumMismatches("NNNN", "NNNN"));

            // Ns should count as mismatches if we set ignoreNMismatches to false
            Assert.Equal(4, Helper.GetNumMismatches("NNNN", "ACGT", true));
            Assert.Equal(4, Helper.GetNumMismatches("ACGT", "NNNN", true));
            Assert.Equal(2, Helper.GetNumMismatches("ACGT", "ANNT", true));
            Assert.Equal(0, Helper.GetNumMismatches("NNNN", "NNNN", true));
        }

        [Fact]
        public void GetCharacterBookendLength()
        {
            Assert.Equal(0, Helper.GetCharacterBookendLength("AAAAA", 'N', false));
            Assert.Equal(0, Helper.GetCharacterBookendLength("AAAAA", 'N', true));

            Assert.Equal(1, Helper.GetCharacterBookendLength("NAAAA", 'N', false));
            Assert.Equal(0, Helper.GetCharacterBookendLength("NAAAA", 'N', true));
            Assert.Equal(2, Helper.GetCharacterBookendLength("NNAAA", 'N', false));
            Assert.Equal(0, Helper.GetCharacterBookendLength("NNAAA", 'N', true));

            Assert.Equal(0, Helper.GetCharacterBookendLength("AAAAN", 'N', false));
            Assert.Equal(1, Helper.GetCharacterBookendLength("AAAAN", 'N', true));
            Assert.Equal(0, Helper.GetCharacterBookendLength("AAANN", 'N', false));
            Assert.Equal(2, Helper.GetCharacterBookendLength("AAANN", 'N', true));

            Assert.Equal(0, Helper.GetCharacterBookendLength("AANAA", 'N', false));
            Assert.Equal(0, Helper.GetCharacterBookendLength("AANAA", 'N', true));
            Assert.Equal(0, Helper.GetCharacterBookendLength("ANANA", 'N', false));
            Assert.Equal(0, Helper.GetCharacterBookendLength("ANANA", 'N', true));
        }
    }
}
