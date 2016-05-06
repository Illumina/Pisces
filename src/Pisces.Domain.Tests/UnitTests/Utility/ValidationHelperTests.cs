using System;
using Pisces.Domain.Utility;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Utility
{
    public class ValidationHelperTests
    {
        [Fact]
        public void Verify_Int_Range()
        {
            ValidationHelper.VerifyRange(100, 99, null, "test");
            ValidationHelper.VerifyRange(100, 99, 101, "test");
            ValidationHelper.VerifyRange(100, 99, 100, "test");
            ValidationHelper.VerifyRange(100, 100, 100, "test");
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(87, 100, 100, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(87, 100, 150, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(151, 100, 150, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(99, 100, null, "test"));
        }

        [Fact]
        public void Verify_Float_Range()
        {
            ValidationHelper.VerifyRange(4.0f, 3.5f, null, "test");
            ValidationHelper.VerifyRange(4.0f, 3.1f, 5.1f, "test");
            ValidationHelper.VerifyRange(5.1f, 3.1f, 5.1f, "test");
            ValidationHelper.VerifyRange(3.1f, 3.1f, 3.1f, "test");
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(3.0f, 3.1f, 3.1f, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(3.1f, 5.1f, 7.1f, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(3.2f, 2.1f, 3.1f, "test"));
            Assert.Throws<ArgumentException>(() => ValidationHelper.VerifyRange(1.2f, 2.1f, null, "test"));
        }
    }
}