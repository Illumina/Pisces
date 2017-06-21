using System;
using System.IO;
using Common.IO.Utility;
using Xunit;

namespace Common.IO.Tests
{
    public class JsonTests
    {
        public class MyClass
        {
            public int myInt = 4;
            public double[] myArray = new double[] { 1,2,3};
            public string myString = "hello";
        }

        [Fact]
        public void JsonIOTests()
        {
     
            var filePath = Path.Combine(TestPaths.LocalScratchDirectory, "JsonTest.json");
            var someObject = new MyClass();

            Assert.Equal(someObject.myString, "hello");
            someObject.myString = "replaced";

   

            if (File.Exists(filePath))
                File.Delete(filePath);
            JsonUtil.Save(filePath,someObject);

            Assert.True(File.Exists(filePath));

            MyClass newObject = JsonUtil.Deserialize<MyClass>(filePath);
            Assert.Equal(newObject.myString, "replaced");
        }
    }
}
