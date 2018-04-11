using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic.TargetCalling;
using RealignIndels.Models;
using Pisces.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class IndelTargetFinderTests
    {
        private readonly string _chr = "chr1";
        private readonly string _refSeq = "ACGTACGTACGTACGT";

        [Fact]
        public void FindIndelsOnly()
        {
            // note the core extraction methods are tested by base class
            // test that only indels extracted and test that min basecall quality is correctly passed down

            // ref and mnv/snv do not produce candidates
            var nonIndelRead = GetBasicRead();  //reference
            ExecuteTest(nonIndelRead, list => Assert.False(list.Any()));

            nonIndelRead.BamAlignment.Bases = "TCGT"; // turn into snp
            ExecuteTest(nonIndelRead, list => Assert.False(list.Any()));

            nonIndelRead.BamAlignment.Position = 5; // turn into mnv by shifting 1 base
            ExecuteTest(nonIndelRead, list => Assert.False(list.Any()));

            // insertions - test in middle, at ends, with softclip
            var insertion = GetBasicRead("1M2I1M");
            ExecuteTest(insertion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Insertion, list[0].Type);
                Assert.Equal("ACG", list[0].AlternateAllele);
            });

            insertion = GetBasicRead("3I1M");
            ExecuteTest(insertion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Insertion, list[0].Type);
                Assert.Equal("TACG", list[0].AlternateAllele);
            });

            insertion = GetBasicRead("1M3I");
            ExecuteTest(insertion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Insertion, list[0].Type);
                Assert.Equal("ACGT", list[0].AlternateAllele);
            });

            insertion = GetBasicRead("1S2I1S");
            ExecuteTest(insertion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Insertion, list[0].Type);
                Assert.Equal("TCG", list[0].AlternateAllele);
            });

            // deletions - test in middle, with softclip
            var deletion = GetBasicRead("1M2D3M");
            ExecuteTest(deletion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Deletion, list[0].Type);
                Assert.Equal("ACG", list[0].ReferenceAllele);
            });

            deletion = GetBasicRead("1S4D3S");
            ExecuteTest(deletion, list =>
            {
                Assert.Equal(1, list.Count);
                Assert.Equal(AlleleCategory.Deletion, list[0].Type);
                Assert.Equal("TACGT", list[0].ReferenceAllele);
            });

            // min basecall quality applied
            insertion = GetBasicRead("1M2I1M");
            ExecuteTest(insertion, list => Assert.Equal(0, list.Count), 1);

            insertion.BamAlignment.Qualities[1] = 1;
            ExecuteTest(insertion, list => Assert.Equal(1, list.Count), 1);
        }

        private Read GetBasicRead(string cigar = "4M")
        {
            return new Read(_chr, new BamAlignment
            {
                Bases = "ACGT",
                Position = 4,
                CigarData = new CigarAlignment(cigar),
                Qualities = new byte[4]
            });
        }

        private void ExecuteTest(Read read, Action<List<CandidateIndel>> assertions, int minBasecallQuality = 0)
        {
            var finder = new IndelTargetFinder(minBasecallQuality);

            var result = finder.FindIndels(read, _refSeq, _chr);

            assertions(result);
        }
    }
}
