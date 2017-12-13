using System;

namespace VcfCompare
{
    public class ComparisonResult
    {
        public string Key;
        public string BaselineValue;
        public string TestValue;
        public bool OK;
        public string ResultDetail;

        public ComparisonResult(string key, string baselineValue, string testValue, bool ok, string resultDetail = "")
        {
            Key = key;
            BaselineValue = baselineValue;
            TestValue = testValue;
            OK = ok;
        }

        public ComparisonResult(string key, double baselineValue, double testValue, bool ok, string resultDetail = "")
        {
            Key = key;
            BaselineValue = Math.Round(baselineValue,3).ToString();
            TestValue = Math.Round(testValue,3).ToString();
            OK = ok;
        }

        public override string ToString()
        {
            return string.Format("{0}: ({1} vs {2})", Key, BaselineValue, TestValue);
        }
    }
}