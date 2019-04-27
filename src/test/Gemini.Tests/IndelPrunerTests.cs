using System.Collections.Generic;
using Gemini.CandidateIndelSelection;
using Gemini.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Gemini.Tests
{
    public class IndelPrunerTests
    {
        [Fact]
        public void GetPrunedPreIndelsForChromosome_ConcurrentIndels()
        {
            // 3 small indels, same position, same score
            var preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(123, "A", "ATC", 5),
                GetIndel(123, "A", "ATG", 5),
                GetIndel(123, "A", "ATT", 5),
            };

            var filterer = new IndelPruner(false,0);
            var pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(3, pruned.Count);


            // 3 long concurrent insertions, one has a better score than the others. take that one, and up the score
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(123, "A", "ATCGTTGTTGT", 6),
                GetIndel(123, "A", "ATCTTTGTTGT", 5),
                GetIndel(123, "A", "ATTGTTGTTGT", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(1.0, pruned.Count);
            Assert.Equal(preIndelsRaw[0], pruned[0]);
            Assert.Equal(11, pruned[0].Score);


            // 3 long concurrent insertions, same score... can't choose a best one
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(123, "A", "ATCGTTGTTGT", 5),
                GetIndel(123, "A", "ATCGTTGTTGT", 5),
                GetIndel(123, "A", "ATTGTTGTTGT", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(3, pruned.Count);


            // 3 long concurrent insertions, two have the same high score... can't choose a best one
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(123, "A", "ATCGTTGTTGT", 10),
                GetIndel(123, "A", "ATCGTTGTTGT", 10),
                GetIndel(123, "A", "ATTGTTGTTGT", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(3, pruned.Count);

            // 3 shorter concurrent insertions, different scores, would have been able to choose a best one 
            // but they're too short to fall under concurrent insertions (>=10bp), and we're not doing bin filtering
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(123, "A", "ATCGTTGTTG", 5),
                GetIndel(123, "A", "ATCGTTGTTG", 5),
                GetIndel(123, "A", "ATTGTTGTTG", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(3, pruned.Count);

        }
     
        [Fact]
        public void GetPrunedPreIndelsForChromosome_BinFiltering()
        {
            // 3 indels very close to each other. One has significantly better score.
            var preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(122, "A", "ATG", 5),
                GetIndel(123, "A", "ATC", 11),
                GetIndel(124, "A", "ATT", 5),
            };

            var filterer = new IndelPruner(false, 1);
            var pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);

            // Remove the weaker nearby ones, but don't up the score
            Assert.Equal(1.0, pruned.Count);
            Assert.Equal(preIndelsRaw[1], pruned[0]);
            Assert.Equal(11, pruned[0].Score);

            // Nearby weak indel is longer than the strong one. Don't remove the weak one as it may have just been harder to call than the smaller one.
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(122, "A", "ATGA", 5),
                GetIndel(123, "A", "ATC", 11),
                GetIndel(124, "A", "ATT", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(2, pruned.Count);

            // One is not significantly better than the rest. Keep all.
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(122, "A", "ATG", 5),
                GetIndel(123, "A", "ATC", 10),
                GetIndel(124, "A", "ATT", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(3, pruned.Count);

            // One is not significantly better than the rest. Keep all.
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(122, "A", "ATG", 5),
                GetIndel(123, "A", "ATC", 10),
                GetIndel(124, "A", "ATT", 5),
                GetIndel(125, "A", "ATTG", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(4, pruned.Count);

            // 3 indels very close to each other. One has significantly better score
            // Get rid of the other nearby ones, but keep the one that is not within the bin
            preIndelsRaw = new List<PreIndel>()
            {
                GetIndel(122, "A", "ATG", 5),
                GetIndel(123, "A", "ATC", 11),
                GetIndel(124, "A", "ATT", 5),
                GetIndel(125, "A", "ATTG", 5),
            };

            pruned = filterer.GetPrunedPreIndelsForChromosome(preIndelsRaw);
            Assert.Equal(2, pruned.Count);

        }

        private PreIndel GetIndel(int pos, string refAllele, string altAllele, int score)
        {
            return new PreIndel(new CandidateAllele("chr1", pos, refAllele, altAllele, altAllele.Length > refAllele.Length ? AlleleCategory.Insertion : AlleleCategory.Deletion))
            {
                Score = score
            };
        }
    }
}