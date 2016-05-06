using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests
{
    public class ConstantsTests
    {
        [Fact]
        public void CheckEnums()
        {
            // the counts are currently "hardcoded" explicitly in Constants since mono is unreliable fetching them
            // make sure we haven't gotten out of sync
            Assert.Equal(Enum.GetValues(typeof(AlleleType)).Length, Constants.NumAlleleTypes);
            Assert.Equal(Enum.GetValues(typeof(DirectionType)).Length, Constants.NumDirectionTypes);
            Assert.Equal(Enum.GetValues(typeof(ReadCollapsedType)).Length, Constants.NumReadCollapsedTypes);
        }
    }
}
