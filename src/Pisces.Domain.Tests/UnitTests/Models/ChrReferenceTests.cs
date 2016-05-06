using System;
using Pisces.Domain.Models;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models
{
    public class ChrReferenceTests
    {
        [Fact]
        public void GetBase_Scenarios()
        {
            var chrReference = new ChrReference { Name = "chr1", Sequence = "TTTTTTTTTTTT" };

            //Happy Path
            Assert.Equal("T", chrReference.GetBase(3));
            Assert.Equal("T", chrReference.GetBase(1));
            Assert.Equal("T", chrReference.GetBase(12));
            //Invalid Position - Greater than sequence length
            Assert.Throws<ArgumentException>(()=>chrReference.GetBase(100));
            Assert.Throws<ArgumentException>(()=>chrReference.GetBase(13));
            //Invalid Position - Less than 1 
            Assert.Throws<ArgumentException>(() => chrReference.GetBase(-1));
            Assert.Throws<ArgumentException>(() => chrReference.GetBase(0));
        }
    }
}