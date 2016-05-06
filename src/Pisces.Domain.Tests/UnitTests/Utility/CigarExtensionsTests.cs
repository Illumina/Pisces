using SequencingFiles;
using Pisces.Domain.Utility;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Utility
{
    public class CigarExtensionsTests
    {
        [Fact]
        public void ValidateCigarAignment()
        {
            //valid cigar string
            Assert.True(new CigarAlignment("5M3D4M7I2S8M").IsSupported());
            //invalid cigar string
            var alignment = new CigarAlignment();
            alignment.Add(new CigarOp('M', 5));
            alignment.Add(new CigarOp('U', 7));
            alignment.Add(new CigarOp('I', 3));
            alignment.Add(new CigarOp('M', 7));
            Assert.False(alignment.IsSupported());
        }

        [Fact]
        public void ReverseCigarAignment()
        {
            Assert.Equal("8M2S7I4M3D5M", new CigarAlignment("5M3D4M7I2S8M").GetReverse().ToString());
            Assert.Equal("5M", new CigarAlignment("5M").GetReverse().ToString());
        }

        [Fact]
        public void HasOperationAtOpIndex()
        {
            var alignment = new CigarAlignment("5M3D4M7I2S8M");
            
            Assert.True(alignment.HasOperationAtOpIndex(3,'I'));
            Assert.False(alignment.HasOperationAtOpIndex(5,'D'));
            Assert.False(CigarExtensions.HasOperationAtOpIndex(null, 3, 'D'));
            Assert.False(CigarExtensions.HasOperationAtOpIndex(null, 3, 'D', true));
            Assert.True(alignment.HasOperationAtOpIndex(1, 'S', true));
            Assert.False(alignment.HasOperationAtOpIndex(2, 'D', true));
            Assert.False(alignment.HasOperationAtOpIndex(-1, 'D', true));
            Assert.False(alignment.HasOperationAtOpIndex(8, 'D', true));
        }

        [Fact]
        public void GetTrimmed()
        {
            //readcycles < 0
            Assert.Equal("", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 0).ToString());
            Assert.Equal("", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 0, true).ToString());
            Assert.Equal("", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), -1, true).ToString());

            //readcycles > 0
            Assert.Equal("1M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 1).ToString());
            Assert.Equal("1M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 1, true).ToString());
            Assert.Equal("5M3D2M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 7).ToString());
            Assert.Equal("3D7M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 7, true).ToString());
        }

        [Fact]
        public void GetReadSpanBetweenClippedEnds()
        {
            Assert.Equal((uint) 7, new CigarAlignment("5M2D2M").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint) 9, new CigarAlignment("1S5M2I2M2S").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint)9, new CigarAlignment("1S5M2I2M").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint)0, new CigarAlignment("1S3D2S").GetReadSpanBetweenClippedEnds());
        }
    }
}