using System;
using System.Collections.Generic;
using System.Text;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class ReadExtensionTests
    {
        [Theory]
        [InlineData(DirectionType.Forward, "FF")]
        [InlineData(DirectionType.Reverse, "RR")]
        [InlineData(DirectionType.Stitched, "FF")]
        [InlineData(DirectionType.Stitched, "RR")]
        public void GetAlignementCollapsedType_NonProperReadPair(DirectionType type, string orientation)
        {
            var nonProperReadPair = ReadTestHelper.CreateNonProperReadPair("test", 6, type, orientation, pos: 10, matePos: 15, minBaseQuality: 30);
            Assert.Null(nonProperReadPair.Item1.GetReadCollapsedType(type));
            Assert.Null(nonProperReadPair.Item2.GetReadCollapsedType(type));
        }
    }
}
