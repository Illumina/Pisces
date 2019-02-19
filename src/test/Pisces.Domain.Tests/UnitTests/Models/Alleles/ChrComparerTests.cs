using Pisces.Domain.Models.Alleles;
using System.Collections.Generic;
using System.Linq;
using Xunit;


namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class ChrComparerTests
    {
        List<string> _exampleHeaderLinesHG19WithDecoy = new List<string>() {
                        "##FORMAT=<ID=NL,Number=1,Type=Integer,Description=\"Applied BaseCall Noise Level\">",
                        "##FORMAT=<ID=SB,Number=1,Type=Float,Description=\"StrandBias Score\">",
                        "##contig=<ID=chr1,length=248956422>",
                        "##contig=<ID=chr2,length=242193529>",
                        "##contig=<ID=chr3,length=198295559>",
                        "##contig=<ID=chr4,length=190214555>",
                        "##contig=<ID=chr12,length=242193529>",
                        "##contig=<ID=chr10,length=198295559>",
                        "##contig=<ID=chr19,length=190214555>",
                        "##contig=<ID=chr21,length=46709983>",
                        "##contig=<ID=chr22,length=50818468>",
                        "##contig=<ID=chrX,length=156040895>",
                        "##contig=<ID=chrY,length=57227415>",
                        "##contig=<ID=chrM,length=16569>",
                        "##contig=<ID=GL000008.2,length=209709>",
                        "##contig=<ID=GL000009.2,length=201709>",
                        "##contig=<ID=GL000194.1,length=191469>",
                        "##contig=<ID=KI270755.1,length=36723>",
                        "##contig=<ID=KI270756.1,length=79590>",
                        "##contig=<ID=KI270757.1,length=71251>",
                        "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	HB04-01.asd.bam",
                        "chr1    817341  .   A   G   100 PASS    DP=44   GT:GQ:AD:DP:VF:NL:SB    1/1:2:0,44:44:1.000:23:-100.0000",
                        "chr1    817514  .   T   C   100 SB  DP=16   GT:GQ:AD:DP:VF:NL:SB    1/1:1:0,16:16:1.000:23:-100.0000" };

        List<string> _exampleHeaderLinesWithGRCh37 = new List<string>() {
                    "##FORMAT=<ID=NL,Number=1,Type=Integer,Description=\"Applied BaseCall Noise Level\">",
                    "##FORMAT=<ID=SB,Number=1,Type=Float,Description=\"StrandBias Score\">",
                    "##contig=<ID=1,length=249250621>",
                    "##contig=<ID=2,length=243199373>",
                    "##contig=<ID=4,length=191154276>",
                    "##contig=<ID=7,length=159138663>",
                    "##contig=<ID=9,length=141213431>",
                    "##contig=<ID=10,length=141213431>",
                    "##contig=<ID=11,length=135006516>",
                    "##contig=<ID=12,length=133851895>",
                    "##contig=<ID=13,length=115169878>",
                    "##contig=<ID=15,length=102531392>",
                    "##contig=<ID=17,length=81195210>",
                    "##contig=<ID=19,length=59128983>",
                    "##contig=<ID=20,length=63025520>",
                    "##contig=<ID=21,length=48129895>",
                    "##contig=<ID=22,length=48129895>",
                    "##contig=<ID=X,length=155270560>",
                    "##contig=<ID=Y,length=155270560>",
                    "##contig=<ID=M,length=155270560>",
                    "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	SRR5627630.bam.sorted.bam",
                    "1   115256530   .   G   .   100 PASS    DP=827  GT:GQ:AD:DP:VF:NL:SB    0/0:36:827:827:0.000:20:-100.0000" };

        List<string> _exampleHeaderLinesPathological = new List<string>() {
                    "##FORMAT=",
                    "##contig=<ID=frog,length=249250621>",
                    "##contig=<ID=frog,length=243199373>",
                    "##contig=<ID=9,length=191154276>",
                    "##contig=<ID=7,length=159138663>",
                    "##contig=<ID=2,length=141213431>",
                    "##contig=<ID=-2,length=141213431>",
                    "##contig=<ID=frog",
                    "",
                    "hi!" };


        [Fact]
       public void OrderChrs_FallBackSortTest()
        {
            bool isHGsort = false;
            bool isGRCHsort = false;

            //if NO ordering is given, we fall back to string sort
            TestExpectations(isHGsort, isGRCHsort, new ChrCompare( new List<string>() { }));
        }

        [Fact]
        public void OrderChrs_DefaultSortTest()
        {
            bool isHGsort = true;
            bool isGRCHsort = false;

            //be default, we use HG19 type ordering
            TestExpectations(isHGsort, isGRCHsort, new ChrCompare());
        }
        [Fact]
        public void OrderChrs_GRCH37Header()
        {
            bool isHG19sort = false;
            bool isGRCHsort = true;
            var orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesWithGRCh37);
            var chrCompare = new ChrCompare(orderedList);

            TestExpectations(isHG19sort, isGRCHsort, chrCompare);
        }


        [Fact]
        public void OrderChrs_HG19Header()
        {
            var orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesHG19WithDecoy);
            var chrCompare = new ChrCompare(orderedList);
            bool isHGsort = true;
            bool isGRCHsort = false;
            TestExpectations(isHGsort, isGRCHsort, chrCompare);
        }

        [Fact]
        public void OrderChrs_PathologicalContigList()
        {
            var orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesPathological);
            var chrCompare = new ChrCompare(orderedList);

            //by default, it falls back to string sort.
            bool isHGsort = false;
            bool isGRCHsort = false;
            TestExpectations(isHGsort, isGRCHsort, chrCompare);

            //plus test how it handles these extras:
            //we said "frog" had to come befor "9" , "9" before "7", and "2" before "-2"
            Assert.Equal(-1, chrCompare.Compare("frog", "9"));
            Assert.Equal(-1, chrCompare.Compare("9", "7"));
            Assert.Equal(-1, chrCompare.Compare("2", "-2"));
            Assert.Equal(1, chrCompare.Compare("9", "frog"));
            Assert.Equal(1, chrCompare.Compare("7", "9"));
            Assert.Equal(1, chrCompare.Compare("-2", "2"));

            //how does it handle stuff it never heard of before? fall back to string compare
            Assert.Equal(-1, chrCompare.Compare("3", "4"));
            Assert.Equal(1, chrCompare.Compare("frog", "app.le"));
        }


        private static void TestExpectations(bool isHGsort, bool isGRCHsort, ChrCompare chrCompare)
        {
            
            // These should work with any std sort order
            TestWithSameChrExpectations(chrCompare);
            TestWithHG19Expectations(chrCompare);
            TestWithGRCH37Expectations(chrCompare);

            //depends on where chrM is expected
            TestWithChrMExpectations(chrCompare, isHGsort);
            TestWithMExpectations(chrCompare, isGRCHsort);

            //These only work if a "chr[.]" HG19 type aware sort order is applied. 
            OnlyPassWithHG19Ordering(chrCompare, isHGsort);
            OnlyPassWithGRCHOrdering(chrCompare, isGRCHsort);


            // decoy chrs
            Assert.Equal(-1, chrCompare.Compare("cat", "dog"));
            Assert.Equal(-1, chrCompare.Compare("GL000194.1", "KI270755.1"));
            Assert.Equal(1, chrCompare.Compare("KI270755.1", "GL000194.1"));

            // One numeric, one chr[x]
            Assert.Equal(-1, chrCompare.Compare("8", "chr9"));
            Assert.Equal(-1, chrCompare.Compare("chr8", "M"));
            Assert.Equal(-1, chrCompare.Compare("2", "chrY"));
            Assert.Equal(-1, chrCompare.Compare("chrM", "X"));
            Assert.Equal(-1, chrCompare.Compare("chrX", "Y"));
            Assert.Equal(1, chrCompare.Compare("X", "chrY"));
        }

        private static void OnlyPassWithGRCHOrdering(ChrCompare chrCompare, bool isGRCHsort)
        {
            var expectation = (isGRCHsort) ? -1 : 1;

            //note these will be counter-intuitive UNLESS an order is supplied
            Assert.Equal(-1* expectation, chrCompare.Compare("12", "2"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("22", "4"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("10", "2"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("19", "2"));

            //note these will be counter-intuitive UNLESS an order is supplied
            Assert.Equal(expectation, chrCompare.Compare("2", "12"));
            Assert.Equal(expectation, chrCompare.Compare("4", "22"));
            Assert.Equal(expectation, chrCompare.Compare("2", "10"));
            Assert.Equal(expectation, chrCompare.Compare("2", "19"));
        }

        private static void OnlyPassWithHG19Ordering(ChrCompare chrCompare, bool isHG19Sort)
        {
            var expectation = (isHG19Sort) ? -1 : 1;

            Assert.Equal(expectation, chrCompare.Compare("chr2", "chr12"));
            Assert.Equal(expectation, chrCompare.Compare("chr4", "chr22"));
            Assert.Equal(expectation, chrCompare.Compare("chr2", "chr10"));
            Assert.Equal(expectation, chrCompare.Compare("chr2", "chr19"));

            Assert.Equal(-1 * expectation, chrCompare.Compare("chr12", "chr2"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("chr22", "chr4"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("chr10", "chr2"));
            Assert.Equal(-1 * expectation, chrCompare.Compare("chr19", "chr2"));

        }

        private static void TestWithGRCH37Expectations(ChrCompare chrCompare)
        {
            // diff chr, std [i] format
            Assert.Equal(-1, chrCompare.Compare("8", "9"));
            Assert.Equal(-1, chrCompare.Compare("2", "Y"));
            Assert.Equal(-1, chrCompare.Compare("X", "Y"));
            Assert.Equal(-1, chrCompare.Compare("20", "22"));
            Assert.Equal(-1, chrCompare.Compare("2", "22"));

            // diff chr, std [i] format
            Assert.Equal(1, chrCompare.Compare("9", "8"));
            Assert.Equal(1, chrCompare.Compare("Y", "2"));
            Assert.Equal(1, chrCompare.Compare("Y", "X"));
            Assert.Equal(1, chrCompare.Compare("22", "20"));
            Assert.Equal(1, chrCompare.Compare("22", "2"));
        }

        private static void TestWithMExpectations(ChrCompare chrCompare, bool mAfterXY)
        {
            var expectChrMAfterChrXY = (mAfterXY) ? 1 : -1;

            //M should always be after the numbered chrs
            Assert.Equal(-1, chrCompare.Compare("8", "M"));
            Assert.Equal(-1, chrCompare.Compare("20", "M"));
            Assert.Equal(-1, chrCompare.Compare("2", "M"));
            Assert.Equal(1, chrCompare.Compare("M", "8"));

            //where is M w.r.t X and Y ?

            Assert.Equal(expectChrMAfterChrXY, chrCompare.Compare("M", "X"));
            Assert.Equal(expectChrMAfterChrXY, chrCompare.Compare("M", "Y"));
            Assert.Equal(-1 * expectChrMAfterChrXY, chrCompare.Compare("X", "M"));
            Assert.Equal(-1 * expectChrMAfterChrXY, chrCompare.Compare("Y", "M"));
        }


        private static void TestWithChrMExpectations(ChrCompare chrCompare, bool chrMAfterChrXY)
        {
            var expectChrMAfterChrXY = (chrMAfterChrXY) ? 1 : -1;

            //M should always be after the numbered chrs
            Assert.Equal(-1, chrCompare.Compare("chr8", "chrM"));
            Assert.Equal(-1, chrCompare.Compare("chr20", "chrM"));
            Assert.Equal(-1, chrCompare.Compare("chr2", "chrM"));
            Assert.Equal(1, chrCompare.Compare("chrM", "chr8"));

            //where is M w.r.t X and Y ?

            Assert.Equal(expectChrMAfterChrXY, chrCompare.Compare("chrM", "chrX"));
            Assert.Equal(expectChrMAfterChrXY, chrCompare.Compare("chrM", "chrY"));
            Assert.Equal(-1 * expectChrMAfterChrXY, chrCompare.Compare("chrX", "chrM"));
            Assert.Equal(-1 * expectChrMAfterChrXY, chrCompare.Compare("chrY", "chrM"));
        }

            private static void TestWithHG19Expectations(ChrCompare chrCompare)
        {
            Assert.Equal(-1, chrCompare.Compare("chr8", "chr9"));
            Assert.Equal(-1, chrCompare.Compare("chr2", "chrY"));
            Assert.Equal(-1, chrCompare.Compare("chrX", "chrY"));
            Assert.Equal(-1, chrCompare.Compare("chr20", "chr22"));
            Assert.Equal(-1, chrCompare.Compare("chr2", "chr22"));


            // diff chr, std chr[i] format
            Assert.Equal(1, chrCompare.Compare("chr9", "chr8"));
            Assert.Equal(1, chrCompare.Compare("chrY", "chr2"));
            Assert.Equal(1, chrCompare.Compare("chrY", "chrX"));
            Assert.Equal(1, chrCompare.Compare("chr22", "chr20"));
            Assert.Equal(1, chrCompare.Compare("chr22", "chr2"));
        }

        private static void TestWithSameChrExpectations(ChrCompare chrCompare)
        {
            Assert.Equal(0, chrCompare.Compare("chr9", "chr9"));
            Assert.Equal(0, chrCompare.Compare("chrM", "chrM"));
            Assert.Equal(0, chrCompare.Compare("chrX", "chrX"));
            Assert.Equal(0, chrCompare.Compare("foo", "foo"));
            Assert.Equal(0, chrCompare.Compare("1", "1"));
            Assert.Equal(0, chrCompare.Compare("-1", "-1"));
            Assert.Equal(0, chrCompare.Compare("KQ031386.1", "KQ031386.1"));
        }

        [Fact]
        public void ParseContigs()
        {
           
            var orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesHG19WithDecoy);

            Assert.Equal(18, orderedList.Count);

            Assert.Equal("chr1", orderedList[0]);
            Assert.Equal("chr2", orderedList[1]);
            Assert.Equal("chr3", orderedList[2]);

            Assert.Equal("chrX", orderedList[9]);
            Assert.Equal("chrY", orderedList[10]);
            Assert.Equal("chrM", orderedList[11]);

            Assert.Equal("KI270756.1", orderedList[16]);
            Assert.Equal("KI270757.1", orderedList[17]);

            orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesWithGRCh37);

            Assert.Equal(18, orderedList.Count);

            Assert.Equal("1", orderedList[0]);
            Assert.Equal("2", orderedList[1]);
            Assert.Equal("4", orderedList[2]);

            Assert.Equal("20", orderedList[12]);
            Assert.Equal("21", orderedList[13]);
            Assert.Equal("X", orderedList[15]);
            Assert.Equal("M", orderedList[17]);

            orderedList = ChrCompare.GetChrListFromVcfHeader(_exampleHeaderLinesPathological);

            Assert.Equal(5, orderedList.Count);

            Assert.Equal("frog", orderedList[0]);
            Assert.Equal("9", orderedList[1]);
            Assert.Equal("7", orderedList[2]);
            Assert.Equal("2", orderedList[3]);
            Assert.Equal("-2", orderedList[4]);
        }
    }
}
