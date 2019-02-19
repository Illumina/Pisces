using System.Collections.Generic;
using System.Linq;
using Gemini.IndelCollection;
using Xunit;

namespace Gemini.Tests
{
    public class IndelEvidenceHelperTests
    {
        [Fact]
        public void FindIndelsAndRecordEvidence()
        {
            var readPair = TestHelpers.GetPair("5M1D5M", "5M2I4M", nm2: 3);
            var readPair2 = TestHelpers.GetPair("3M1D8M", "5M1D5M", nm2:4);
            var targetFinder = new IndelTargetFinder();
            var lookup = new Dictionary<string, int[]>();
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read1, targetFinder, lookup, true, "chr1", 10);

            var expectedDel = "chr1:104 NN>N";
            var expectedIns = "chr1:104 N>NTT";
            Assert.Equal(1.0, lookup.Count);
            Assert.Equal(expectedDel, lookup.Keys.First());

            //obs,left,right,mess,quals,fwd,reverse,stitched,reput
            ValidateArraysMatch(new []{1,5,5,0,30,1,0,0,1}, lookup[expectedDel]);

            // Build evidence for same indel, let's call it stitched this time
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read1, targetFinder, lookup, true, "chr1", 10, true);
            Assert.Equal(1.0, lookup.Count);
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateArraysMatch(new[] { 2, 10, 10, 0, 60, 1, 0, 1, 2 }, lookup[expectedDel]);

            // Build evidence for same indel from a different read, this one's not reputable and is reverse
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair2.Read2, targetFinder, lookup, false, "chr1", 10);
            Assert.Equal(1.0, lookup.Count);
            Assert.Contains(expectedDel, lookup.Keys);
            // mess should subtract ins length from nm
            ValidateArraysMatch(new[] { 3, 15, 15, 3, 90, 1, 1, 1, 2 }, lookup[expectedDel]);

            // Different indel, reverse only
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read2, targetFinder, lookup, true, "chr1", 10);
            Assert.Equal(2, lookup.Count);
            // Original del shouldn't have changed
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateArraysMatch(new[] { 3, 15, 15, 3, 90, 1, 1, 1, 2 }, lookup[expectedDel]);

            Assert.Contains(expectedIns, lookup.Keys);
            // mess should subtract ins length from nm
            ValidateArraysMatch(new[] { 1, 5, 4, 1, 30, 0, 1, 0, 1 }, lookup[expectedIns]);

        }

        private void ValidateArraysMatch(int[] expected, int[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

    }
}