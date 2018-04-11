using VariantPhasing.Logic;
using VariantPhasing.Models;
using Xunit;

namespace VariantPhasing.Tests.Helpers
{
    public class PhasedVariantExtractorTests
    {
        string referenceSequence = "AGAAGTACTCATTATCTGAGGAGCCGGTCACCTGTACCA";
        string chromosome = "chr13";


        //This is an example where the true ref allele is effective empty after trimming for parsimony
        //(PICS-929 bug). Used to give: ArgumentOutOfRangeException : Length cannot be less than zero.
        [Fact]
        public void CheckInsertionsInHomopolymerStretches()
        {
            //(1) The exact case of the original bug

            string referenceSequenceWithRepeats = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT";
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608288) , new VariantSite(28608289)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "T";
            clusterVariantSites[0].VcfAlternateAllele = "T";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "TTTT";

            clusterVariantSites[2].VcfReferenceAllele = "T";
            clusterVariantSites[2].VcfAlternateAllele = "TTTTTTT";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequenceWithRepeats,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("T", allele.ReferenceAllele);
            Assert.Equal("TTTTTTTTTT", allele.AlternateAllele);
            Assert.Equal(28608288, allele.ReferencePosition);

            //(2) A similar, contrived case (N's instead of ref) that would cause the problem.
 
            clusterVariantSites[0].VcfReferenceAllele = "N";
            clusterVariantSites[0].VcfAlternateAllele = "N";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "TTTT";

            clusterVariantSites[2].VcfReferenceAllele = "T";
            clusterVariantSites[2].VcfAlternateAllele = "TTTTTTT";

             refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequenceWithRepeats,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("T", allele.ReferenceAllele);
            Assert.Equal("TTTTTTTTTT", allele.AlternateAllele);
            Assert.Equal(28608288, allele.ReferencePosition);

            //(3) Another contrived case that would cause the problem.

            clusterVariantSites[0].VcfReferenceAllele = "G";
            clusterVariantSites[0].VcfAlternateAllele = "GT";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "TTTT";

            clusterVariantSites[2].VcfReferenceAllele = "T";
            clusterVariantSites[2].VcfAlternateAllele = "TTTTTTT";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("T", allele.ReferenceAllele);
            Assert.Equal("TTTTTTTTTTT", allele.AlternateAllele); // <- (note, alt alt allele now has one extra T)
            Assert.Equal(28608285, allele.ReferencePosition);    // left shifting, all the insetion joins to the first variant

            //(4) A case that would NOT cause the problem. (the A insertion doesnt make the repeat section
            //in the reference sequence, so that saves it.

            clusterVariantSites[0].VcfReferenceAllele = "G";
            clusterVariantSites[0].VcfAlternateAllele = "GA";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "TTTT";

            clusterVariantSites[2].VcfReferenceAllele = "T";
            clusterVariantSites[2].VcfAlternateAllele = "TTTTTTT";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("T", allele.ReferenceAllele);
            Assert.Equal("TATTTTTTTTT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
                    
            //(5) Another case that might cause the problem

            clusterVariantSites[0].VcfReferenceAllele = "TTT";
            clusterVariantSites[0].VcfAlternateAllele = "T";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "TTTT";

            clusterVariantSites[2].VcfReferenceAllele = "T";
            clusterVariantSites[2].VcfAlternateAllele = "TTTTTTT";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("T", allele.ReferenceAllele);
            Assert.Equal("TTTTTTTT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
        }

        [Fact]
        public void CheckInsertions()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "AGAAGTACTCATTATCTGA";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("AGAAGTACTCATTATCTGA", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);


            //check co-located insertions

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            clusterVariantSites[0].VcfAlternateAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "CGTA";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("C", allele.ReferenceAllele);
            Assert.Equal("TGTA", allele.AlternateAllele); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.ReferencePosition);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            //here we put the alleles in the wrong order with the insertion first.
            clusterVariantSites[0].VcfAlternateAllele = "CGTA";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            //note that now the MNV and the position are wrong.
            //(they were correct in the previous example)
            //This demonstrates and assumption of the PhasedVariantExtractor.Extract
            //algorithm: the VS must be in order of their true position (first base of difference).

            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("AGTA", allele.AlternateAllele); //old bug.
            Assert.Equal(28608285, allele.ReferencePosition);

            //check colocated insertions with repeats inside them 

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfReferenceAllele = "T";

            //here we put the alleles in the wrong order with the insertion first.
            clusterVariantSites[0].VcfAlternateAllele = "TTTTTT";
            clusterVariantSites[1].VcfAlternateAllele = "TTTTTTTTT";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            //note that now the MNV and the position are wrong.
            //(they were correct in the previous example)
            //This demonstrates and assumption of the PhasedVariantExtractor.Extract
            //algorithm: the VS must be in order of their true position (first base of difference).

            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("ATTTTTTTTTTTTT", allele.AlternateAllele); 
            Assert.Equal(28608285, allele.ReferencePosition);

            //
            //(6) Check insertions with ambigous trimming on each side
            //This example creates a G-> GGAAGGG allele
            //that trims to {} -> GGAAGG allele
            //And then the reference "A" gets repadded.

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608286)  };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "AGGAA";
         
            //here we put the alleles in the wrong order with the insertion first.
            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "GGG";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            //note that now the MNV and the position are wrong.
            //(they were correct in the previous example)
            //This demonstrates and assumption of the PhasedVariantExtractor.Extract
            //algorithm: the VS must be in order of their true position (first base of difference).

            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("AGGAAGG", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
        }

        [Fact]
        public void CheckInsertionsWorkWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "AGAAGTACTCATTATCTGT";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            //Assert.Equal(0, refsToRemove.Count);
            Assert.Equal(1, refsToRemove.Count); //28608285, 90

            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("AGAAGTACTCATTATCTGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);


            //check co-located insertions

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            clusterVariantSites[0].VcfAlternateAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "CGTA";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("C", allele.ReferenceAllele);
            Assert.Equal("TGTA", allele.AlternateAllele); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.ReferencePosition);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";


            //check co-located insertions

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608286)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfAlternateAllele = "CGTA";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);

            Assert.Equal("AG", allele.ReferenceAllele);
            Assert.Equal("AGGTA", allele.AlternateAllele); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.ReferencePosition);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };


            //check a mix of insertions and references

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608286),
                new VariantSite(28608288), new VariantSite(28608290) , new VariantSite(28608291)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfAlternateAllele = "C";

            clusterVariantSites[2].VcfReferenceAllele = "C";
            clusterVariantSites[2].VcfAlternateAllele = "CGTA";

            clusterVariantSites[3].VcfReferenceAllele = "C";
            clusterVariantSites[3].VcfAlternateAllele = "CCATCAT";

            clusterVariantSites[4].VcfReferenceAllele = "C";
            clusterVariantSites[4].VcfAlternateAllele = "C";


            neighborhoodDepthAtSites = new int[] { 100, 200, 100, 200, 200 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0, 0, 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190, 20, 20, 20 };

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);


            //referenceSequence = "AGAA-GT-ACTCATTATCTGAGGAGCCGGTCACCTGTACCA";

            //with insertions = "AGAA[GTA]GT[CATCAT]ACTCATTATCTGAGGAGCCGGTCACCTGTACCA";

            Assert.Equal(6, refsToRemove.Count);

            Assert.Equal("AGAAG", allele.ReferenceAllele);
            Assert.Equal("AGAAGTAGTCATCA", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };
        }

        [Fact]
        public void CheckDeletionsWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AGAAGTACTCATTATCTGT";
            clusterVariantSites[0].VcfAlternateAllele = "A";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(1, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCATTATCTGT", allele.ReferenceAllele);
            Assert.Equal("A", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);


            neighborhoodDepthAtSites = new int[] { 100, 200, 100, 200 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0, 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190, 10, 20 };
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287),
                    new VariantSite(28608288), new VariantSite(28608291)};


            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "A";

            clusterVariantSites[1].VcfReferenceAllele = "AAG";
            clusterVariantSites[1].VcfAlternateAllele = "A";

            clusterVariantSites[2].VcfReferenceAllele = "A";
            clusterVariantSites[2].VcfAlternateAllele = "A";

            clusterVariantSites[3].VcfReferenceAllele = "ACTCAT";
            clusterVariantSites[3].VcfAlternateAllele = "A";

            // referenceSequence = "AGA[AG]TA[CTCAT]TATCTGAGGAGCCGGTCACCTGTACCA";
            // altSequence = "AGA[XX]TA[XXXXX]TATCTGAGGAGCCGGTCACCTGTACCA";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(5, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCAT", allele.ReferenceAllele);
            Assert.Equal("AGATA", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
        }


        [Fact]
        public void CheckDeletions()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AGAAGTACTCATTATCTGA";
            clusterVariantSites[0].VcfAlternateAllele = "A";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCATTATCTGA", allele.ReferenceAllele);
            Assert.Equal("A", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            // testing a real bug
            // G>G T>T TTG>T ATG>A .
            // mnv accepted:	chr5    176517113.GTCCGTATG   CCGTA.

            /*
            chr5    176517099.T   TTG 86  
            chr5    176517099.TTG T   55  
            chr5    176517100.T.  
            chr5    176517101.G.   100 PASS DP = 298  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:283:298:0.0503:20:-100.0000:0.0165
            chr5    176517102.T.   100 PASS DP = 296  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:294:296:0.0068:20:-100.0000:0.0199
            chr5    176517103.G.   100 PASS DP = 302  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:301:302:0.0033:20:-100.0000:0.0098
            chr5    176517104.T.   100 PASS DP = 295  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:293:295:0.0068:20:-100.0000:0.0232
            chr5    176517105.G.   100 PASS DP = 297  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:297:297:0.0000:20:-100.0000:0.0166
            chr5    176517106.T.   100 PASS DP = 294  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:293:294:0.0034:20:-100.0000:0.0265
            chr5    176517107.G.   100 PASS DP = 301  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:301:301:0.0000:20:-100.0000:0.0033
            chr5    176517108.T.   100 PASS DP = 293  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:293:293:0.0000:20:-100.0000:0.0298
            chr5    176517109.G.   100 PASS DP = 301  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:301:301:0.0000:20:-100.0000:0.0066
            chr5    176517110.T.   100 PASS DP = 287  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:286:287:0.0035:20:-100.0000:0.0559
            chr5    176517111.G.   100 PASS DP = 300  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:298:300:0.0067:20:-100.0000:0.0066
            chr5    176517112.T.   100 PASS DP = 293  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:292:293:0.0034:20:-100.0000:0.0201
            chr5    176517113.G.   100 PASS DP = 289  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:288:289:0.0035:20:-100.0000:0.0137
            chr5    176517114.T.   100 PASS DP = 280  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:279:280:0.0036:20:-100.0000:0.0378
            chr5    176517115.C.   100 PASS DP = 257  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:255:257:0.0078:20:-100.0000:0.1076
            chr5    176517116.C.   100 LowDP DP = 222  GT: GQ: AD: DP: VF: NL: SB: NC./.:100:220:222:0.0090:20:-100.0000:0.1898
            chr5    176517117.G.   100 PASS DP = 262  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:262:262:0.0000:20:-100.0000:0.0260
            chr5    176517118.T.   100 PASS DP = 257  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 0:100:257:257:0.0000:20:-100.0000:0.0410
            chr5    176517119.ATG A   64  PASS DP = 251  GT: GQ: AD: DP: VF: NL: SB: NC 0 / 1:64:237,14:251:0.0558:20:-20.0580:0.0000
            */

            clusterVariantSites = new VariantSite[] {
                new VariantSite(176517098), new VariantSite(176517099),
                new VariantSite(176517099), new VariantSite(176517119)  };


            neighborhoodDepthAtSites = new int[] { 100, 200, 100, 200 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0, 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190, 90, 90 };

            clusterVariantSites[0].VcfReferenceAllele = "G";
            clusterVariantSites[0].VcfAlternateAllele = "G";

            clusterVariantSites[1].VcfReferenceAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTG";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            clusterVariantSites[3].VcfReferenceAllele = "ATG";
            clusterVariantSites[3].VcfAlternateAllele = "A";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequence,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(18, refsToRemove.Count);
            Assert.Equal("TGGTACTCATTATCTGAGGATG", allele.ReferenceAllele);
            Assert.Equal("GTACTCATTATCTGAGGA", allele.AlternateAllele);
            Assert.Equal(176517100, allele.ReferencePosition);

            //now, suppose we had  7x"TG" + "TCCGT" in between, instead of "R"
            string realReferenceSequence = "GTTGTGTGTGTGTGTG" + "TCCGT" + "ATG";

            //the ref would be like this: "TGTGTGTGTGTGTGTCCGTATG"
            //the alt would be like this: "TGTGTGTGTGTGTCCGTA"
            //starting at position  176517100

            //but Scylla would clean it up
            //the ref would be like this: "-------------GTCCGTATG"
            //the alt would be like this: "------------CCGTA"
            //starting at position  176517100 + 1(mnv style reporting) + 12 (where the alt agreed with the reference)

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, realReferenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(18 - 12, refsToRemove.Count);
            Assert.Equal("GTCCGTATG", allele.ReferenceAllele);
            Assert.Equal("CCGTA", allele.AlternateAllele);
            Assert.Equal(176517113, allele.ReferencePosition);

        }

        //We had a bug with homopolymer regions and insertions (PICS-929). This is a test to make sure nothing 
        // similar can happen with deletions
        [Fact]
        public void CheckDeletionsInHomopolymerStretches()
        {
            //(1) 

            string referenceSequenceWithRepeats = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT";
            //Where deletions occure:           = "TTTTXXXTTTXXXXXXTTTTTTTTTTTTTTTTTTTTTT";
            //Expected result:                  = TTTTTTTTTT -> T

            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608288) , new VariantSite(28608294)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "T";
            clusterVariantSites[0].VcfAlternateAllele = "T";

            clusterVariantSites[1].VcfReferenceAllele = "TTTT";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTTTTTT";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequenceWithRepeats,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("TTTTTTTTTT", allele.ReferenceAllele);
            Assert.Equal("T", allele.AlternateAllele);
            Assert.Equal(28608288, allele.ReferencePosition);

            //(2) A similar, contrived case (N's instead of ref) that would cause the problem.

            clusterVariantSites[0].VcfReferenceAllele = "N";
            clusterVariantSites[0].VcfAlternateAllele = "N";

            clusterVariantSites[1].VcfReferenceAllele = "TTTT";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTTTTTT";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("TTTTTTTTTT", allele.ReferenceAllele);
            Assert.Equal("T", allele.AlternateAllele);
            Assert.Equal(28608288, allele.ReferencePosition);

            //(3) 

            clusterVariantSites[0].VcfReferenceAllele = "G";
            clusterVariantSites[0].VcfAlternateAllele = "GT";

            clusterVariantSites[1].VcfReferenceAllele = "TTTT";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTTTTTT";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("TTTTTTTTT", allele.ReferenceAllele);// <- (note, ref allele now has one less T)
            Assert.Equal("T", allele.AlternateAllele);// 
            Assert.Equal(28608285, allele.ReferencePosition);    // left shifting, all the insetion joins to the first variant

            //(4) 

            clusterVariantSites[0].VcfReferenceAllele = "G";
            clusterVariantSites[0].VcfAlternateAllele = "GA";

            clusterVariantSites[1].VcfReferenceAllele = "TTTT";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTTTTTT";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("TTTTTTTTT", allele.ReferenceAllele);
            Assert.Equal("A", allele.AlternateAllele);
            Assert.Equal(28608286, allele.ReferencePosition);

            //(5) 

            clusterVariantSites[0].VcfReferenceAllele = "T";
            clusterVariantSites[0].VcfAlternateAllele = "TTT";

            clusterVariantSites[1].VcfReferenceAllele = "TTTT";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            clusterVariantSites[2].VcfReferenceAllele = "TTTTTTT";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
               out allele, clusterVariantSites, referenceSequenceWithRepeats,
               neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal("TTTTTTTT", allele.ReferenceAllele);
            Assert.Equal("T", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
        }



        [Fact]
        public void CheckSNVsWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("C", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            //and example where the first VS is N
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);
            Assert.Equal(190, refsToRemove[28608285].Counts);
            Assert.Equal(190, refsToRemove[28608286].Counts);
            Assert.Equal("AGG", allele.ReferenceAllele);
            Assert.Equal("AGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            //an example where there are two real VS
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(1, refsToRemove.Count);
            Assert.Equal(140, refsToRemove[28608286].Counts); // (190+90)/2
            Assert.Equal("AGG", allele.ReferenceAllele);
            Assert.Equal("CGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            //an example where there is one ref in between two real VS
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287),new VariantSite(28608288)  };

            neighborhoodDepthAtSites = new int[] { 100, 200, 300 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190, 20 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "N";
            clusterVariantSites[1].VcfAlternateAllele = "N";

            clusterVariantSites[2].VcfReferenceAllele = "G";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);
            Assert.Equal(55, refsToRemove[28608286].Counts); // (90+20)/2
            Assert.Equal(55, refsToRemove[28608286].Counts); // (90+20)/2
            Assert.Equal("AGAG", allele.ReferenceAllele);
            Assert.Equal("CGAT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

        }


        [Fact]
        public void CheckSNVs()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 50, 100 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("A", allele.ReferenceAllele);
            Assert.Equal("C", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);
            Assert.Equal(100, allele.TotalCoverage);
            Assert.Equal(50, allele.NumNoCalls);
            Assert.Equal((1f / 3f), allele.FractionNoCalls);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("G", allele.ReferenceAllele);
            Assert.Equal("T", allele.AlternateAllele);
            Assert.Equal(28608287, allele.ReferencePosition);

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(1, refsToRemove.Count);
            Assert.Equal("AGG", allele.ReferenceAllele);
            Assert.Equal("CGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

        }

        [Fact]
        public void CheckMNVs()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AG";
            clusterVariantSites[0].VcfAlternateAllele = "CC";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("AG", allele.ReferenceAllele);
            Assert.Equal("CC", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);


            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "GA";
            clusterVariantSites[1].VcfAlternateAllele = "TT";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("GA", allele.ReferenceAllele);
            Assert.Equal("TT", allele.AlternateAllele);
            Assert.Equal(28608287, allele.ReferencePosition);

            clusterVariantSites[0].VcfReferenceAllele = "AG";
            clusterVariantSites[0].VcfAlternateAllele = "CC";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("AGGA", allele.ReferenceAllele);
            Assert.Equal("CCTT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

        }


        [Fact]
        public void CheckOverlappingMNVs()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287) , new VariantSite(28608287) };

            var neighborhoodDepthAtSites = new int[] { 100, 200, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AGG"; //5,6,7
            clusterVariantSites[0].VcfAlternateAllele = "CCT";


            clusterVariantSites[1].VcfReferenceAllele = "GGA"; //7,8,9
            clusterVariantSites[1].VcfAlternateAllele = "TTT";

            clusterVariantSites[2].VcfReferenceAllele = "A";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("AGGGA", allele.ReferenceAllele);
            Assert.Equal("CCTTT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);




        }



        /// <summary>
        /// This is from a real bug, PICS-645, where Scylla retured the following result, in error:
        /// chr3 41266136 . TCTCTG GAGTTG 92 PASS DP=338 GT:GQ:AD:DP:VF:NL:SB 
        /// Should have the same effact if anchored or not anchored.
        /// </summary>
        [Fact]
        public void CheckTrailingBasesGetRemoved()
        {
            //anchored

            int anchorPosition = 28608285;

            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "TCTCTG";
            clusterVariantSites[0].VcfAlternateAllele = "GAGTTG";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, anchorPosition);

            Assert.Equal("TCTC", allele.ReferenceAllele);
            Assert.Equal("GAGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            //not anchored

            anchorPosition = -1;

            allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            neighborhoodDepthAtSites = new int[] { 100, 200 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "TCTCTG";
            clusterVariantSites[0].VcfAlternateAllele = "GAGTTG";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, anchorPosition);

            Assert.Equal("TCTC", allele.ReferenceAllele);
            Assert.Equal("GAGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

        }


        /// <summary>
        /// Never had a bug with this, but seemed worth testing...
        /// Behavior will be different if anchored vs unanchored.
        /// </summary>
        [Fact]
        public void CheckPrecedingBasesGetRemoved()
        {
            //anchored

            int anchorPosition = 28608285;

            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "TCTC";
            clusterVariantSites[0].VcfAlternateAllele = "TCGT";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, anchorPosition);

            Assert.Equal("TCTC", allele.ReferenceAllele);
            Assert.Equal("TCGT", allele.AlternateAllele);
            Assert.Equal(28608285, allele.ReferencePosition);

            //not anchored

            anchorPosition = -1;

            allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            neighborhoodDepthAtSites = new int[] { 100, 200 };
            neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            clusterCountsAtSites = new int[] { 90, 190 };


            clusterVariantSites[0].VcfReferenceAllele = "TCTC";
            clusterVariantSites[0].VcfAlternateAllele = "TCGT";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, anchorPosition);

            Assert.Equal("TC", allele.ReferenceAllele);
            Assert.Equal("GT", allele.AlternateAllele);
            Assert.Equal(28608285+2, allele.ReferencePosition);
        }


        [Fact]
        public void CheckPrecedingAndTrailingBasesGetRemoved()
        {
            //anchored

            int anchorPosition = -1;

            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new int[] { 100, 200 };
            var neighborhoodNoCallsAtSites = new int[] { 0, 0 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "TCTCAAAAAACGT";
            clusterVariantSites[0].VcfAlternateAllele = "TCGTACGT";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, neighborhoodNoCallsAtSites, clusterCountsAtSites, chromosome, 20, 100, anchorPosition);

            Assert.Equal("TCAAAAA", allele.ReferenceAllele);
            Assert.Equal("GT", allele.AlternateAllele);
            Assert.Equal(28608285+2, allele.ReferencePosition);

           
        }

    }
}