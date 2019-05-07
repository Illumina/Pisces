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
            var lookup = new Dictionary<string, IndelEvidence>();
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read1, targetFinder, lookup, true, "chr1", 10);

            var expectedDel = "chr1:104 NN>N";
            var expectedIns = "chr1:104 N>NTT";
            Assert.Equal(1.0, lookup.Count);
            Assert.Equal(expectedDel, lookup.Keys.First());

            //obs,left,right,mess,quals,fwd,reverse,stitched,reput
            var evidence = new IndelEvidence()
            {
                Observations = 1,
                LeftAnchor = 5,
                RightAnchor = 5,
                Mess = 0,
                Quality = 30,
                Forward = 1,
                Reverse = 0,
                Stitched =0,
                ReputableSupport = 1,
                IsRepeat = 0,
                IsSplit = 0
            };
            ValidateEvidenceMatches(evidence, lookup[expectedDel]);

            // Build evidence for same indel, let's call it stitched this time
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read1, targetFinder, lookup, true, "chr1", 10, true);
            Assert.Equal(1.0, lookup.Count);
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateEvidenceMatches( new IndelEvidence()
            {
                Observations = 2,
                LeftAnchor = 10,
                RightAnchor = 10,
                Mess = 0,
                Quality = 60,
                Forward = 1,
                Reverse = 0,
                Stitched = 1,
                ReputableSupport = 2,
                IsRepeat = 0,
                IsSplit = 0
            }, lookup[expectedDel]);

            // Build evidence for same indel from a different read, this one's not reputable and is reverse
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair2.Read2, targetFinder, lookup, false, "chr1", 10);
            Assert.Equal(1.0, lookup.Count);
            Assert.Contains(expectedDel, lookup.Keys);
            // mess should subtract ins length from nm
            ValidateEvidenceMatches(
            new IndelEvidence(){
                Observations = 3,
                LeftAnchor = 15,
                RightAnchor = 15,
                Mess = 3,
                Quality = 90,
                Forward = 1,
                Reverse = 1,
                Stitched = 1,
                ReputableSupport = 2,
                IsRepeat = 0,
                IsSplit = 0
            }, lookup[expectedDel]);

            // Different indel, reverse only
            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPair.Read2, targetFinder, lookup, true, "chr1", 10);
            Assert.Equal(2, lookup.Count);
            // Original del shouldn't have changed
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                    {
                        Observations = 3,
                        LeftAnchor = 15,
                        RightAnchor = 15,
                        Mess = 3,
                        Quality = 90,
                        Forward = 1,
                        Reverse = 1,
                        Stitched = 1,
                        ReputableSupport = 2,
                        IsRepeat = 0,
                        IsSplit = 0
                    }, lookup[expectedDel]);

            Assert.Contains(expectedIns, lookup.Keys);
            // mess should subtract ins length from nm
            ValidateEvidenceMatches(
                new IndelEvidence
                {
                        Observations = 1,
                        LeftAnchor = 5,
                        RightAnchor = 4,
                        Mess = 1,
                        Quality = 30,
                        Forward = 0,
                        Reverse = 1,
                        Stitched = 0,
                        ReputableSupport = 1,
                        IsRepeat = 0,
                        IsSplit = 0
                    }
                , lookup[expectedIns]);


            // Multi-indel
            var readPairMulti = TestHelpers.GetPair("5M1D1M1D4M", "5M1D1M1D4M", nm: 2, nm2: 2);

            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPairMulti.Read1, targetFinder, lookup, true, "chr1", 10);
            Assert.Equal(3, lookup.Count);
            // Original del shouldn't have changed
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                {
                    Observations = 3,
                    LeftAnchor = 15,
                    RightAnchor = 15,
                    Mess = 3,
                    Quality = 90,
                    Forward = 1,
                    Reverse = 1,
                    Stitched = 1,
                    ReputableSupport = 2,
                    IsRepeat = 0,
                    IsSplit = 0
                }, lookup[expectedDel]);
            var expectedMulti = "chr1:104 NN>N|chr1:106 NN>N";
            Assert.Contains(expectedMulti, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                {
                    Observations = 1,
                    LeftAnchor = 5,
                    RightAnchor = 4,
                    Mess = 0,
                    Quality = 30,
                    Forward = 1,
                    Reverse = 0,
                    Stitched = 0,
                    ReputableSupport = 1,
                    IsRepeat = 0,
                    IsSplit = 0
                }, lookup[expectedMulti]);

            // Multi that are far apart - allow to track individually too.
            var readPairMultiFar = TestHelpers.GetPair("5M1D26M1D4M", "5M1D26M1D4M", nm: 2, nm2: 2);

            IndelEvidenceHelper.FindIndelsAndRecordEvidence(readPairMultiFar.Read1, targetFinder, lookup, true, "chr1", 10);
            Assert.Equal(5, lookup.Count);
            // Original del shouldn't have changed
            Assert.Contains(expectedDel, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                {
                    Observations = 4,
                    LeftAnchor = 20,
                    RightAnchor = 41,
                    Mess = 4,
                    Quality = 120,
                    Forward = 2,
                    Reverse = 1,
                    Stitched = 1,
                    ReputableSupport = 3,
                    IsRepeat = 0,
                    IsSplit = 0
                }, lookup[expectedDel]);
            var expectedMultiFar = "chr1:104 NN>N|chr1:131 NN>N";
            Assert.Contains(expectedMultiFar, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                {
                    Observations = 1,
                    LeftAnchor = 5,
                    RightAnchor = 4,
                    Mess = 0,
                    Quality = 30,
                    Forward = 1,
                    Reverse = 0,
                    Stitched = 0,
                    ReputableSupport = 1,
                    IsRepeat = 0,
                    IsSplit = 0
                }, lookup[expectedMultiFar]);
            var expectedSecondSingleFromMulti = "chr1:131 NN>N";
            Assert.Contains(expectedSecondSingleFromMulti, lookup.Keys);
            ValidateEvidenceMatches(
                new IndelEvidence()
                {
                    Observations = 1,
                    LeftAnchor = 26,
                    RightAnchor = 4,
                    Mess = 1,
                    Quality = 30,
                    Forward = 1,
                    Reverse = 0,
                    Stitched = 0,
                    ReputableSupport = 1,
                    IsRepeat = 0,
                    IsSplit = 0
                }, lookup[expectedSecondSingleFromMulti]);
        }

        private void ValidateEvidenceMatches(IndelEvidence expected, IndelEvidence actual)
        {
            Assert.Equal(expected.Stitched, actual.Stitched);
            Assert.Equal(expected.Forward, actual.Forward);
            Assert.Equal(expected.Reverse, actual.Reverse);
            Assert.Equal(expected.Observations, actual.Observations);
            Assert.Equal(expected.Quality, actual.Quality);
            Assert.Equal(expected.Mess, actual.Mess);
            Assert.Equal(expected.LeftAnchor, actual.LeftAnchor);
            Assert.Equal(expected.RightAnchor, actual.RightAnchor);
            Assert.Equal(expected.IsRepeat, actual.IsRepeat);
            Assert.Equal(expected.ReputableSupport, actual.ReputableSupport);
            Assert.Equal(expected.IsSplit, actual.IsSplit);

            //Assert.Equal(expected.Length, actual.Length);
            //for (int i = 0; i < expected.Length; i++)
            //{
            //    Assert.Equal(expected[i], actual[i]);
            //}
        }

    }
}