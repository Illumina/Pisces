using System;
using CallSomaticVariants.Logic.Alignment;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Models
{
    public class ReadTests
    {
        [Fact]
        public void Constructor()
        {
            var chr = "chr4";
            var sequence = "ACTCTAAAAA";
            var position = 134345;

            var read = new Read(chr, new BamAlignment() { Bases = sequence, Position = position});

            Assert.Equal(chr, read.Chromosome);
            Assert.Equal(sequence, read.Sequence);
            Assert.Equal(position + 1, read.Position);
            Assert.Equal(10, read.ReadLength);
            Assert.Equal(read.PositionMap.Length, read.ReadLength);

            for (var i = 0; i < read.PositionMap.Length; i ++)
            {
                Assert.Equal(-1, read.PositionMap[i]);  // default for no cigar is -1
            }
        }

        [Fact]
        public void FromBam()
        {
            var alignment = new BamAlignment
            {
                Bases = "ATCTTA",
                Position = 100,
                MatePosition = 500,
                Name = "test",
                CigarData = new CigarAlignment("5M1S"),
                MapQuality = 10,
                Qualities = new[] { (byte)10, (byte)20, (byte)30 }
            };

            alignment.SetIsDuplicate(true);
            alignment.SetIsProperPair(true);
            alignment.SetIsSecondaryAlignment(true);
            alignment.SetIsUnmapped(true);

            var read = new Read("chr1", alignment);

            Assert.Equal(read.Chromosome, "chr1");
            Assert.Equal(read.Sequence, alignment.Bases);
            Assert.Equal(read.Position, alignment.Position + 1);
            Assert.Equal(read.MatePosition, alignment.MatePosition + 1);
            Assert.Equal(read.Name, alignment.Name);
            Assert.Equal(read.CigarData, alignment.CigarData);
            Assert.Equal(read.IsMapped, alignment.IsMapped());
            Assert.Equal(read.IsProperPair, alignment.IsProperPair());
            Assert.Equal(read.IsPrimaryAlignment, alignment.IsPrimaryAlignment());
            Assert.Equal(read.IsPcrDuplicate, alignment.IsDuplicate());

            foreach (var direction in read.DirectionMap)
                Assert.Equal(direction, DirectionType.Forward);

            for (var i = 0; i < read.Qualities.Length; i++)
                Assert.Equal(read.Qualities[i], alignment.Qualities[i]);
        }

        [Fact]
        public void DeepCopy()
        {
            var alignment = new BamAlignment
            {
                Bases = "ACTC",
                Position = 5,
                MapQuality = 343,
                MatePosition = 12312,
                Qualities = new[] {(byte) 20, (byte) 21, (byte) 30, (byte) 40},
                CigarData = new CigarAlignment("1S3M")
            };
            alignment.SetIsUnmapped(false);
            alignment.SetIsSecondaryAlignment(false);
            alignment.SetIsDuplicate(true);
            alignment.SetIsProperPair(true);
                
            var read = new Read("chr1", alignment);
            read.StitchedCigar = new CigarAlignment("7M");
            read.DirectionMap = new[] {DirectionType.Forward, DirectionType.Reverse, DirectionType.Stitched, DirectionType.Reverse};
            var clonedRead = read.DeepCopy();

            TestHelper.CompareReads(read, clonedRead);

            // verify the arrays are deep copies
            read.PositionMap[0] = 1000;
            Assert.False(clonedRead.PositionMap[0] == 1000);
            read.DirectionMap[0] = DirectionType.Stitched;
            Assert.False(clonedRead.DirectionMap[0] == DirectionType.Stitched);
            read.Qualities[0] = 11;
            Assert.False(clonedRead.Qualities[0] == 11);

            read.CigarData.Reverse();
            Assert.False(clonedRead.CigarData.ToString() == read.CigarData.ToString());
        }

        [Fact]
        public void Reset()
        {
            var alignment = new BamAlignment
            {
                Bases = "ACTC",
                Position = 5,
                MapQuality = 343,
                MatePosition = 12312,
                Qualities = new[] { (byte)20, (byte)21, (byte)30, (byte)40 },
                CigarData = new CigarAlignment("1S3M")
            };
            alignment.SetIsUnmapped(false);
            alignment.SetIsSecondaryAlignment(false);
            alignment.SetIsDuplicate(true);
            alignment.SetIsProperPair(true);

            var read = new Read("chr1", alignment);
            read.StitchedCigar = new CigarAlignment("7M");
            read.DirectionMap = new[] { DirectionType.Forward, DirectionType.Reverse, DirectionType.Stitched, DirectionType.Reverse };
            
            alignment.SetIsDuplicate(false);
            alignment.MatePosition = 555;

            read.Reset("chr2", alignment, false);
            Assert.Equal(556, read.MatePosition);
            Assert.False(read.IsPcrDuplicate);
            Assert.Equal("chr2", read.Chromosome);

            var stitchedCigar = "1S3M1S";
            alignment.TagData = TestUtility.GetXCTagData(stitchedCigar);
            read.Reset("chr3", alignment, true);
            Assert.Equal(556, read.MatePosition);
            Assert.False(read.IsPcrDuplicate);
            Assert.Equal("chr3", read.Chromosome);
            Assert.Equal(stitchedCigar, read.StitchedCigar.ToString());

        }

        [Fact]
        public void CigarData()
        {
            var read = TestHelper.CreateRead("chr4", "ACCGACTAAC", 4, new CigarAlignment("10M"));

            Verify(new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, read.PositionMap);

            read = TestHelper.CreateRead("chr4", "ACCGACTAAC", 4, new CigarAlignment("2S1M4I5D2M1S"));
            Verify(new[] { -1, -1, 4, -1, -1, -1, -1, 10, 11, -1 }, read.PositionMap);

            read = TestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("12M"));

            for (var i = 0; i < read.PositionMap.Length; i++)
                Assert.Equal(read.PositionMap[i], read.Position + i);

            read = TestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("2S5M4I10D1M"));
            Verify(new []
            {
                -1, -1, 100, 101, 102, 103, 104, -1, -1, -1, -1, 115
            }, read.PositionMap);

            Assert.Throws<Exception>(() => TestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("100M")));
        }

        private void Verify(int[] expectedPositions, int[] actualPositions)
        {
            Assert.Equal(actualPositions.Length, expectedPositions.Length);

            for (var i = 0; i < expectedPositions.Length; i++)
            {
                Assert.Equal(expectedPositions[i], actualPositions[i]);
            }
        }
    }
}
