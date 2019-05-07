using System;
using Gemini.IO;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Utlity;
using Xunit;

namespace Gemini.Tests
{
    public class GenomeSnippetSourceTests
    {
        [Fact]
        public void GetGenomeSnippet()
        {
            var genome = new Mock<IGenome>();
            genome.Setup(x => x.GetChrReference("chr1"))
                .Returns(new ChrReference() {Sequence = new string('A', 10) + new string('B', 50), Name = "chr1"});

            var snippetSource = new GenomeSnippetSource("chr1", genome.Object, 20, 0);
            var snippet = snippetSource.GetGenomeSnippet(5);

            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length); 
            Assert.Equal(new string('A',10) + new string('B',30), snippet.Sequence);
            Assert.Equal(0, snippet.StartPosition);

            // Confirm proper indexing - demonstrates why we need to have 0 as min snippet startposition
            var pair = TestHelpers.GetPair("10M", "10M");
            pair.Read1.Bases = "AAAAAAAABB";
            pair.Read1.Position = 5;
            var originalAlignmentSummary =
                Extensions.GetAlignmentSummary((new Read("chr1", pair.Read1)), snippet.Sequence, true, true, snippet.StartPosition);
            Assert.Equal(3, originalAlignmentSummary.NumMismatches);

            // Hitting up against the end of the chromosome
            snippet = snippetSource.GetGenomeSnippet(59);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(21, snippet.Sequence.Length);
            Assert.Equal(new string('B',21), snippet.Sequence);
            Assert.Equal(39, snippet.StartPosition);

            // Very beginning of the chromosome
            snippet = snippetSource.GetGenomeSnippet(0);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('A', 10) + new string('B', 30), snippet.Sequence);
            Assert.Equal(0, snippet.StartPosition);

            // Somewhere in the middle
            snippet = snippetSource.GetGenomeSnippet(30);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(10, snippet.StartPosition);

            // Off the end of the chromosome
            Assert.Throws<ArgumentException>(()=>snippetSource.GetGenomeSnippet(81));

            // Shouldn't have negative position
            Assert.Throws<ArgumentException>(() => snippetSource.GetGenomeSnippet(-1));
        }
    }

    public class ReusableGenomeSnippetSourceTests
    {
        [Fact]
        public void GetGenomeSnippet()
        {
            var genome = new Mock<IGenome>();
            genome.Setup(x => x.GetChrReference("chr1"))
                .Returns(new ChrReference() { Sequence = new string('A', 10) + new string('B', 50), Name = "chr1" });

            var baseSnippetSource = new GenomeSnippetSource("chr1", genome.Object, 20, 0);
            var snippetSource = new ReusableSnippetSource(baseSnippetSource, 10);
            var snippet = snippetSource.GetGenomeSnippet(5);

            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('A', 10) + new string('B', 30), snippet.Sequence);
            Assert.Equal(0, snippet.StartPosition);

            // Confirm proper indexing - demonstrates why we need to have 0 as min snippet startposition
            var pair = TestHelpers.GetPair("10M", "10M");
            pair.Read1.Bases = "AAAAAAAABB";
            pair.Read1.Position = 5;
            var originalAlignmentSummary =
                Extensions.GetAlignmentSummary((new Read("chr1", pair.Read1)), snippet.Sequence, true, true, snippet.StartPosition);
            Assert.Equal(3, originalAlignmentSummary.NumMismatches);

            // Hitting up against the end of the chromosome
            snippet = snippetSource.GetGenomeSnippet(59);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(21, snippet.Sequence.Length);
            Assert.Equal(new string('B', 21), snippet.Sequence);
            Assert.Equal(39, snippet.StartPosition);

            // Very beginning of the chromosome
            snippet = snippetSource.GetGenomeSnippet(0);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('A', 10) + new string('B', 30), snippet.Sequence);
            Assert.Equal(0, snippet.StartPosition);

            // Somewhere in the middle
            snippet = snippetSource.GetGenomeSnippet(30);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(10, snippet.StartPosition);

            // Use the same snippet if we're close enough
            snippet = snippetSource.GetGenomeSnippet(31);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(10, snippet.StartPosition);

            // Boundary: just within range to use the same snippet
            snippet = snippetSource.GetGenomeSnippet(39);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(10, snippet.StartPosition);

            // Need to move to new snippet
            snippet = snippetSource.GetGenomeSnippet(40);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(20, snippet.StartPosition);

            // Use the same snippet if we're close enough
            snippet = snippetSource.GetGenomeSnippet(39);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(20, snippet.StartPosition);

            // Use the same snippet if we're close enough
            snippet = snippetSource.GetGenomeSnippet(35);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(20, snippet.StartPosition);

            snippet = snippetSource.GetGenomeSnippet(31);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(20, snippet.StartPosition);

            snippet = snippetSource.GetGenomeSnippet(30);
            Assert.Equal("chr1", snippet.Chromosome);
            Assert.Equal(40, snippet.Sequence.Length);
            Assert.Equal(new string('B', 40), snippet.Sequence);
            Assert.Equal(10, snippet.StartPosition);

            // Off the end of the chromosome
            Assert.Throws<ArgumentException>(() => snippetSource.GetGenomeSnippet(81));

            // Shouldn't have negative position
            Assert.Throws<ArgumentException>(() => snippetSource.GetGenomeSnippet(-1));
        }
    }
}