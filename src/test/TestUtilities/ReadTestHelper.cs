using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace TestUtilities
{
    public static class ReadTestHelper
    {
        public static Tuple<Read, Read> CreateNonProperReadPair(string nameBase, int sequencelength, DirectionType type, string XRTag,
            string chrName = "chr1", int pos = 10, int matePos = 100, byte minBaseQuality = 30,
            string candidateBases = "ACGT")
        {
            Read read1 = null;
            Read read2 = null;
            switch (type)
            {
                case DirectionType.Forward:
                case DirectionType.Reverse:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped: true, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", XRTag);
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", XRTag);
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    break;
                case DirectionType.Stitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped: true, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", XRTag);
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", XRTag);
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    break;
                default:
                    throw new Exception("unknown type");
            }
            read1.IsDuplex = false;
            read2.IsDuplex = false;
            Assert.NotNull(read1);
            Assert.NotNull(read2);

            read1.BamAlignment.SetIsProperPair(true);
            read2.BamAlignment.SetIsProperPair(true);
            read1.BamAlignment.SetIsFirstMate(true);
            read1.BamAlignment.SetIsSecondMate(false);
            read2.BamAlignment.SetIsSecondMate(true);
            read2.BamAlignment.SetIsFirstMate(false);

            if (read1.Chromosome == read2.Chromosome)
            {
                // might not be same as pisces defintion
                read1.BamAlignment.FragmentLength =
                    Math.Abs(read1.Position - read2.Position);
                read2.BamAlignment.FragmentLength =
                    Math.Abs(read1.Position - read2.Position);
            }
            else
            {
                // FragmentLength be 0 if the reads are mapped to diff chrs
                read1.BamAlignment.FragmentLength = 0;
                read2.BamAlignment.FragmentLength = 0;
            }
            return new Tuple<Read, Read>(read1, read2);
        }

        public static Tuple<Read, Read> CreateProperReadPair(string nameBase, int sequencelength, ReadCollapsedType type,
            string chrName = "chr1", int pos = 10, int matePos = 100, byte minBaseQuality = 30, string candidateBases ="ACGT")
        {
            
            Read read1 = null;
            Read read2 = null;
            switch (type)
            {
                case ReadCollapsedType.SimplexForwardStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll:minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped:true, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "FR");
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "FR");
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    break;
                case ReadCollapsedType.SimplexForwardNonStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped:true, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{null}", "FR");
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{null}", "FR");
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    break;
                case ReadCollapsedType.SimplexReverseStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped: true, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "RF");
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "RF");
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(null, 10));
                    break;
                case ReadCollapsedType.SimplexReverseNonStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, isReverseMapped: true, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetReadCountsTagData(null, 10);
                    read1.BamAlignment.AppendTagData(GetXDXRTagData($"{null}", "RF"));
                    read2.BamAlignment.TagData = GetReadCountsTagData(null, 10);
                    read2.BamAlignment.AppendTagData(GetXDXRTagData($"{null}", "RF"));
                    break;
               case ReadCollapsedType.DuplexStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "RF");
                    read1.BamAlignment.AppendTagData(GetReadCountsTagData(1, 10));
                    read2.BamAlignment.TagData = GetXDXRTagData($"{sequencelength}S", "RF");
                    read2.BamAlignment.AppendTagData(GetReadCountsTagData(1, 10));
                    break;
                case ReadCollapsedType.DuplexNonStitched:
                    read1 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), pos, matePosition: matePos, qualityForAll: minBaseQuality);
                    read2 = CreateRead(chrName, RandomBases(sequencelength, candidateBases), matePos, matePosition: pos, qualityForAll: minBaseQuality);
                    read1.BamAlignment.TagData = GetReadCountsTagData(1, 10);
                    read2.BamAlignment.TagData = GetReadCountsTagData(1, 10);
                    break;
                default:
                    throw new Exception("unknown type");
            }
            Assert.NotNull(read1);
            Assert.NotNull(read2);

            read1.BamAlignment.SetIsProperPair(true);
            read2.BamAlignment.SetIsProperPair(true);
            read1.BamAlignment.SetIsFirstMate(true);
            read1.BamAlignment.SetIsSecondMate(false);
            read2.BamAlignment.SetIsSecondMate(true);
            read2.BamAlignment.SetIsFirstMate(false);

            if (read1.Chromosome == read2.Chromosome)
            {
                // might not be same as pisces defintion
                read1.BamAlignment.FragmentLength =
                    Math.Abs(read1.Position - read2.Position);
                read2.BamAlignment.FragmentLength =
                    Math.Abs(read1.Position - read2.Position);
            }
            else
            {
                // FragmentLength be 0 if the reads are mapped to diff chrs
                read1.BamAlignment.FragmentLength = 0;
                read2.BamAlignment.FragmentLength = 0;
            }
            return new Tuple<Read, Read>(read1, read2);
        }

        public static Read CreateRead(string chr, string sequence, int position,
            CigarAlignment cigar = null, byte[] qualities = null, int matePosition = 0, byte qualityForAll = 30, bool isReverseMapped = false, uint mapQ = 30)
        {
            var read = new Read(chr,
                new BamAlignment
                {
                    Bases = sequence,
                    Position = position - 1,
                    CigarData = cigar ?? new CigarAlignment(sequence.Length + "M"),
                    Qualities = qualities ?? Enumerable.Repeat(qualityForAll, sequence.Length).ToArray(),
                    MatePosition = matePosition - 1,
                });
            read.BamAlignment.MapQuality = mapQ;

            read.BamAlignment.SetIsReverseStrand(isReverseMapped);
            read.BamAlignment.SetIsMateReverseStrand(!isReverseMapped);
            return read;
        }
     
        public static byte[] GetXCTagData(string value)
        {
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("XC", value);
            return tagUtils.ToBytes();
        }

        public static byte[] GetXDXRTagData(string xd, string xr)
        {
            var tagUtils = new TagUtils();
            if(!string.IsNullOrEmpty(xd))
                tagUtils.AddStringTag("XD", xd);
            if(!string.IsNullOrEmpty(xr))
            tagUtils.AddStringTag("XR", xr);
            return tagUtils.ToBytes();
        }

        public static byte[] GetReadCountsTagData(int? xw, int? xv)
        {
            var tagUtils = new TagUtils();
            if (xw.HasValue)
                tagUtils.AddIntTag("XW", xw.Value);
            if (xv.HasValue)
                tagUtils.AddIntTag("XV", xv.Value);
            return tagUtils.ToBytes();
        }

        /// <summary>
        /// Returns a string of length psuedo random bases. Note that seeding is used such that output is always consistent
        /// </summary>
        /// <param name="length"></param>
        /// <param name="candidateBases"></param>
        /// <returns></returns>
        public static string RandomBases(int length, string candidateBases)
        {
            // source: https://stackoverflow.com/questions/1344221/how-can-i-generate-random-alphanumeric-strings-in-c
            Random rng = new Random(1337);  
            string retval = new string(Enumerable.Repeat(candidateBases, length).Select(s => s[rng.Next(s.Length)]).ToArray());
            return retval;
        }
    }
}
