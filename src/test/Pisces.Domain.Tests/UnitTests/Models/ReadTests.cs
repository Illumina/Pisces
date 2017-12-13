using System;
using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class ReadTests
    {
        [Fact]
        public void Constructor()
        {
            var chr = "chr4";
            var sequence = "ACTCTAAAAA";
            var position = 134345;

            var read = new Read(chr, new BamAlignment() { Bases = sequence, Position = position });

            Assert.Equal(chr, read.Chromosome);
            Assert.Equal(sequence, read.Sequence);
            Assert.Equal(position + 1, read.Position);
            Assert.Equal(10, read.ReadLength);
            Assert.Equal(read.PositionMap.Length, read.ReadLength);

            for (var i = 0; i < read.PositionMap.Length; i++)
            {
                Assert.Equal(-1, read.PositionMap[i]);  // default for no cigar is -1
            }

            Assert.Throws<ArgumentException>(() => new Read("", read.BamAlignment));  // empty chr
            Assert.Throws<ArgumentException>(() => new Read(null, read.BamAlignment));  // empty chr
            Assert.Throws<ArgumentException>(() => new Read("chr1", null));  // null alignment
            Assert.Throws<ArgumentException>(() => new Read("chr1", new BamAlignment()));  // alignment with no sequences
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

            foreach (var direction in read.SequencedBaseDirectionMap)
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
                Qualities = new[] { (byte)20, (byte)21, (byte)30, (byte)40 },
                CigarData = new CigarAlignment("1S3M")
            };
            alignment.SetIsUnmapped(false);
            alignment.SetIsSecondaryAlignment(false);
            alignment.SetIsDuplicate(true);
            alignment.SetIsProperPair(true);

            var read = new Read("chr1", alignment);
            read.StitchedCigar = new CigarAlignment("7M");
            read.SequencedBaseDirectionMap = new[] { DirectionType.Forward, DirectionType.Reverse, DirectionType.Stitched, DirectionType.Reverse };
            var clonedRead = read.DeepCopy();

            Tests.ReadTests.CompareReads(read, clonedRead);

            // verify the arrays are deep copies
            read.PositionMap[0] = 1000;
            Assert.False(clonedRead.PositionMap[0] == 1000);
            read.SequencedBaseDirectionMap[0] = DirectionType.Stitched;
            Assert.False(clonedRead.SequencedBaseDirectionMap[0] == DirectionType.Stitched);
            read.Qualities[0] = 11;
            Assert.False(clonedRead.Qualities[0] == 11);

            read.CigarData.Reverse();
            Assert.False(((Read)clonedRead).CigarData.ToString() == read.CigarData.ToString());
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
            read.SequencedBaseDirectionMap = new[] { DirectionType.Forward, DirectionType.Reverse, DirectionType.Stitched, DirectionType.Reverse };

            alignment.SetIsDuplicate(false);
            alignment.MatePosition = 555;

            read.Reset("chr2", alignment);
            Assert.Equal(556, read.MatePosition);
            Assert.False(read.IsPcrDuplicate);
            Assert.Equal("chr2", read.Chromosome);

            var stitchedCigar = "1S3M1S";
            alignment.TagData = ReadTestHelper.GetXCTagData(stitchedCigar);
            read.Reset("chr3", alignment);
            Assert.Equal(556, read.MatePosition);
            Assert.False(read.IsPcrDuplicate);
            Assert.Equal("chr3", read.Chromosome);
            Assert.Equal(stitchedCigar, read.StitchedCigar.ToString());

        }

        [Fact]
        public void ReadCollapsedCounts()
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

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(5, 10);
            var read = new Read("chr1", alignment);

            Assert.True(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(0, 5);  // first tag is 0
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(null, 5);  // first tag is missing
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(5, 0);  // second tag is 0
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(5, null);  // second tag is missing
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(0, 0);  // both tags 0
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);

            alignment.TagData = ReadTestHelper.GetReadCountsTagData(null, null);  // both tags missing
            read = new Read("chr1", alignment);
            Assert.False(read.IsDuplex);
        }

        [Fact]
        public void CigarData()
        {
            var read = ReadTestHelper.CreateRead("chr4", "ACCGACTAAC", 4, new CigarAlignment("10M"));

            Verify(new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, read.PositionMap);

            read = ReadTestHelper.CreateRead("chr4", "ACCGACTAAC", 4, new CigarAlignment("2S1M4I5D2M1S"));
            Verify(new[] { -1, -1, 4, -1, -1, -1, -1, 10, 11, -1 }, read.PositionMap);

            read = ReadTestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("12M"));

            for (var i = 0; i < read.PositionMap.Length; i++)
                Assert.Equal(read.PositionMap[i], read.Position + i);

            read = ReadTestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("2S5M4I10D1M"));
            Verify(new[]
            {
                -1, -1, 100, 101, 102, 103, 104, -1, -1, -1, -1, 115
            }, read.PositionMap);

            Assert.Throws<System.IO.InvalidDataException>(() => ReadTestHelper.CreateRead("chr1", "ACTTCCCAAAAT", 100, new CigarAlignment("100M")));
        }

        [Fact]
        public void CoverageSummary()
        {
            var chr = "chr4";
            var sequence = "ACTCTAAAAA";
            var position = 134345;
            var cigar = "10M";

            var read = new Read(chr, new BamAlignment() { Bases = sequence, Position = position - 1, CigarData = new CigarAlignment(cigar) });

            // happy path
            var readSummary = read.GetCoverageSummary();
            Assert.Equal(position, readSummary.ClipAdjustedStartPosition);
            Assert.Equal(position + 9, readSummary.ClipAdjustedEndPosition);
            Assert.Equal(cigar, readSummary.CigarString);
            Assert.Equal("10F", readSummary.DirectionString);

            // clip adjusted position
            cigar = "2S1I5M2S";
            read.BamAlignment.CigarData = new CigarAlignment(cigar);
            readSummary = read.GetCoverageSummary();
            Assert.Equal(position - 2, readSummary.ClipAdjustedStartPosition);
            Assert.Equal(position + 7 - 1, readSummary.ClipAdjustedEndPosition);
            Assert.Equal(cigar, readSummary.CigarString);

            // direction string
            for (var i = 0; i < read.SequencedBaseDirectionMap.Length; i++)
                read.SequencedBaseDirectionMap[i] = DirectionType.Reverse;
            Assert.Equal("10R", read.GetCoverageSummary().DirectionString);
            for (var i = 0; i < read.SequencedBaseDirectionMap.Length; i++)
                read.SequencedBaseDirectionMap[i] = DirectionType.Stitched;
            Assert.Equal("10S", read.GetCoverageSummary().DirectionString);

            // combo
            read.SequencedBaseDirectionMap = new[]
            {
                DirectionType.Reverse, DirectionType.Reverse,
                DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched,
                DirectionType.Forward, DirectionType.Forward, DirectionType.Forward, DirectionType.Forward
            };
            Assert.Equal("2R:4S:4F", read.GetCoverageSummary().DirectionString);
        }

        private void Verify(int[] expectedPositions, int[] actualPositions)
        {
            Assert.Equal(actualPositions.Length, expectedPositions.Length);

            for (var i = 0; i < expectedPositions.Length; i++)
            {
                Assert.Equal(expectedPositions[i], actualPositions[i]);
            }
        }



        [Fact]
        public void SequencedBaseDirectionMapTest()
        {
            //easy cases:
            CheckSequencedBaseDirectionMap("6F", "5M1S", "ATCTTA",
                new DirectionType[] { DirectionType.Forward, DirectionType.Forward, DirectionType.Forward, DirectionType.Forward, DirectionType.Forward, DirectionType.Forward });

            CheckSequencedBaseDirectionMap("6S", "2S3M1S", "ATCTTA",
                      new DirectionType[] { DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched });

            CheckSequencedBaseDirectionMap("6R", "2M3I1S", "ATCTTA",
                new DirectionType[] { DirectionType.Reverse, DirectionType.Reverse, DirectionType.Reverse, DirectionType.Reverse, DirectionType.Reverse, DirectionType.Reverse });

            CheckSequencedBaseDirectionMap("6R", "2M3D1S", "ATA",
                new DirectionType[] { DirectionType.Reverse, DirectionType.Reverse,
                    DirectionType.Reverse });

            //more complex cases
            CheckSequencedBaseDirectionMap("2F3S1R", "5M1S", "ATCTTA",
                new DirectionType[] { DirectionType.Forward, DirectionType.Forward, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Reverse });

            CheckSequencedBaseDirectionMap("1R2F3S", "2S3M1S", "ATCTTA",
                      new DirectionType[] { DirectionType.Reverse, DirectionType.Forward, DirectionType.Forward, DirectionType.Stitched, DirectionType.Stitched, DirectionType.Stitched });

            CheckSequencedBaseDirectionMap("1R1F1S1R1F1S", "2M3I1S", "ATCTTA",
                new DirectionType[] { DirectionType.Reverse, DirectionType.Forward, DirectionType.Stitched, DirectionType.Reverse, DirectionType.Forward, DirectionType.Stitched });

            CheckSequencedBaseDirectionMap("1R1F1S1R1F1S", "2M3D1S", "ATA",
             new DirectionType[] { DirectionType.Reverse, DirectionType.Forward,
                 DirectionType.Stitched });

        }

        [Fact]
        public void IsReadCollapsed()
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
            var read = new Read("chr1", alignment);
            Assert.False(read.IsCollapsedRead());
            alignment.TagData = ReadTestHelper.GetReadCountsTagData(1, 10);  // set XV and XW tags
            Assert.True(read.IsCollapsedRead());
        }


        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void GetReadCollapsedType(ReadCollapsedType readCollapsedType)
        {
            var readpair = ReadTestHelper.CreateReadPair("test", 6, readCollapsedType, pos: 10, matePos: 15, minBaseQuality: 30);
            for (int i = 0; i <= 5; i++)
            {
                DirectionType direction = readpair.Item1.SequencedBaseDirectionMap[i];
                Assert.Equal(readCollapsedType, readpair.Item1.GetReadCollapsedType(direction));
                direction = readpair.Item2.SequencedBaseDirectionMap[i];
                Assert.Equal(readCollapsedType, readpair.Item2.GetReadCollapsedType(direction));
            }
        }


        private static void CheckSequencedBaseDirectionMap(string inputXDtag, string inputCigarString, string sequence,
            DirectionType[] expectedDirectionMap)
        {
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("XD", inputXDtag);

            var alignment = new BamAlignment
            {
                Bases = sequence,
                Position = 100,
                MatePosition = 500,
                Name = "test",
                CigarData = new CigarAlignment(inputCigarString),
                MapQuality = 10,
                TagData = tagUtils.ToBytes(),
                Qualities = new[] { (byte)10, (byte)20, (byte)30 }
            };

            var read = new Read("chr7", alignment);
            var directionMap = read.SequencedBaseDirectionMap;
            Assert.Equal(expectedDirectionMap, directionMap);

            var directTestMap = Read.CreateSequencedBaseDirectionMap(read.CigarDirections.Expand().ToArray(),read.CigarData);
            Assert.Equal(expectedDirectionMap, directTestMap);
        }


        [Fact]
        public void ReadIndexToExpandedIndexTest()
        {
            //pathological cases

            //the "6" will run it out of bounds
            Assert.Throws<ArgumentException>(
                () => CheckReadIndexToExpandedIndex("6F", "5M1S", "ATCTTA",
                new int[] { 0, 1, 2, 3, 4, 5, 6 }, new int[] { 0, 1, 2, 3, 4, 5, -1 }));

            //the "-1" will run it out of bounds
            Assert.Throws<ArgumentException>(
                () => CheckReadIndexToExpandedIndex("6F", "5M1S", "ATCTTA",
                new int[] { -1, 0, 1, 2, 3, 4, 5 }, new int[] { -1, 0, 1, 2, 3, 4, 5 }));



            //easy cases:
            CheckReadIndexToExpandedIndex("6F", "5M1S", "ATCTTA",
                new int[] { 0, 1, 2, 3, 4, 5}, new int[] { 0, 1, 2, 3, 4, 5});

            CheckReadIndexToExpandedIndex( "6S", "2S3M1S", "ATCTTA",
                    new int[] { 0, 1, 2, 3, 4, 5}, new int[] { 0, 1, 2, 3, 4, 5 });

            CheckReadIndexToExpandedIndex("6R", "2M3I1S", "ATCTTA",
                  new int[] { 0, 1, 2, 3, 4, 5}, new int[] { 0, 1, 2, 3, 4, 5 });

            // ATA -> ATXXXA  ; 0 1 x x x 2 -1 -1 -> 0 1 2 3 4 5 -1 -1 -1 -1
            //  read    |   expanded read
            //  0       |   0 
            //  1       |   1 
            //  x       |   (2) 
            //  x       |   (3) 
            //  x       |   (4) 
            //  2       |   5 


            CheckReadIndexToExpandedIndex("6R", "2M3D1S", "ATA",
                 new int[] { 0, 1, 2 }, new int[]  { 0, 1, 5 });

            //more complex cases
            CheckReadIndexToExpandedIndex("2F3S1R", "5M1S", "ATCTTA",
                new int[] { 0, 1, 2, 3, 4, 5 }, new int[]  { 0, 1, 2, 3, 4, 5});

            CheckReadIndexToExpandedIndex("1R2F3S", "2S3M1S", "ATCTTA",
               new int[] { 0, 1, 2, 3, 4, 5 }, new int[]  { 0, 1, 2, 3, 4, 5 });

            CheckReadIndexToExpandedIndex("1R1F1S1R1F1S", "2M3I1S", "ATCTTA",
               new int[] { 0, 1, 2, 3, 4, 5}, new int[]  { 0, 1, 2, 3, 4, 5});

            CheckReadIndexToExpandedIndex("1R1F1S1R1F1S", "2M3D1S", "ATA",
               new int[] { 0, 1, 2}, new int[]  { 0, 1, 5});


            //several deletions

            // ATAGG -> ATXXXAXXGG  ;         
            //  read    |   expanded read   |   Base
            //  0       |   0               |   A
            //  1       |   1               |   T 
            //  x       |   (2)             |   x
            //  x       |   (3)             |   x
            //  x       |   (4)             |   x
            //  2       |   5               |   A
            //  x       |   (6)             |   x
            //  x       |   (7)             |   x
            //  3       |   8               |   G
            //  4       |   9               |   G

            CheckReadIndexToExpandedIndex("6R4S", "2M3D1M2D2S", "ATAGG",
                 new int[] { 0, 1, 2, 3, 4 }, 
                 new int[] {  0, 1, 5, 8, 9});

        }

        private static void CheckReadIndexToExpandedIndex(string inputXDtag, string inputCigarString, string sequence,
            int[] readIndexes, int[] expectedExpandedIndexes)
        {
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("XD", inputXDtag);

            var alignment = new BamAlignment
            {
                Bases = sequence,
                Position = 100,
                MatePosition = 500,
                Name = "test",
                CigarData = new CigarAlignment(inputCigarString),
                MapQuality = 10,
                TagData = tagUtils.ToBytes(),
                Qualities = new[] { (byte)10, (byte)20, (byte)30 }
            };

            var read = new Read("chr7", alignment);
            var directionMap = read.SequencedBaseDirectionMap;
            var observedExpandedIndexes = read.SequencedIndexesToExpandedIndexes(readIndexes);

            for (int i=0; i < readIndexes.Length; i++)
            {
                var observedExpandedIndex = observedExpandedIndexes[i];
                var expectedExpandedIndex = expectedExpandedIndexes[i];
                
                Assert.Equal(expectedExpandedIndex, observedExpandedIndex);
            }
           ;
        }

    }
}
  