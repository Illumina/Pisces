using System.Linq;
using SequencingFiles;
using Pisces.Domain.Models;
using Xunit;

namespace Pisces.Domain.Tests
{
    public static class DomainTestHelper
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

        public static void CompareReads(Read read1, Read read2)
        {
            Assert.Equal(read1.Chromosome, read2.Chromosome);
            Assert.Equal(read1.Sequence, read2.Sequence);
            Assert.Equal(read1.Position, read2.Position);
            Assert.Equal(read1.Name, read2.Name);
            Assert.Equal(read1.MatePosition, read2.MatePosition);
            Assert.Equal(read1.IsMapped, read2.IsMapped);
            Assert.Equal(read1.IsPcrDuplicate, read2.IsPcrDuplicate);
            Assert.Equal(read1.IsPrimaryAlignment, read2.IsPrimaryAlignment);
            Assert.Equal(read1.IsProperPair, read2.IsProperPair);
            Assert.Equal(read1.MapQuality, read2.MapQuality);

            Assert.Equal(((Read)read1).StitchedCigar == null ? null: ((Read)read1).StitchedCigar.ToString(),
                ((Read)read2).StitchedCigar == null ? null : ((Read)read2).StitchedCigar.ToString());
            Assert.Equal(((Read)read1).CigarData == null ? null : ((Read)read1).CigarData.ToString(),
                            ((Read)read2).CigarData == null ? null : ((Read)read2).CigarData.ToString());

            VerifyArray(read1.PositionMap, read2.PositionMap);
            VerifyArray(read1.DirectionMap, read2.DirectionMap);
            VerifyArray(read1.Qualities, read2.Qualities);
        }

        public static void VerifyArray<T>(T[] array1, T[] array2)
        {
            if (array1 == null || array2 == null)
                Assert.Equal(array1, array2);
            else
            {
                Assert.Equal(array1.Length, array2.Length);
                for (var i = 0; i < array1.Length; i ++)
                    Assert.Equal(array1[i], array2[i]);
            }
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
