using System.Collections.Generic;
using System.Linq;
using Gemini.CandidateIndelSelection;
using Gemini.Models;
using Gemini.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class HashableIndelSourceTests
    {
        private HashableIndel CheckForIndel(List<HashableIndel> hashableIndels, int expRefPosition, string expRefAllele, string expAlt1, int expScore1)
        {
            var result = hashableIndels.Where(x =>
                x.ReferencePosition == expRefPosition && x.ReferenceAllele == expRefAllele && x.AlternateAllele == expAlt1 &&
                x.Score == expScore1);
            Assert.Single(result);
            return result.First();
        }

        private void EnsureIndelNotPresent(List<HashableIndel> hashableIndels, int expRefPosition, string s, string expAlt1)
        {
            var result = hashableIndels.Where(x =>
                x.ReferencePosition == expRefPosition && x.ReferenceAllele == s && x.AlternateAllele == expAlt1);
            Assert.Empty(result);
        }

        [Fact]
        public void GetFinalIndelsForChromosome()
        {
            var preIndels = new List<PreIndel>();
            var insertion1 = new PreIndel(new CandidateAllele("chr1", 100, "N", "NGA", AlleleCategory.Insertion));
            insertion1.Score = 100;
            var deletion = new PreIndel(new CandidateAllele("chr1", 5, "NNNN", "N", AlleleCategory.Deletion));
            deletion.Score = 100;
            var insertionSimilarToIns1 = new PreIndel(new CandidateAllele("chr1", 100, "N", "NGC", AlleleCategory.Insertion));
            insertionSimilarToIns1.Score = 20;
            var insertion2 = new PreIndel(new CandidateAllele("chr1", 302, "N", "NTCATCA", AlleleCategory.Insertion));
            insertion2.Score = 100;
            var insertionSimilarConsequenceToIns2 = new PreIndel(new CandidateAllele("chr1", 305, "N", "NTCATGA", AlleleCategory.Insertion));
            insertionSimilarConsequenceToIns2.Score = 20;
            var insertionNotSimilarEnoughConsequenceToIns2 = new PreIndel(new CandidateAllele("chr1", 305, "N", "NTCAGTA", AlleleCategory.Insertion));
            insertionNotSimilarEnoughConsequenceToIns2.Score = 20;
            var insertionContainingInsertion2 = new PreIndel(new CandidateAllele("chr1", 302, "N", "NTCATCATCATCA", AlleleCategory.Insertion));
            insertionContainingInsertion2.Score = 20;
            // TODO add edge cases in terms of score, negative cases in terms of diffferent variant types

            preIndels = new List<PreIndel>(){deletion, insertion1, insertionSimilarToIns1,
                insertion2, insertionSimilarConsequenceToIns2, insertionNotSimilarEnoughConsequenceToIns2,
                insertionContainingInsertion2 };

            // insertionSimilarToIns1 is removed for being very similar to insertion 1 and much lower quality
            // insertionSimilarConsequenceToIns2 is removed for having almost the exact same consequence as insertion 2 and much lower quality
            // insertionNotSimilarEnoughConsequenceToIns2 is pretty close to insertion 2 in terms of consequence, and weaker, but not similar enough, so can stay
            // insertionContainingInsertion2 has exact same nearby consequence and position as insertion 2 but it is hard to call, being a long dup. so it gets to stay.

            var indelSource = new HashableIndelSource();
            var chrReference = new ChrReference() { FastaPath = "abc", Name = "chr1",
                Sequence = new string('A', 99) + new string('T', 5) + new string('C', 195) + 
                           //299
                           string.Join("",Enumerable.Repeat("TCA",20)) + new string('G',300) };
        
            var finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);

            // Rehydrate with reference sequence and keep the right ones
            Assert.Equal(5, finalIndels.Count);
            EnsureIndelNotPresent(finalIndels, insertionSimilarToIns1.ReferencePosition, "A", "AGC");
            EnsureIndelNotPresent(finalIndels, insertionSimilarConsequenceToIns2.ReferencePosition, "A", "ATCATGA");
            var ins1 = CheckForIndel(finalIndels, 100, "T", "TGA", 100);
            Assert.False(ins1.IsDuplication);
            Assert.False(ins1.IsRepeat);
            var del = CheckForIndel(finalIndels, 5, "AAAA", "A", 100);
            Assert.False(del.IsDuplication);
            Assert.True(del.IsRepeat);
            var ins2 = CheckForIndel(finalIndels, 302, "A", "ATCATCA", 100);
            Assert.True(ins2.IsRepeat);
            Assert.True(ins2.IsDuplication);
            var ins2NotSimilarEnough = CheckForIndel(finalIndels, 305, "A", "ATCAGTA", 20);
            Assert.True(ins2NotSimilarEnough.IsRepeat);
            Assert.False(ins2NotSimilarEnough.IsDuplication);
            var longerInsertion = CheckForIndel(finalIndels, 302, "A", "ATCATCATCATCA", 20);
            Assert.True(longerInsertion.IsRepeat);
            Assert.True(longerInsertion.IsDuplication);
            Assert.True(longerInsertion.HardToCall);

            // Should handle scenario of stutter
            //         012345678901234567890
            // ...CCCCCCGGGGGTTTTTAAAAATATATA
            //              *ins TGG
            //          *ins GGG
            // ...CCCCCCGGGGGTGGTTTTTAAAAATATATA
            // ...CCCCCCGGGGGGGGTTTTTAAAAATATATA
            var homopolymerIns = new PreIndel(new CandidateAllele("chr1", 300, "N", "NGGG", AlleleCategory.Insertion));
            homopolymerIns.Score = 100;
            var homopolymerInsWithStutter = new PreIndel(new CandidateAllele("chr1", 305, "N", "NTGG", AlleleCategory.Insertion));
            homopolymerInsWithStutter.Score = 10;
            preIndels = new List<PreIndel>() { homopolymerIns, homopolymerInsWithStutter };

            indelSource = new HashableIndelSource();
            chrReference = new ChrReference()
            {
                FastaPath = "abc",
                Name = "chr1",
                Sequence = new string('C', 300) + "GGGGGTTTTTAAAAATATATA" + new string('G', 300)
            };
            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(1, finalIndels.Count);

            //chr1: 125080780 N > NTTTGATTCCATTCGATGATCACTACATTCAGTTCCATTCAATGATGATTCCAACAGATTCCATTTGGTGACTCCATTCGATTCTATTCATTGATGATTCCA
            //chr1: 125080854 N > NATTCGATTCTATTCATTGATGATTCCATTTGATTCCATTCGATGATGACTGCCTTCAGTTCCATTCGGTGATGATTCCAACAGATTCCATTTGGTGACTCA
            var realLongIns1 = new PreIndel(new CandidateAllele("chr1", 780, "N", "NTTTGATTCCATTCGATGATCACTACATTCAGTTCCATTCAATGATGATTCCAACAGATTCCATTTGGTGACTCCATTCGATTCTATTCATTGATGATTCCA", AlleleCategory.Insertion));
            realLongIns1.Score = 100;
            var realLongIns2 = new PreIndel(new CandidateAllele("chr1", 854, "N", "NATTCGATTCTATTCATTGATGATTCCATTTGATTCCATTCGATGATGACTGCCTTCAGTTCCATTCGGTGATGATTCCAACAGATTCCATTTGGTGACTCA", AlleleCategory.Insertion));
            realLongIns2.Score = 20;
            preIndels = new List<PreIndel>(){ realLongIns1, realLongIns2 };

            indelSource = new HashableIndelSource();
            chrReference = new ChrReference()
            {
                FastaPath = "abc",
                Name = "chr1",
                Sequence = new string('A', 3000)
            };

            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(2, finalIndels.Count);

            // Long deletion - should adjust snippet width to accomodate
            var longDel1 = new PreIndel(new CandidateAllele("chr1", 100, new string('N', 200), "N", AlleleCategory.Deletion));
            longDel1.Score = 100;
            var longDel2 = new PreIndel(new CandidateAllele("chr1", 150, new string('N', 200), "N", AlleleCategory.Deletion));
            longDel2.Score = 20;
            preIndels = new List<PreIndel>() { longDel1, longDel2 };

            indelSource = new HashableIndelSource();
            chrReference = new ChrReference()
            {
                FastaPath = "abc",
                Name = "chr1",
                Sequence = new string('A', 100) + new string('T', 100) + new string('C', 1000)
            };

            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(2, finalIndels.Count);

            chrReference = new ChrReference()
            {
                FastaPath = "abc",
                Name = "chr1",
                Sequence = new string('A', 100) + new string('T', 500) + new string('C', 1000)
            };

            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(1, finalIndels.Count);


            //         012345678901234567890
            // ...CCCCCCGGGGGGGGAGGTTTTTAAAAATATATA
            // ...CCCCCC---GGGGGAGGTTTTTAAAAATATATA // del 1
            // ...CCCCCCGGGGGGGG---TTTTTAAAAATATATA // del 2
            // ...CCCCCCGGGGGGGGA---TTTTAAAAATATATA // del 3
            // ...CCCCCCGGGGGAGGTTTTTAAAAATATATA // effective 1
            // ...CCCCCCGGGGGGGGTTTTTAAAAATATATA // effective 2
            // ...CCCCCCGGGGGGGGATTTTAAAAATATATA // effective 3 - edit distance of 2 from eff1, 1 from eff2

            var homopolymerDel = new PreIndel(new CandidateAllele("chr1", 300, "NNNN", "N", AlleleCategory.Deletion));
            homopolymerDel.Score = 100;
            var homopolymerDelMuchWeakerOneMismatch = new PreIndel(new CandidateAllele("chr1", 308, "NNNN", "N", AlleleCategory.Deletion));
            homopolymerDelMuchWeakerOneMismatch.Score = 10;
            var homopolymerDelMuchWeakerTwoMismatch = new PreIndel(new CandidateAllele("chr1", 309, "NNNN", "N", AlleleCategory.Deletion));
            homopolymerDelMuchWeakerTwoMismatch.Score = 10;
            preIndels = new List<PreIndel>() { homopolymerDel, homopolymerDelMuchWeakerOneMismatch, homopolymerDelMuchWeakerTwoMismatch };

            indelSource = new HashableIndelSource();
            chrReference = new ChrReference()
            {
                FastaPath = "abc",
                Name = "chr1",
                Sequence = new string('C', 300) + "GGGGGGGGAGGTTTTTAAAAATATATA" + new string('G', 300)
            };
            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(2, finalIndels.Count);
            CheckForIndel(finalIndels, 300, "CGGG", "C", 100);
            EnsureIndelNotPresent(finalIndels, 308, "GAGG", "G");
            CheckForIndel(finalIndels, 309, "AGGT", "A", 10);

            // Same deletions but flip the scores -- The deletions have very similar consequences, but there is not a clear stronger deletion, which makes us less confident that these are mismatching versions of the same deletion. Keep all.
            homopolymerDelMuchWeakerTwoMismatch.Score = 60;
            homopolymerDelMuchWeakerOneMismatch.Score = 60;
            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(3, finalIndels.Count);
            CheckForIndel(finalIndels, 300, "CGGG", "C", 100);
            CheckForIndel(finalIndels, 308, "GAGG", "G", 60);
            CheckForIndel(finalIndels, 309, "AGGT", "A", 60);

            // Same deletions but flip the scores -- The strongest deletion is edit distance of 1 away from both of the others
            homopolymerDel.Score = 40;
            homopolymerDelMuchWeakerTwoMismatch.Score = 10;
            homopolymerDelMuchWeakerOneMismatch.Score = 100;
            finalIndels = indelSource.GetFinalIndelsForChromosome("chr1", preIndels, chrReference);
            Assert.Equal(1, finalIndels.Count);
            EnsureIndelNotPresent(finalIndels, 300, "CGGG", "C");
            CheckForIndel(finalIndels, 308, "GAGG", "G", 100);
            EnsureIndelNotPresent(finalIndels, 309, "AGGT", "A");

        }

        [Fact]
        public void GetHashableIndel()
        {
            var refSequence = "ZZXXXXXCAGCAGCAGCAGXYZ";

            var indel = new PreIndel(new CandidateAllele("chr1", 7, "XCAG", "X", AlleleCategory.Deletion));

            var genomeSnippet = new GenomeSnippet()
            {
                Chromosome = "chr1",
                Sequence = refSequence + "TTTTT",
                StartPosition = 0
            };

            var hashable = HashableIndelSource.GetHashableIndel(genomeSnippet, indel, 0, false);
            Assert.Equal("ZZXXXXX", hashable.RefPrefix);
            Assert.Equal("CAGCAGCAGX", hashable.RefSuffix);

            indel = new PreIndel(new CandidateAllele("chr1", 7, "X", "XCAG", AlleleCategory.Insertion));
            hashable = HashableIndelSource.GetHashableIndel(genomeSnippet, indel, 0, false);
            Assert.Equal("ZZXXXXX", hashable.RefPrefix);
            Assert.Equal("CAGCAGCAGC", hashable.RefSuffix);

        }
    }
}