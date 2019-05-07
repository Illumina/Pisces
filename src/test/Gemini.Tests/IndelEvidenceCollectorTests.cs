using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Types;
using Xunit;

namespace Gemini.Tests
{
    public class IndelEvidenceCollectorTests
    {
        [Fact]
        public void CollectIndelEvidence()
        {
            var targetFinder = new IndelTargetFinder();
            var pairs = new List<PairResult>();

            // Reputable read
            var pair1 = TestHelpers.GetPairResult(1,0, 
                classification: PairClassification.IndelUnstitchable,
                hasIndels: true, isReputableIndelContaining:true);
            // Less reputable
            var pair2 = TestHelpers.GetPairResult(1, 0,
                classification: PairClassification.IndelUnstitchable, hasIndels: true);

            pairs.Add(pair1);

            ConcurrentDictionary<string, IndelEvidence> indelLookup = new ConcurrentDictionary<string, IndelEvidence>();
            var results = IndelEvidenceCollector.CollectIndelEvidence(targetFinder, "chr1", indelLookup, pairs.ToArray());

            // This is just a pass-through
            Assert.Equal(pairs.Count, results.Length);

            // Check indel evidence
            Assert.Equal(1, indelLookup.Count);
            var indel = indelLookup.First();
            var expectedEvidence = new IndelEvidence()
            {
                Forward = 1,
                Reverse = 1,
                LeftAnchor = 10,
                RightAnchor = 10,
                ReputableSupport = 2
            };
            VerifyIndelEvidence("chr1:6 N>NT", expectedEvidence, indel.Key, indel.Value);

            // Add on more indel evidence for the same one
            results = IndelEvidenceCollector.CollectIndelEvidence(targetFinder, "chr1", indelLookup, pairs.ToArray());
            Assert.Equal(pairs.Count, results.Length);
            Assert.Equal(1, indelLookup.Count);
            indel = indelLookup.First();
            expectedEvidence = new IndelEvidence()
            {
                Forward = 2,
                Reverse = 2,
                LeftAnchor = 20,
                RightAnchor = 20,
                ReputableSupport = 4
            };
            VerifyIndelEvidence("chr1:6 N>NT", expectedEvidence, indel.Key, indel.Value);

            // Add on some less reputable evidence
            pairs.Clear();
            pairs.Add(pair2);
            results = IndelEvidenceCollector.CollectIndelEvidence(targetFinder, "chr1", indelLookup, pairs.ToArray());
            Assert.Equal(pairs.Count, results.Length);
            Assert.Equal(1, indelLookup.Count);
            indel = indelLookup.First();
            expectedEvidence = new IndelEvidence()
            {
                Forward = 3,
                Reverse = 3,
                LeftAnchor = 30,
                RightAnchor = 30,
                ReputableSupport = 4
            };
            VerifyIndelEvidence("chr1:6 N>NT", expectedEvidence, indel.Key, indel.Value);
        }

        private void VerifyIndelEvidence(string expectedIndel, IndelEvidence expectedEvidence, string actualIndel,
            IndelEvidence actualEvidence)
        {
            Assert.Equal(expectedIndel, actualIndel);
            Assert.Equal(expectedEvidence.Forward, actualEvidence.Forward);
            Assert.Equal(expectedEvidence.Reverse, actualEvidence.Reverse);
            Assert.Equal(expectedEvidence.LeftAnchor, actualEvidence.LeftAnchor);
            Assert.Equal(expectedEvidence.RightAnchor, actualEvidence.RightAnchor);
            Assert.Equal(expectedEvidence.ReputableSupport, actualEvidence.ReputableSupport);
        }
    }
}