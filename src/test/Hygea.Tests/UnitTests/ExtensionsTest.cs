using System;
using System.Linq;
using RealignIndels.Utlity;
using Pisces.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class ExtensionsTest
    {
        [Fact]
        public void AnchorLength()
        {
            var chrReference = string.Join(string.Empty, Enumerable.Repeat("AAAA", 10));

            var read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AAATAA",
                CigarData = new CigarAlignment("6M")
            });

            // no mismatches or indels
            var results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(1, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(2, results.AnchorLength);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("6M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(2, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(2, results.AnchorLength);

        }

        [Fact]
        public void NumIndelBases()
        {
            var chrReference = string.Join(string.Empty, Enumerable.Repeat("AAAA", 10));

            var read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AAATAA",
                CigarData = new CigarAlignment("6M")
            });

            // no mismatches or indels
            var results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumIndelBases);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("2M2I2M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(2, results.NumIndelBases);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("1M1I1M1I2M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(2, results.NumIndelBases);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("2M2I2D2M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(4, results.NumIndelBases);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("1M1I1M2D1I2M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(4, results.NumIndelBases);

            read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "AATTAA",
                CigarData = new CigarAlignment("2M2D2M2D2M")
            });

            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(4, results.NumIndelBases);


        }

        [Fact]
        public void ReadMismatchCount()
        {
            var chrReference = string.Join(string.Empty, Enumerable.Repeat("ACGT", 10));

            var read = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "ACGTACGT",
                CigarData = new CigarAlignment("8M")
            });

            // no mismatches or indels
            var results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(read.ReadLength, results.AnchorLength);

            read.BamAlignment.Position = 4;
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(read.ReadLength, results.AnchorLength);

            // all mismatches, no indels
            read.BamAlignment.Position = 1;
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(8, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(0, results.AnchorLength);

            // one indel, no mismatches
            read.BamAlignment.Position = 2;
            read.BamAlignment.CigarData = new CigarAlignment("8I");
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(1, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(0, results.AnchorLength);

            read.BamAlignment.CigarData = new CigarAlignment("2D8M");
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(1, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(0, results.AnchorLength);

            // one indel, some mismatches
            read.BamAlignment.CigarData = new CigarAlignment("4M3I1M");
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(5, results.NumMismatches);
            Assert.Equal(1, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);
            Assert.Equal(0, results.AnchorLength);


            read.BamAlignment.CigarData = new CigarAlignment("2M2D2M1D4M");
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(6, results.NumMismatches);
            Assert.Equal(2, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);

            // multiple indels
            read.BamAlignment.Position = 4;
            read.BamAlignment.CigarData = new CigarAlignment("1M3D2M1I2M2D2M");
            results = read.GetAlignmentSummary(chrReference);
            Assert.Equal(4, results.NumMismatches);
            Assert.Equal(3, results.NumIndels);
            Assert.Equal(0, results.NumSoftclips);

            var readWithNSoftclips = new Read("chr1", new BamAlignment
            {
                Position = 0,
                Bases = "NCGTACNN",
                CigarData = new CigarAlignment("8M")
            });

            // softclips and indels
            readWithNSoftclips.BamAlignment.Position = 2;
            readWithNSoftclips.BamAlignment.CigarData = new CigarAlignment("3S5I");
            results = readWithNSoftclips.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(1, results.NumIndels);
            Assert.Equal(3, results.NumSoftclips);
            Assert.Equal(2, results.NumNonNSoftclips);

            readWithNSoftclips.BamAlignment.Position = 2;
            readWithNSoftclips.BamAlignment.CigarData = new CigarAlignment("7I1S");
            results = readWithNSoftclips.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(1, results.NumIndels);
            Assert.Equal(1, results.NumSoftclips);
            Assert.Equal(0, results.NumNonNSoftclips);

            // softclips and matches
            readWithNSoftclips.BamAlignment.Position = 5;
            readWithNSoftclips.BamAlignment.CigarData = new CigarAlignment("1S7M");
            results = readWithNSoftclips.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(1, results.NumSoftclips);
            Assert.Equal(0, results.NumNonNSoftclips);

            readWithNSoftclips.BamAlignment.Position = 4;
            readWithNSoftclips.BamAlignment.CigarData = new CigarAlignment("6M2S");
            results = readWithNSoftclips.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(2, results.NumSoftclips);
            Assert.Equal(0, results.NumNonNSoftclips);

            readWithNSoftclips.BamAlignment.Position = 4;
            readWithNSoftclips.BamAlignment.CigarData = new CigarAlignment("5M3S");
            results = readWithNSoftclips.GetAlignmentSummary(chrReference);
            Assert.Equal(0, results.NumMismatches);
            Assert.Equal(0, results.NumIndels);
            Assert.Equal(3, results.NumSoftclips);
            Assert.Equal(1, results.NumNonNSoftclips);

        }

        [Fact]
        public void GetAdjustedPosition()
        {
            // all matches
            var read = GetTestRead("10M");

            Assert.Equal(100, read.GetAdjustedPosition(true));

            // with soft clipping
            read = GetTestRead("5S3M2S");
            Assert.Equal(95, read.GetAdjustedPosition(true));

            // Simple case with softclips (N and non-N), anchor from right
            read = GetTestRead("3M3S");
            Assert.Equal(100, read.GetAdjustedPosition(false));

            read = GetTestRead("2S3M3S");
            Assert.Equal(98, read.GetAdjustedPosition(false));

            read = GetTestRead("2S3M3S", 2);
            Assert.Equal(100, read.GetAdjustedPosition(false));

            read = GetTestRead("2S3M3S", 0, 3);
            Assert.Equal(98, read.GetAdjustedPosition(false));

            read = GetTestRead("2S3M3S", 2, 3);
            Assert.Equal(100, read.GetAdjustedPosition(false));

            // with deletion, anchor right
            read = GetTestRead("2M4D15M");
            Assert.Equal(104, read.GetAdjustedPosition(false));

            read = GetTestRead("3S2M4D15M");
            Assert.Equal(101, read.GetAdjustedPosition(false));

            read = GetTestRead("3S4D1M14S");
            Assert.Equal(101, read.GetAdjustedPosition(false));

            read = GetTestRead("3S4D1M14S", 2);
            Assert.Equal(103, read.GetAdjustedPosition(false));

            read = GetTestRead("3S1M4D1M4S", 2, 3);
            Assert.Equal(103, read.GetAdjustedPosition(false));

            // with insertion, anchor right
            read = GetTestRead("2M4I15M");
            Assert.Equal(96, read.GetAdjustedPosition(false));

            read = GetTestRead("3S2M4I15M");
            Assert.Equal(93, read.GetAdjustedPosition(false));

            read = GetTestRead("3S4I2M15S");
            Assert.Equal(93, read.GetAdjustedPosition(false));

            read = GetTestRead("3S4I2M15S", 2);
            Assert.Equal(95, read.GetAdjustedPosition(false));

            read = GetTestRead("3S1M4I2M5S", 2, 3);
            Assert.Equal(95, read.GetAdjustedPosition(false));

            // with deletion, anchor left
            read = GetTestRead("12M4D5M");
            Assert.Equal(100, read.GetAdjustedPosition(true));

            read = GetTestRead("10S2M4D5M");
            Assert.Equal(90, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4D5M1S");
            Assert.Equal(90, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4D5M1S", 6);
            Assert.Equal(96, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4D5M1S", 6, 1);
            Assert.Equal(96, read.GetAdjustedPosition(true));

            // with insertion, anchor left
            read = GetTestRead("12M4I5M");
            Assert.Equal(100, read.GetAdjustedPosition(true));

            read = GetTestRead("10S2M4I5M");
            Assert.Equal(90, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4I5M1S");
            Assert.Equal(86, read.GetAdjustedPosition(true));

            read = GetTestRead("4I5M1S");
            Assert.Equal(96, read.GetAdjustedPosition(true));

            read = GetTestRead("10S2M4I5M", 6);
            Assert.Equal(96, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4I5M1S", 6);
            Assert.Equal(92, read.GetAdjustedPosition(true));

            read = GetTestRead("10S4I5M1S", 6, 1);
            Assert.Equal(92, read.GetAdjustedPosition(true));
        }

        private Read GetTestRead(string cigarString, int prefixNs = 0, int suffixNs = 0)
        {
            var cigarData = new CigarAlignment(cigarString);

            return new Read("chr1", new BamAlignment
            {
                Position = 99,  // zero index for bam alignment
                CigarData = cigarData,
                Bases = string.Join(string.Empty, Enumerable.Repeat("N", prefixNs).Concat(Enumerable.Repeat("A", (int)cigarData.GetReadSpan() - prefixNs - suffixNs)).Concat(Enumerable.Repeat("N", suffixNs)))
            });
        }

        private Read GetReadWithSequence(string cigarString, string sequence)
        {
            var cigarData = new CigarAlignment(cigarString);

            return new Read("chr1", new BamAlignment
            {
                Position = 99,  // zero index for bam alignment
                CigarData = cigarData,
                Bases = sequence
            });

        }

        [Fact]
        public void GetNPrefix()
        {
            var read = GetReadWithSequence("5S2M2S", "NNAAATTAA");
            Assert.Equal(2,read.GetNPrefix());

            read = GetReadWithSequence("5S2M2S", "AAAAATTAA");
            Assert.Equal(0, read.GetNPrefix());

            read = GetReadWithSequence("5S2M2S", "NNNNNTTAA");
            Assert.Equal(5, read.GetNPrefix());

            // Terminal only
            read = GetReadWithSequence("5S2M2S", "AANNNTTAA");
            Assert.Equal(0, read.GetNPrefix());

            // Doesn't actually care about cigar
            read = GetReadWithSequence("5S2M2S", "NNNNNNTAA");
            Assert.Equal(6, read.GetNPrefix());
        }

        [Fact]
        public void GetNSuffix()
        {
            var read = GetReadWithSequence("2S2M5S", "AATTAAANN");
            Assert.Equal(2, read.GetNSuffix());

            read = GetReadWithSequence("2S2M5S", "AATTAAAAA");
            Assert.Equal(0, read.GetNSuffix());

            read = GetReadWithSequence("2S2M5S", "AATTNNNNN");
            Assert.Equal(5, read.GetNSuffix());

            // Terminal only
            read = GetReadWithSequence("2S2M5S", "AATTANNAA");
            Assert.Equal(0, read.GetNSuffix());

            // Doesn't actually care about cigar
            read = GetReadWithSequence("2S2M5S", "AATNNNNNN");
            Assert.Equal(6, read.GetNSuffix());

        }

        [Fact]
        public void GetAlignmentResult()
        {
            var read = new Read("chr1",
                new BamAlignment { Bases = "ACGTACGTACGT", CigarData = new CigarAlignment("12M") });

            var result = read.GetAlignmentSummary("ACCCACGTACGT");
            Assert.Equal(0, result.NumIndels);
            Assert.Equal(2, result.NumMismatches);

            read.BamAlignment.CigarData = new CigarAlignment("1S4M5D1I3M2I1S");
            result = read.GetAlignmentSummary("CCCCACGTACGTTCCCACGTACGT");
            Assert.Equal(3, result.NumIndels);
            Assert.Equal(6, result.NumMismatches);
        }
    }
}
