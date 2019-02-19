using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;

namespace Gemini.Tests
{
    public static class TestHelpers
    {
        public static Read CreateRead(string chr, string sequence, int position,
            CigarAlignment cigar = null, byte[] qualities = null, int matePosition = 0, byte qualityForAll = 30, bool isReverseMapped = false, uint mapQ = 30)
        {
            var bamAlignment = CreateBamAlignment(sequence, position, matePosition, qualityForAll, isReverseMapped, mapQ, qualities, cigar);

            var read = new Read(chr, bamAlignment);
            
            return read;
        }

        public static BamAlignment CreateBamAlignment(string sequence, int position, 
            int matePosition, byte qualityForAll, bool isReverseMapped, uint mapQ = 30, byte[] qualities = null, CigarAlignment cigar = null, string name = null, bool isFirstMate = true)
        {
            var bamAlignment = new BamAlignment
            {
                Bases = sequence,
                Position = position - 1,
                CigarData = cigar ?? new CigarAlignment(sequence.Length + "M"),
                Qualities = qualities ?? Enumerable.Repeat(qualityForAll, sequence.Length).ToArray(),
                MatePosition = matePosition - 1,
                TagData = new byte[0],
                RefID = 1,
                MateRefID = 1,
                Name = name ?? "Alignment"
            };
            bamAlignment.SetIsFirstMate(isFirstMate);
            bamAlignment.MapQuality = mapQ;
            bamAlignment.SetIsReverseStrand(isReverseMapped);
            bamAlignment.SetIsMateReverseStrand(!isReverseMapped);
            return bamAlignment;
        }

        public static ReadPair GetPair(string cigar1, string cigar2, uint mapq1 = 30, uint mapq2 = 30, PairStatus pairStatus = PairStatus.Paired, bool singleReadOnly = false, int nm = 0, int read2Offset = 0, int? nm2 = null, string name = null)
        {
            int nmRead2 = nm2 ?? nm;

            var tagUtils = new TagUtils();
            if (nm >= 0)
            {
                tagUtils.AddIntTag("NM", nm);
            }

            var cigarAln1 = new CigarAlignment(cigar1);

            var qualities1 = new List<byte>();
            for (int i = 0; i < cigarAln1.GetReadSpan(); i++)
            {
                qualities1.Add(30);
            }

            var basesRaw = "AAAGTTTTCCCCCCCCCCCC";
            var alignment = new BamAlignment
            {
                Name = name ?? "hi:1:2:3:4:5:6",
                RefID = 1,
                Position = 99,
                Bases = basesRaw.Substring(0, (int)cigarAln1.GetReadSpan()),
                CigarData = cigarAln1,
                Qualities = qualities1.ToArray(),
                MapQuality = mapq1
            };
            alignment.TagData = tagUtils.ToBytes();
            var pair = new ReadPair(alignment);

            if (!singleReadOnly)
            {
                var tagUtils2 = new TagUtils();
                if (nmRead2 >= 0)
                {
                    tagUtils2.AddIntTag("NM", nmRead2);
                }

                var cigarAln2 = new CigarAlignment(cigar2);

                var qualities2 = new List<byte>();
                for (int i = 0; i < cigarAln2.GetReadSpan(); i++)
                {
                    qualities2.Add(30);
                }

                var alignment2 = new BamAlignment
                {
                    Name = "hi:1:2:3:4:5:6",
                    RefID = 1,
                    Position = 99 + read2Offset,
                    Bases = basesRaw.Substring(0, (int)cigarAln2.GetReadSpan()),
                    CigarData = cigarAln2,
                    Qualities = qualities2.ToArray(),
                    MapQuality = mapq2
                };
                alignment2.SetIsSecondMate(true);
                alignment2.SetIsReverseStrand(true);
                alignment2.TagData = tagUtils2.ToBytes();
                pair.AddAlignment(alignment2);
            }


            pair.PairStatus = pairStatus;
            return pair;

        }
    }
}