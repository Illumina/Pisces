using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class CigarDirectionTests
    {
        [Fact]
        public void Constructor()
        {
            var directionString = "1F2S1R";
            var expectedDirections = new List<DirectionOp>()
            {
                GetDirectionOp(DirectionType.Forward, 1),
                GetDirectionOp(DirectionType.Stitched, 2),
                GetDirectionOp(DirectionType.Reverse, 1),
            };

            var cigarDirection = new CigarDirection(directionString);

            Assert.Equal(directionString, cigarDirection.ToString());
            CompareDirections(expectedDirections, cigarDirection.Directions);
        }

        private void CompareDirections(List<DirectionOp> expected, List<DirectionOp> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        private DirectionOp GetDirectionOp(DirectionType type, int length)
        {
            return new DirectionOp() {Direction = type, Length = length};
        }

        [Fact]
        public void Compress()
        {
            var directionString = "1F2S1S1R";
            var directions = new CigarDirection(directionString);
            var uncompressedDirections = new List<DirectionOp>()
            {
                GetDirectionOp(DirectionType.Forward, 1),
                GetDirectionOp(DirectionType.Stitched, 2),
                GetDirectionOp(DirectionType.Stitched, 1),
                GetDirectionOp(DirectionType.Reverse, 1),
            };

            Assert.Equal(directionString, directions.ToString());

            CompareDirections(uncompressedDirections, directions.Directions);

            directions.Compress();

            var compressedDirections = new List<DirectionOp>()
            {
                GetDirectionOp(DirectionType.Forward, 1),
                GetDirectionOp(DirectionType.Stitched, 3),
                GetDirectionOp(DirectionType.Reverse, 1),
            };

            CompareDirections(compressedDirections, directions.Directions);
        }

        [Fact]
        public void Expand()
        {
            var directionString = "2F3S2R";
            var directions = new CigarDirection(directionString);
            var expandedDirections = new List<DirectionType>()
            {
                DirectionType.Forward,
                DirectionType.Forward,
                DirectionType.Stitched,
                DirectionType.Stitched,
                DirectionType.Stitched,
                DirectionType.Reverse,
                DirectionType.Reverse
            };

            CompareDirectionTypes(expandedDirections, directions.Expand());
        }

        [Fact]
        public void Expander()
        {
            var directionString = "2F3S2R";
            var directions = new CigarDirection(directionString);
            var expandedDirections = directions.Expand();
            int i = 0;
            for (var expander = new CigarDirectionExpander(directions); expander.IsNotEnd(); expander.MoveNext())
            {
                Assert.Equal(expandedDirections[i], expander.Current);
                ++i;
            }
        }

        private void CompareDirectionTypes(List<DirectionType> expected, List<DirectionType> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

    }
}