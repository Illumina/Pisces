using System;
using System.Collections.Generic;
using System.IO;
using Pisces.Domain.Models;
using Xunit;

namespace Pisces.IO.Tests
{
    public class BamFileExtractorTests
    {
        [Fact]
        public void ReadFile()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "small.bam");
            ReadFileTest(smallBam, 1000, false);

            var bwaXCbam = Path.Combine(TestPaths.LocalTestDataDirectory, "bwaXC.bam");
            var ex = Assert.Throws<InvalidDataException>(() => ReadFileTest(bwaXCbam, 4481 + 8171, true));
            Assert.Contains("CIGAR", ex.Message, StringComparison.CurrentCultureIgnoreCase);
        }

        private void ReadFileTest(string bamfile, int expectedReads, bool bamHasXc)
        {
            var extractor = new BamFileAlignmentExtractor(bamfile);

            var read = new Read();
            var lastPosition = -1;
            var numReads = 0;

            bool hasAnyStitchedCigars = false;
            while (extractor.GetNextAlignment(read))
            {
                Assert.True(read.Position >= lastPosition); // make sure reads are read in order
                Assert.False(string.IsNullOrEmpty(read.Name));
                Assert.False(string.IsNullOrEmpty(read.Chromosome));

                if (!bamHasXc) Assert.Equal(null, read.StitchedCigar);
                if (read.StitchedCigar != null && read.StitchedCigar.Count > 0) hasAnyStitchedCigars = true;
                lastPosition = read.Position;
                numReads++;
            }

            if (bamHasXc) Assert.True(hasAnyStitchedCigars);
            Assert.Equal(expectedReads, numReads);
            extractor.Dispose();

            // make sure can't read after dispose
            Assert.Throws<IOException>(() => extractor.GetNextAlignment(read));
        }

        [Fact]
        [Trait("ReqID", "SDS-5")]
        [Trait("ReqID", "SDS-6")]
        public void Constructor()
        {
            var nonExistantBam = Path.Combine(TestPaths.LocalTestDataDirectory, "non_existant.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(nonExistantBam));

            var missingIndexBam = Path.Combine(TestPaths.LocalTestDataDirectory, "missing_bai.bam");

            Assert.Throws<ArgumentException>(() => new BamFileAlignmentExtractor(missingIndexBam));
        }

        [Fact]
        public void SanityCheckSequenceOrdering()
        {
            var smallBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
            var intervals = new Dictionary<string, List<Region>>();
            var chrIntervals = new List<Region>
            {
                new Region(28607838, 28607838),
                new Region(28608631, 28608631)
            };
            var expectedSQorder = new List<string> { "chr10", "chr11", "chr12", "chr13" }; //I dont know why it starts with 10, thats just how it is in the bam. thats what makes it a good test case.

            intervals.Add("chr13", chrIntervals);
            var extractor = new BamFileAlignmentExtractor(smallBam);
            List<string> sequencesInTheBamOrder = extractor.SourceReferenceList;

            Assert.Equal(expectedSQorder[0], sequencesInTheBamOrder[0]);
            Assert.Equal(expectedSQorder[1], sequencesInTheBamOrder[1]);
            Assert.Equal(expectedSQorder[3], sequencesInTheBamOrder[3]);
            Assert.Equal(25, sequencesInTheBamOrder.Count);

            //happyPath 
            Assert.False( extractor.SequenceOrderingIsNotConsistent(new List<string> {"chr1", "chr2" }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1", "chr3", "chr4" }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr14", "chr9" })); //only b/c the bam header is silly.

            //not OK
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr2", "chr1" }));
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr9", "chr14" }));
            Assert.True(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr22", "chr21" }));

            //genome has chr not in bam, be ok with it
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1", "chrMotherGoose" }));

            //bam has chr not in genome, be ok with it
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> { "chr1" }));

            //empty lists
            Assert.False(extractor.SequenceOrderingIsNotConsistent(new List<string> {  }));
            Assert.False(extractor.SequenceOrderingIsNotConsistent(null));

        }

        [Fact]
        public void UnalignedReads()
        {
            var extractor = new BamFileAlignmentExtractor(Path.Combine(TestPaths.LocalTestDataDirectory, "unaligned.bam"));

            var read = new Read();
            var count = 0;
            while (extractor.GetNextAlignment(read))
            {
                count++;
            }

            Assert.Equal(138826, count);
            Assert.Equal(null, read.Chromosome); // last reads are unaligned
        }

        [Fact]
        public void TestIfBamIsStitched()
        {
            //test some generic bam
            var extractor = new BamFileAlignmentExtractor(Path.Combine(TestPaths.LocalTestDataDirectory, "unaligned.bam"));
            Assert.Equal(false, extractor.SourceIsStitched);


            //test to be robust to crazy bams.

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(""));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched("@PG @PG"));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched("blah"));

            Assert.Equal(false,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(null));

            //test some real normal headers

            Assert.Equal(true,
            BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(GetPiscesStitchedHeader()));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenStitched(GetRegularHeader()));
        }

        [Fact]
        public void TestIfBamIsCollapsed()
        {
            //test some generic bam
            var extractor = new BamFileAlignmentExtractor(Path.Combine(TestPaths.LocalTestDataDirectory, "unaligned.bam"));
            Assert.Equal(false, extractor.SourceIsStitched);
            Assert.Equal(false, extractor.SourceIsCollapsed);

            //test to be robust to crazy bams.
            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed(""));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed("@PG @PG"));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed("blah"));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed(null));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed("z@PG PN:Reco"));

            Assert.Equal(false,
                    BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed("@PG\n PN:Reco"));
             
            Assert.Equal(true, 
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed("@PG PN:Reco"));
            //test some real normal headers
            Assert.Equal(true,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed(GetRecoCollapsedHeader()));

            Assert.Equal(false,
                BamFileAlignmentExtractor.CheckIfBamHasBeenCollapsed(GetRegularHeader()));
        }

        public string GetRegularHeader()
        {
            return
            @"@HD VN:1.4 SO:coordinate
@PG ID: Isis PN:Isis VN:2.4.61.97
@SQ SN:chrM LN:16571 M5:
            d2ed829b8a1628d16cbeee88e88e39eb
@SQ SN: chr1 LN:249250621 M5: 1b22b98cdeb4a9304cb5d48026a85128
@SQ SN: chr2 LN:243199373 M5:
            a0d9851da00400dec1098a9255ac712e
..
@SQ SN: chr21 LN:48129895 M5: 2979a6085bfe28e3ad6f552f361ed74d
@SQ SN: chr22 LN:51304566 M5:
            a718acaa6135fdca8357d5bfe94211dd
@SQ SN: chrX LN:155270560 M5: 7e0e2e580297b7764e31dbc80c2540dd
@SQ SN: chrY LN:59373566 M5: 1fa3474750af0948bdf97d5a0ee52e51
@RG ID: AMHS - MixB - 22030 PL: ILLUMINA SM:AMHS - MixB - 22030";

        }

        public string GetPiscesStitchedHeader()
        {
            return
            @"@HD VN:1.4 SO:coordinate
@PG ID: Isis PN:Isis VN:2.4.61.97
@PG ID:Pisces PN:Stitcher VN:5.1.5.2
@SQ SN:chrM LN:16571 M5:
            d2ed829b8a1628d16cbeee88e88e39eb
@SQ SN: chr1 LN:249250621 M5: 1b22b98cdeb4a9304cb5d48026a85128
@SQ SN: chr2 LN:243199373 M5:
            a0d9851da00400dec1098a9255ac712e
..
@SQ SN: chr21 LN:48129895 M5: 2979a6085bfe28e3ad6f552f361ed74d
@SQ SN: chr22 LN:51304566 M5:
            a718acaa6135fdca8357d5bfe94211dd
@SQ SN: chrX LN:155270560 M5: 7e0e2e580297b7764e31dbc80c2540dd
@SQ SN: chrY LN:59373566 M5: 1fa3474750af0948bdf97d5a0ee52e51
@RG ID: AMHS - MixB - 22030 PL: ILLUMINA SM:AMHS - MixB - 22030";

        }

        public string GetRecoCollapsedHeader()
        {
            return
                "@HD\tVN: 1.3\tSO: coordinate\n " +
                "@SQ\tSN: chrM\tLN: 16571\n " +
                "@SQ\tSN: chr1\tLN: 249250621\n " +
                "@SQ\tSN: chr2\tLN: 243199373\n " +
                "@SQ\tSN: chr3\tLN: 198022430\n " +
                "@SQ\tSN: chr4\tLN: 191154276\n " +
                "@SQ\tSN: chr5\tLN: 180915260\n " +
                "@SQ\tSN: chr6\tLN: 171115067\n " +
                "@SQ\tSN: chr7\tLN: 159138663\n " +
                "@SQ\tSN: chr8\tLN: 146364022\n " +
                "@SQ\tSN: chr9\tLN: 141213431\n " +
                "@SQ\tSN: chr10\tLN: 135534747\n " +
                "@SQ\tSN: chr11\tLN: 135006516\n " +
                "@SQ\tSN: chr12\tLN: 133851895\n " +
                "@SQ\tSN: chr13\tLN: 115169878\n " +
                "@SQ\tSN: chr14\tLN: 107349540\n " +
                "@SQ\tSN: chr15\tLN: 102531392\n " +
                "@SQ\tSN: chr16\tLN: 90354753\n " +
                "@SQ\tSN: chr17\tLN: 81195210\n " +
                "@SQ\tSN: chr18\tLN: 78077248\n " +
                "@SQ\tSN: chr19\tLN: 59128983\n " +
                "@SQ\tSN: chr20\tLN: 63025520\n " +
                "@SQ\tSN: chr21\tLN: 48129895\n " +
                "@SQ\tSN: chr22\tLN: 51304566\n " +
                "@SQ\tSN: chrX\tLN: 155270560\n " +
                "@SQ\tSN: chrY\tLN: 59373566\n " +
                "@RG\tID: Sample_71 - 100\tPL: ILLUMINA\tSM: Sample_71 - 100\n " +
                "@PG\tID: bwa\tPN: bwa\tCL:/ grail / scratch / OncoRC / Napa / Analysis / Experiments / 160317_lychee_test_data / 1.1.6.295_218UMI / bin / Dependencies / BWA / bwa mem - M - t 40 - R @RG\\tID: Sample_71 - 100\\tPL: ILLUMINA\\tSM: Sample_71 - 100 / illumina / sync / igenomes / Homo_sapiens / UCSC / hg19 / Sequence / BWAIndex / genome.fa / grail / scratch / OncoRC / Napa / Analysis / Experiments / 160317_lychee_test_data / 1.1.6.295_218UMI / HiSeqX1 / Analysis / Alignment / Staging / Sample_71 - 100 / Sample_71 - 100_S1_R1_001.fastq.gz / grail / scratch / OncoRC / Napa / Analysis / Experiments / 160317_lychee_test_data / 1.1.6.295_218UMI / HiSeqX1 / Analysis / Alignment / Staging / Sample_71 - 100 / Sample_71 - 100_S1_R2_001.fastq.gz\tVN: 0.7.12 - r1039\n" +
                "@PG\tID: Reco PN:Reco VN:1.0.0.0 CL:bla";
        }
    }
}
