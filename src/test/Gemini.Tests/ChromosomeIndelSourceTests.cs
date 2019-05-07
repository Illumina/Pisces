using System.Collections.Generic;
using System.Linq;
using Gemini.Interfaces;
using Gemini.IO;
using Gemini.Logic;
using Gemini.Realignment;
using Gemini.Types;
using Moq;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class ChromosomeIndelSourceTests
    {
        [Fact]
        public void GetRelevantIndels()
        {
            var indel = new HashableIndel()
            {
                AlternateAllele = "AG",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 1,
                ReferencePosition = 10002,
                Score = 1,
                Type = AlleleCategory.Insertion
            };
            var indel2 = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 10002,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var positionWayLower = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 8002,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var positionLikelyDiffBlockButWithinRange = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 9800,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var positionWayHigher = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 21000,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var borderCaseHigh = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 10251,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var borderCaseLow = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 9752,
                Score = 10,
                Type = AlleleCategory.Insertion
            };
            var indelAt0 = new HashableIndel()
            {
                AlternateAllele = "AGT",
                ReferenceAllele = "A",
                Chromosome = "chr1",
                Length = 2,
                ReferencePosition = 0,
                Score = 10,
                Type = AlleleCategory.Insertion
            };


            var indels = new List<HashableIndel>()
            {
                indel,
                indel2,
                positionWayLower,
                positionLikelyDiffBlockButWithinRange,
                positionWayHigher,
                borderCaseHigh,
                borderCaseLow,
                indelAt0
            };

            var snippetSource = new Mock<IGenomeSnippetSource>();
            snippetSource.Setup(s => s.GetGenomeSnippet(It.IsAny<int>())).Returns(new GenomeSnippet() { Chromosome = "chr1", Sequence = new string('A', 2000), StartPosition = 1 });
            var indelSource = new ChromosomeIndelSource(indels, snippetSource.Object);

            //var relevant = indelSource.GetRelevantIndels(100);
            //Assert.Equal(4, relevant.Count);

            // Should get indel1 and 2, border high, border low, withinrange
            var relevant = indelSource.GetRelevantIndels(10002);
            Assert.Equal(5, relevant.Count());

            // Should get indel1 and 2, border low, within range, but not border high (now > 250 away)
            relevant = indelSource.GetRelevantIndels(10000);
            Assert.Equal(4, relevant.Count());

            // Should get all 5 as 10002 did, showing that it is 250 inclusive
            relevant = indelSource.GetRelevantIndels(10001);
            Assert.Equal(5, relevant.Count());

            // Should get the 9752 and the 9800
            relevant = indelSource.GetRelevantIndels(9700);
            Assert.Equal(2, relevant.Count());

            // Not close enough to anything
            relevant = indelSource.GetRelevantIndels(9500);
            Assert.Equal(0.0, relevant.Count());

            relevant = indelSource.GetRelevantIndels(0);
            Assert.Equal(1.0, relevant.Count());

            relevant = indelSource.GetRelevantIndels(100000);
            Assert.Equal(0.0, relevant.Count());

        }
    }
}