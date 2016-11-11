using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using VariantPhasing.Helpers;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using VariantPhasing.Tests.Models;
using Xunit;

namespace VariantPhasing.Tests.Helpers
{
    public class PhasedVariantExtractorTests
    {
        string referenceSequence = "AGAAGTACTCATTATCTGAGGAGCCGGTCACCTGTACCA";
        string chromosome = "chr13";

        [Fact]
        public void CheckInsertions()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };
            
            var neighborhoodDepthAtSites = new List<int> {100,200}; 
            var clusterCountsAtSites = new int[] {90, 190};

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "AGAAGTACTCATTATCTGA";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0,refsToRemove.Count);

            Assert.Equal("A", allele.Reference);
            Assert.Equal("AGAAGTACTCATTATCTGA", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);


            //check co-located insertions

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            clusterVariantSites[0].VcfAlternateAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "CGTA";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("C", allele.Reference);
            Assert.Equal("TGTA", allele.Alternate); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.Coordinate);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            //here we put the alleles in the wrong order with the insertion first.
            clusterVariantSites[0].VcfAlternateAllele = "CGTA";
            clusterVariantSites[1].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            //note that now the MNV and the position are wrong.
            //(they were correct in the previous example)
            //This demonstrates and assumption of the PhasedVariantExtractor.Extract
            //algorithm: the VS must be in order of their true position (first base of difference).
            Assert.Equal("C", allele.Reference);
            Assert.Equal("GTAT", allele.Alternate); //old bug. this used to come out as GTAT
            Assert.Equal(28608286, allele.Coordinate);
        }

        [Fact]
        public void CheckInsertionsWorkWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "AGAAGTACTCATTATCTGA";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            //Assert.Equal(0, refsToRemove.Count);
            Assert.Equal(1, refsToRemove.Count); //28608285, 90

            Assert.Equal("A", allele.Reference);
            Assert.Equal("AGAAGTACTCATTATCTGA", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);


            //check co-located insertions

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };

            clusterVariantSites[0].VcfReferenceAllele = "C";
            clusterVariantSites[1].VcfReferenceAllele = "C";

            clusterVariantSites[0].VcfAlternateAllele = "T";
            clusterVariantSites[1].VcfAlternateAllele = "CGTA";

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("C", allele.Reference);
            Assert.Equal("TGTA", allele.Alternate); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.Coordinate);

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
    neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);

            Assert.Equal("AG", allele.Reference);
            Assert.Equal("AGGTA", allele.Alternate); //this only comes out correct so long as the VS are ordered correctly in the list.
            Assert.Equal(28608285, allele.Coordinate);

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


            neighborhoodDepthAtSites = new List<int> { 100, 200, 100, 200, 200 };
            clusterCountsAtSites = new int[] { 90, 190, 20, 20, 20 };

            refsToRemove = PhasedVariantExtractor.Extract(
    out allele, clusterVariantSites, referenceSequence,
    neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);


            //referenceSequence = "AGAA-GT-ACTCATTATCTGAGGAGCCGGTCACCTGTACCA";

            //with insertions = "AGAA[GTA]GT[CATCAT]ACTCATTATCTGAGGAGCCGGTCACCTGTACCA";

            Assert.Equal(6, refsToRemove.Count);

            Assert.Equal("AGAAGT", allele.Reference);
            Assert.Equal("AGAAGTAGTCATCAT", allele.Alternate); 
            Assert.Equal(28608285, allele.Coordinate);

            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608285)  };
        }

        [Fact]
        public void CheckDeletionsWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AGAAGTACTCATTATCTGA";
            clusterVariantSites[0].VcfAlternateAllele = "A";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(1, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCATTATCTGA", allele.Reference);
            Assert.Equal("A", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);


            neighborhoodDepthAtSites = new List<int> { 100, 200, 100, 200 };
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
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(5, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCAT", allele.Reference);
            Assert.Equal("AGATA", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);
        }


        [Fact]
        public void CheckDeletions()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AGAAGTACTCATTATCTGA";
            clusterVariantSites[0].VcfAlternateAllele = "A";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);

            Assert.Equal("AGAAGTACTCATTATCTGA", allele.Reference);
            Assert.Equal("A", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

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


            neighborhoodDepthAtSites = new List<int> { 100, 200, 100, 200 };
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
               neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(18, refsToRemove.Count);
            Assert.Equal("TGGTACTCATTATCTGAGGATG", allele.Reference);
            Assert.Equal("GTACTCATTATCTGAGGA", allele.Alternate);
            Assert.Equal(176517100, allele.Coordinate);

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
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(18-12, refsToRemove.Count);
            Assert.Equal("GTCCGTATG", allele.Reference);
            Assert.Equal("CCGTA", allele.Alternate);
            Assert.Equal(176517113, allele.Coordinate);

        }


        [Fact]
        public void CheckSNVsWithAnchoring()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("A", allele.Reference);
            Assert.Equal("C", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

            //and example where the first VS is N
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);
            Assert.Equal(190, refsToRemove[28608285]);
            Assert.Equal(190, refsToRemove[28608286]);
            Assert.Equal("AGG", allele.Reference);
            Assert.Equal("AGT", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

            //an example where there are two real VS
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(1, refsToRemove.Count);
            Assert.Equal(140, refsToRemove[28608286]); // (190+90)/2
            Assert.Equal("AGG", allele.Reference);
            Assert.Equal("CGT", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

            //an example where there is one ref in between two real VS
            clusterVariantSites = new VariantSite[] {
                new VariantSite(28608285), new VariantSite(28608287),new VariantSite(28608288)  };

            neighborhoodDepthAtSites = new List<int> { 100, 200,300 };
            clusterCountsAtSites = new int[] { 90, 190, 20 };

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            clusterVariantSites[1].VcfReferenceAllele = "N";
            clusterVariantSites[1].VcfAlternateAllele = "N";

            clusterVariantSites[2].VcfReferenceAllele = "G";
            clusterVariantSites[2].VcfAlternateAllele = "T";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100, 28608285);

            Assert.Equal(2, refsToRemove.Count);
            Assert.Equal(55, refsToRemove[28608286]); // (90+20)/2
            Assert.Equal(55, refsToRemove[28608286]); // (90+20)/2
            Assert.Equal("AGAG", allele.Reference);
            Assert.Equal("CGAT", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

        }


        [Fact]
        public void CheckSNVs()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };
            
            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };
            
            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("A", allele.Reference);
            Assert.Equal("C", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);


            clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "G";
            clusterVariantSites[1].VcfAlternateAllele = "T";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("G", allele.Reference);
            Assert.Equal("T", allele.Alternate);
            Assert.Equal(28608287, allele.Coordinate);

            clusterVariantSites[0].VcfReferenceAllele = "A";
            clusterVariantSites[0].VcfAlternateAllele = "C";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(1, refsToRemove.Count);
            Assert.Equal("AGG", allele.Reference);
            Assert.Equal("CGT", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

        }

        [Fact]
        public void CheckMNVs()
        {
            var allele = new Pisces.Domain.Models.Alleles.CalledAllele();
            var clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };

            var neighborhoodDepthAtSites = new List<int> { 100, 200 };
            var clusterCountsAtSites = new int[] { 90, 190 };

            clusterVariantSites[0].VcfReferenceAllele = "AG";
            clusterVariantSites[0].VcfAlternateAllele = "CC";

            var refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);


            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("AG", allele.Reference);
            Assert.Equal("CC", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);


            clusterVariantSites = new VariantSite[] { 
                new VariantSite(28608285), new VariantSite(28608287)  };

            clusterVariantSites[1].VcfReferenceAllele = "GA";
            clusterVariantSites[1].VcfAlternateAllele = "TT";


            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20, 100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("GA", allele.Reference);
            Assert.Equal("TT", allele.Alternate);
            Assert.Equal(28608287, allele.Coordinate);

            clusterVariantSites[0].VcfReferenceAllele = "AG";
            clusterVariantSites[0].VcfAlternateAllele = "CC";

            refsToRemove = PhasedVariantExtractor.Extract(
                out allele, clusterVariantSites, referenceSequence,
                neighborhoodDepthAtSites, clusterCountsAtSites, chromosome, 20,100);

            Assert.Equal(0, refsToRemove.Count);
            Assert.Equal("AGGA", allele.Reference);
            Assert.Equal("CCTT", allele.Alternate);
            Assert.Equal(28608285, allele.Coordinate);

        }
  
    }
}
