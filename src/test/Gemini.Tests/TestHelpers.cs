using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.Types;
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
        
        public static ReadPair GetPair(string cigar1, string cigar2, uint mapq1 = 30, uint mapq2 = 30, PairStatus pairStatus = PairStatus.Paired, bool singleReadOnly = false, int nm = 0, int read2Offset = 0, int? nm2 = null, string name = null, string basesRaw = "AAAGTTTTCCCCCCCCCCCCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", int read1Position = 99, string read1Bases = null, string read2Bases = null)
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

            //var basesRaw = "AAAGTTTTCCCCCCCCCCCC";
            var alignment = new BamAlignment
            {
                Name = name ?? "hi:1:2:3:4:5:6",
                RefID = 1,
                Position = read1Position,
                Bases = read1Bases ?? basesRaw.Substring(0, (int)cigarAln1.GetReadSpan()),
                CigarData = cigarAln1,
                Qualities = qualities1.ToArray(),
                MapQuality = mapq1
            };
            alignment.TagData = tagUtils.ToBytes();
            if (!singleReadOnly)
            {
                alignment.SetIsProperPair(true);
                alignment.MateRefID = 1;
            }
            var pair = new ReadPair(alignment);

            if (!singleReadOnly)
            {
                alignment.SetIsMateUnmapped(false);
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
                    Position = read1Position + read2Offset,
                    Bases = read2Bases ?? basesRaw.Substring(read2Offset, (int)cigarAln2.GetReadSpan()),
                    CigarData = cigarAln2,
                    Qualities = qualities2.ToArray(),
                    MapQuality = mapq2
                };

                alignment2.MateRefID = pair.Read1.RefID;
                alignment2.SetIsProperPair(true);
                alignment2.SetIsSecondMate(true);
                alignment2.SetIsReverseStrand(true);
                alignment2.TagData = tagUtils2.ToBytes();
                pair.AddAlignment(alignment2);
            }


            pair.PairStatus = pairStatus;
            return pair;

        }

        public static PairResult GetPairResult(int position, int offset = 0, 
            string r1Cigar = "5M1I5M", string r2Cigar = "5M1I5M", 
            PairClassification classification = PairClassification.Unknown, 
            int numMismatchesInSingleton = 0, int softclipLength = 0, bool hasIndels = false, bool isReputableIndelContaining = false)
        {
            //var read1 = TestHelpers.CreateBamAlignment("ATCGATCG", 125005, 126005, 30, true);
            //var read2 = TestHelpers.CreateBamAlignment("ATCGATCG", 126005, 125005, 30, true);

            //var pair2 = new ReadPair(read1);
            //pair2.AddAlignment(read2);

            var readPair1 = TestHelpers.GetPair(r1Cigar, r2Cigar, read2Offset: offset, read1Position: position);
            var pairResult2 = new PairResult(readPair1.GetAlignments(), readPair1, classification: classification, numMismatchesInSingleton: numMismatchesInSingleton, softclipLengthForIndelRead: softclipLength, hasIndels: hasIndels);
            pairResult2.IsReputableIndelContaining = isReputableIndelContaining;
            return pairResult2;
        }
    }
}