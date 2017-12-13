using System;
using System.Collections.Generic;
using Alignment.Domain.Sequencing;
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
            Assert.Equal("5M3D", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 5).ToString());

            // skip end dels if specified (but not internal dels)
            Assert.Equal("7M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 7, true, false).ToString());
            Assert.Equal("7M", CigarExtensions.GetTrimmed(new CigarAlignment("4M1D1M3D7M"), 7, true, false).ToString());
            Assert.Equal("4M1D3M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D4M1D3M"), 7, true, false).ToString());
            Assert.Equal("5M3D2M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 7, false, false).ToString());
            Assert.Equal("5M", CigarExtensions.GetTrimmed(new CigarAlignment("5M3D7M"), 5, false, false).ToString());
        }

        [Fact]
        public void GetClippedCigar()
        {
            // from middle
            Assert.Equal("3M", CigarExtensions.GetClippedCigar(new CigarAlignment("3S6M"), 3, 6).ToString());
            Assert.Equal("3M", CigarExtensions.GetClippedCigar(new CigarAlignment("3S3D6M"), 3, 6, false).ToString());
            Assert.Equal("3M", CigarExtensions.GetClippedCigar(new CigarAlignment("3S3M3D4M"), 3, 6, false).ToString());
            Assert.Equal("3M3D", CigarExtensions.GetClippedCigar(new CigarAlignment("3S3M3D4M"), 3, 6).ToString());
            Assert.Equal("3D3M", CigarExtensions.GetClippedCigar(new CigarAlignment("3S3D4M"), 3, 6).ToString());
            Assert.Equal("3D3M", CigarExtensions.GetClippedCigar(new CigarAlignment("1S1D2S3D4M"), 3, 6).ToString());

            // readcycles < 0
            Assert.Equal("", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 0).ToString());
            Assert.Equal("", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 0).ToString());
            Assert.Equal("", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), -1, -1).ToString());

            //readcycles > 0
            Assert.Equal("1M", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 1).ToString());
            Assert.Equal("5M3D2M", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 7).ToString());
            Assert.Equal("5M3D", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 5).ToString());

            // skip end dels if specified (but not internal dels)
            Assert.Equal("5M3D2M", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0,7, false).ToString());
            Assert.Equal("5M", CigarExtensions.GetClippedCigar(new CigarAlignment("5M3D7M"), 0, 5, false).ToString());

            //softclips
            Assert.Equal("2M1S", CigarExtensions.GetClippedCigar(new CigarAlignment("2M1S"), 0, 3).ToString());
            Assert.Equal("2M", CigarExtensions.GetClippedCigar(new CigarAlignment("2M1S"), 0, 2).ToString());

        }

        [Fact]
        public void GetReadSpanBetweenClippedEnds()
        {
            Assert.Equal((uint) 7, new CigarAlignment("5M2D2M").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint) 9, new CigarAlignment("1S5M2I2M2S").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint)9, new CigarAlignment("1S5M2I2M").GetReadSpanBetweenClippedEnds());
            Assert.Equal((uint)0, new CigarAlignment("1S3D2S").GetReadSpanBetweenClippedEnds());
        }

        [Fact]
        public void Expand()
        {
            var cigar = new CigarAlignment("2S3M1D1M");

            var expectedExpansion = new List<char>() {'S', 'S', 'M', 'M', 'M', 'D', 'M'};
            var actualExpansion = cigar.Expand();

            Assert.Equal(expectedExpansion.Count, actualExpansion.Count);

            for (var i = 0; i < expectedExpansion.Count; i++)
            {
                Assert.Equal(expectedExpansion[i], actualExpansion[i].Type);
            }
        }

        [Fact]
        public void Expander()
        {
            var cigar = new CigarAlignment("2S3M1D1M");

            var expectedExpansion = new List<char>() { 'S', 'S', 'M', 'M', 'M', 'D', 'M' };

            int i = 0;
            for (var expander = new CigarExtensions.CigarOpExpander(cigar); expander.IsNotEnd(); expander.MoveNext())
            {
                Assert.Equal(expectedExpansion[i], expander.Current);
                ++i;
            }
        }

        [Fact]
        public void GetSubCigar()
        {
            var cigar = new CigarAlignment("2S3M1D1M");

            var subCigar = cigar.GetSubCigar(0, cigar.Count - 1);
            Assert.Equal("2S3M1D", subCigar.ToString());
            subCigar = cigar.GetSubCigar(0, cigar.Count);
            Assert.Equal("2S3M1D1M", subCigar.ToString());

            subCigar = cigar.GetSubCigar(1, cigar.Count);
            Assert.Equal("3M1D1M", subCigar.ToString());

            subCigar = cigar.GetSubCigar(1, cigar.Count - 1);
            Assert.Equal("3M1D", subCigar.ToString());

            subCigar = cigar.GetSubCigar(2, 2);
            Assert.Equal("", subCigar.ToString());


            Assert.Throws<ArgumentException>(() => cigar.GetSubCigar(3, 2));
        }

        [Fact]
        public void HasInternalSoftclip()
        {
            var cigar = new CigarAlignment("2S3M2S");
            Assert.False(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("2S3M1S1M1S");
            Assert.True(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("3M1S1M");
            Assert.True(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("3M1D2S1M");
            Assert.True(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("1S3M1D1S1M");
            Assert.True(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("3M1I1M");
            Assert.False(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("3M1D2I1M");
            Assert.False(cigar.HasInternalSoftclip());

            cigar = new CigarAlignment("1S3M1D1I1M");
            Assert.False(cigar.HasInternalSoftclip());

        }

        [Fact]
        public void GetCigarWithoutProbeClips()
        {
            var cigar = new CigarAlignment("1S3M");
            Assert.Equal("3M", cigar.GetCigarWithoutProbeClips(true).ToString());
            Assert.Equal("1S3M", cigar.GetCigarWithoutProbeClips(false).ToString());

            cigar = new CigarAlignment("1S3M1D");
            Assert.Equal("3M1D", cigar.GetCigarWithoutProbeClips(true).ToString());
            Assert.Equal("1S3M1D", cigar.GetCigarWithoutProbeClips(false).ToString());

            cigar = new CigarAlignment("3M1S");
            Assert.Equal("3M1S", cigar.GetCigarWithoutProbeClips(true).ToString());
            Assert.Equal("3M", cigar.GetCigarWithoutProbeClips(false).ToString());

            cigar = new CigarAlignment("1D3M1S");
            Assert.Equal("1D3M1S", cigar.GetCigarWithoutProbeClips(true).ToString());
            Assert.Equal("1D3M", cigar.GetCigarWithoutProbeClips(false).ToString());

            cigar = new CigarAlignment("1S3M1S");
            Assert.Equal("3M1S", cigar.GetCigarWithoutProbeClips(true).ToString());
            Assert.Equal("1S3M", cigar.GetCigarWithoutProbeClips(false).ToString());

        }
    }
}