using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Xunit;

namespace TestUtilities
{
    public static class ReadTestHelper
    {


        public static Read CreateRead(string chr, string sequence, int position,
            CigarAlignment cigar = null, byte[] qualities = null, int matePosition = 0, byte qualityForAll = 30)
        {
            return new Read(chr,
                new BamAlignment
                {
                    Bases = sequence,
                    Position = position - 1,
                    CigarData = cigar ?? new CigarAlignment(sequence.Length + "M"),
                    Qualities = qualities ?? Enumerable.Repeat(qualityForAll, sequence.Length).ToArray(),
                    MatePosition = matePosition - 1
                });
        }

        public static byte[] GetXCTagData(string value)
        {
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("XC", value);
            return tagUtils.ToBytes();
        }

        public static byte[] GetReadCountsTagData(int? x1, int? x2)
        {
            var tagUtils = new TagUtils();
            if (x1.HasValue)
                tagUtils.AddIntTag("X1", x1.Value);
            if (x2.HasValue)
                tagUtils.AddIntTag("X2", x2.Value);

            return tagUtils.ToBytes();
        }
    }
}
