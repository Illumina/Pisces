using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class BaseApplicationOptionsTests
    {
        private class MyOptions : BaseApplicationOptions
        {
            public bool Test(string key, string value, Func<BaseApplicationOptions, bool> tester)
            {
                UpdateOptions(key, value);
                return tester(this);
            }
        }
        [Fact]
        [Trait("ReqID", "SDS-2")]
        public void BaseOptions()
        {
            Assert.True(new MyOptions().Test("-insidesUbprocess", "true", (x) => x.InsideSubProcess));
            Assert.True(new MyOptions().Test("-multiProcess", "true", (x) => x.MultiProcess));
            Assert.False(new MyOptions().Test("-multiProcess", "false", (x) => x.MultiProcess));
            Assert.Throws<FormatException>(() => new MyOptions().Test("-MultiProcess", "boo", (x) => x.MultiProcess));
            Assert.Throws<FormatException>(() => new MyOptions().Test("-InsideSubProcess", "boo", (x) => x.InsideSubProcess));

            Assert.True(new MyOptions().Test("-chrFilter", "goo", (x) => x.ChromosomeFilter == "goo"));
            Assert.True(new MyOptions().Test("-bampaths", "a,b,c", (x) => x.BAMPaths.Contains("a") && x.BAMPaths.Contains("b") && x.BAMPaths.Contains("c")));
            Assert.True(new MyOptions().Test("-bamfolder", "/foo", (x) => x.BAMFolder == "/foo"));
            Assert.True(new MyOptions().Test("-outfolder", "/out", (x) => x.OutputFolder == "/out"));
            Assert.True(new MyOptions().Test("-maxnumthreads", "6", (x) => x.MaxNumThreads == 6));
        }
    }
}
