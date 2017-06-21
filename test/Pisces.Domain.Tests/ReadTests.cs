using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using TestUtilities;
using Xunit;

namespace Pisces.Domain.Tests
{
    public static class ReadTests
    {

 
      
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

            TestHelper.VerifyArray(read1.PositionMap, read2.PositionMap);
            TestHelper.VerifyArray(read1.SequencedBaseDirectionMap, read2.SequencedBaseDirectionMap);
            TestHelper.VerifyArray(read1.Qualities, read2.Qualities);
        }
       
    }
}
