using System.Collections.Generic;
using System.Linq;
using Gemini.CandidateIndelSelection;
using Gemini.IndelCollection;
using Gemini.IO;
using Xunit;

namespace Gemini.Tests
{
    public class BasicIndelFiltererTests
    {
        [Fact]
        public void GetRealignablePreIndels()
        {
            var filterer = new BasicIndelFilterer(0, 0, false);

            // Good support, good anchors, good direction balance, low mess
            var goodEvidence = new IndelEvidence()
            {
                Observations = 10,
                LeftAnchor = 500,
                RightAnchor = 500,
                Mess = 3,
                Quality = 300,
                Forward = 3,
                Reverse = 3,
                Stitched = 4,
                ReputableSupport = 5,
                IsRepeat = 0,
                IsSplit = 0
            };
            // Bad left anchor
            var badLeftAnchor = new IndelEvidence()
            {
                Observations = 10,
                LeftAnchor = 100,
                RightAnchor = 900,
                Mess = 3,
                Quality = 300,
                Forward = 3,
                Reverse = 3,
                Stitched = 4,
                ReputableSupport = 5,
                IsRepeat = 0,
                IsSplit = 0
            };
            // Bad right anchor
            var badRightAnchor = new IndelEvidence()
            {
                Observations = 10,
                LeftAnchor = 900,
                RightAnchor = 100,
                Mess = 3,
                Quality = 300,
                Forward = 3,
                Reverse = 3,
                Stitched = 4,
                ReputableSupport = 5,
                IsRepeat = 0,
                IsSplit = 0
            };

            // Support too low
            var supportTooLow = new IndelEvidence()
            {
                Observations = 4,
                LeftAnchor = 200,
                RightAnchor = 200,
                Mess = 0,
                Quality = 240,
                Forward = 1,
                Reverse = 1,
                Stitched = 2,
                ReputableSupport = 4,
                IsRepeat = 0,
                IsSplit = 0
            };
            var supportTooLowAndIsMess = new IndelEvidence()
            {
                Observations = 4,
                LeftAnchor = 200,
                RightAnchor = 200,
                Mess = 3,
                Quality = 240,
                Forward = 1,
                Reverse = 1,
                Stitched = 2,
                ReputableSupport = 4,
                IsRepeat = 0,
                IsSplit = 0
            };

            var indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor }, 
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow }, 
                {"chr4:123 A>ATG",  supportTooLowAndIsMess}, 
            };

            var realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            var indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(5, indels.Count());

            // Filter by support only
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor },
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow },
                {"chr4:123 A>ATG",  supportTooLowAndIsMess},  
            };
            filterer = new BasicIndelFilterer(5, 0, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(3, indels.Count());

            // Filter by anchor only
            // Note, by default we throw out anything with 0 observations (what does that even mean?)
            // Should keep chr1:123 A>ATG, chr3:123 A>ATG and chr4:123 A>ATG
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor },
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow },
                {"chr4:123 A>ATG",  supportTooLowAndIsMess}, 
            };
            filterer = new BasicIndelFilterer(0, 20, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(3, indels.Count());

            // Filter by anchor and support
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor },
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow },
                {"chr4:123 A>ATG",  supportTooLowAndIsMess},
            };

            filterer = new BasicIndelFilterer(5, 20, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Single(indels);

            // Rescue good indel that doesn't meet the requirements
            // Low support but 
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor },
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow },
                {"chr4:123 A>ATG",  supportTooLowAndIsMess},  
            };

            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, true);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(2, indels.Count());

            // Don't rescue stuff that falls below required minimum
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG", goodEvidence},
                {"chr1:123 A>ATGC", badLeftAnchor },
                {"chr2:123 ATG>A", badRightAnchor},
                {"chr3:123 A>ATG", supportTooLow },
                {"chr4:123 A>ATG",  supportTooLowAndIsMess}, 
            };

            filterer = new BasicIndelFilterer(5, 20, false, strictFoundThreshold: 5);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, true);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(1.0, indels.Count());

            // Multis
            indelsDict = new Dictionary<string, IndelEvidence>()
            {
                {"chr1:123 A>ATG|chr1:140 C>CTG", goodEvidence},
            };

            filterer = new BasicIndelFilterer(5, 20, false, strictFoundThreshold: 5);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, true);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(2.0, indels.Count());

        }
    }
}