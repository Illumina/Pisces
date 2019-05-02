using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.FromHygea;
using Gemini.Types;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using Xunit;

namespace Gemini.Tests
{
    public class SoftclipReapplierTests
    {
        private RealignmentResult GetResult(string cigar)
        {
            var result = new RealignmentResult()
            {
                Cigar = new CigarAlignment(cigar),
                IndelsAddedAt = new List<int>() { 8 },
                NifiedAt = new List<int>(),
                AcceptedIndels = new List<int>() { 0 },
                AcceptedIndelsInSubList = new List<int>() { 0 }
            };

            return result;
        }
        [Fact]
        public void ReapplySoftclips()
        {
            var reapplier = new SoftclipReapplier(true, false, false, false, false, true);
            var reapplierNonly = new SoftclipReapplier(true, true, false, false, false, true);
            var read = new Read("chr", new BamAlignment
            {
                Position = 20, // zero based
                CigarData = new CigarAlignment("10M"),
                Bases = "GTACGTACGT",
                Qualities = new byte[] { 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 }
            });

            var result = GetResult("8M2I");
            var snippet = new GenomeSnippet() {Chromosome = "chr1", Sequence = "GTACGTACGT", StartPosition = 20};
            reapplier.ReapplySoftclips(read, 0, 0, new PositionMap(new int[]{21,22,23,24,25,26,27,28,29,30}), result, snippet, 0, 0, new CigarAlignment("10M"));
            Assert.Equal("8M2I", result.Cigar.ToString());


            // reapply N softclips
            read = new Read("chr", new BamAlignment
            {
                Position = 22, // zero based
                CigarData = new CigarAlignment("2S8M"),
                Bases = "NNACGTACGT",
                Qualities = new byte[] { 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 }
            });
            // At this point, the position map doesn't include the Ns. They get re-added.
            result = GetResult("6M2I");
            reapplier.ReapplySoftclips(read, 2, 0, new PositionMap(new int[] { 23, 24, 25, 26, 27, 28, 29, 30 }), result, snippet, 2, 0, new CigarAlignment("10M"));
            Assert.Equal("2S6M2I", result.Cigar.ToString());

            // reapply non-N softclips
            read = new Read("chr", new BamAlignment
            {
                Position = 22, // zero based
                CigarData = new CigarAlignment("2S8M"),
                Bases = "CCACGTACGT",
                Qualities = new byte[] {20,20,20,20,20, 20, 20, 20, 20, 20 }
            });
            result = GetResult("8M2I");
            reapplier.ReapplySoftclips(read, 0, 0, new PositionMap(new int[] {21,22, 23, 24, 25, 26, 27, 28, 29, 30 }), result, snippet, 2, 0, new CigarAlignment("8M"));
            Assert.Equal("2S6M2I", result.Cigar.ToString());

            // if only remasking Ns, don't reapply non-N softclips
            result = GetResult("8M2I");
            reapplierNonly.ReapplySoftclips(read, 0, 0, new PositionMap(new int[] { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 }), result, snippet, 2, 0, new CigarAlignment("8M"));
            Assert.Equal("8M2I", result.Cigar.ToString());

            //// if the bases match, don't reapply softclips
            //read = new Read("chr", new BamAlignment
            //{
            //    Position = 22, // zero based
            //    CigarData = new CigarAlignment("2S8M"),
            //    Bases = "CTACGTACGT",
            //    Qualities = new byte[] { 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 }
            //});
            //result = GetResult("8M2I");
            //reapplier.ReapplySoftclips(read, 0, 0, new PositionMap(new int[] { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 }), result, snippet, 2, 0, new CigarAlignment("8M"));
            //Assert.Equal("1S7M2I", result.Cigar.ToString());

        }
    }
}