using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemini.CandidateIndelSelection;
using Gemini.FromHygea;
using Xunit;

namespace Gemini.Tests
{
    public class BasicIndelFiltererTests
    {
        [Fact]
        public void MergeIndelEvidence()
        {
            var originalIndelEvidence = new Dictionary<string, int[]>()
            {
            };
            var secondIndelEvidence = new Dictionary<string, int[]>();
        }

        [Fact]
        public void GetRealignablePreIndels()
        {
            var filterer = new BasicIndelFilterer(0, 0, false);

            var indelsDict = new Dictionary<string, int[]>()
            {
                {"chr1:123 A>ATG", new []{10,500,500,3,300,3,3,4,5} }, // Good support, good anchors, good direction balance, low mess
                {"chr1:123 A>ATGC", new []{10,100,900,3,300,3,3,4,5} }, // Bad left anchor
                {"chr2:123 ATG>A", new []{10,900,100,3,300,3,3,4,5} }, // Bad right anchor
                {"chr3:123 A>ATG", new []{4,200,200,3,120,1,1,2,4} }, // Support too low
                {"chr4:123 A>ATG", new []{4,200,200,0,240,1,1,2,4} }, // 
            };

            var realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            var indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(5, indels.Count());

            // Filter by support only
            filterer = new BasicIndelFilterer(5, 0, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(3, indels.Count());

            // Filter by anchor only
            filterer = new BasicIndelFilterer(0, 20, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(3, indels.Count());

            // Filter by anchor and support
            filterer = new BasicIndelFilterer(5, 20, false);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, false);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(1.0, indels.Count());

            // Rescue good indel that doesn't meet the requirements
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, true);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(2, indels.Count());

            // Don't rescue stuff that falls below required minimum
            filterer = new BasicIndelFilterer(5, 20, false, strictFoundThreshold: 5);
            realignableIndels = filterer.GetRealignablePreIndels(indelsDict, true);
            indels = realignableIndels.SelectMany(x => x.Value);
            Assert.Equal(1.0, indels.Count());
        }
    }
}