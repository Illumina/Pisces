using System.IO;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class AlleleReaderTests
    {
        public string VcfTestFile_1 = Path.Combine(TestPaths.LocalTestDataDirectory,"VcfReaderTests_Test1.vcf");
        public string ColocatedVcfTestFile = Path.Combine(TestPaths.LocalTestDataDirectory, "colocated.genome.vcf");

        [Fact]
        public void GetHeaderTests()
        {
            var header = AlleleReader.GetAllHeaderLines(VcfTestFile_1);
            string firstLine = "##fileformat=VCFv4.1";
            string lastLine = "#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO\tFORMAT\tmySample";
            Assert.Equal(23, header.Count);
            Assert.Equal(firstLine, header[0]);
            Assert.Equal(lastLine, header[22]);
        }

        [Fact]
        public void VcfLineToAllelesTests_SomaticForcedGTExample_PICS_1168()
        {
            //some example crummy input
            var inputLines = new List<string>() {
                "chr4\t56236582\t1ai\tA\tC\t.\t.\t.\t.\t.\r",
                "chr4\t56236583\t1aii\tA\tAA\t.\t.\t.\t.\t.",
                "chr18\t9888034\t6b\tA\t.\t.\t.\t.\t.\t.blah",
                "chr21\t46644966\t6b\tA\t.\t.\t.\t.\tboo\too",
                "chr21\t33694232\t6b\tA\t.\t.\t.\t.\t.\t.",
                "chr21\t33694239\t6c\tT\t<del>\t.\t.\t.\t.\t.",
                "chr8\t1817367\t6d\tC\tA\t.\t.\t.\t.\t.",
                "chr1\t109465143\tPICS827\tCTGCCATACAGCTTCAACAACAACTT\tATGCCATACAGCTTCAACAACAA\t.\t.\t.\t.\t.",
            };
            var outputAlleles = new List<CalledAllele>() { };

            foreach (var line in inputLines)
            {
                //make sure nothing throws
                var outputAllelesForLine = AlleleReader.VcfLineToAlleles(line, true);

                //make sure we only ever read 1 allele per line, since this is somatic input
                Assert.Equal(1, outputAllelesForLine.Count());

                outputAlleles.Add(outputAllelesForLine[0]);
            }

            //sanity check results
            var allele1 = outputAlleles[0];
            var allele2 = outputAlleles[7];

            Assert.Equal("chr4", allele1.Chromosome);
            Assert.Equal(56236582, allele1.ReferencePosition);
            Assert.Equal("A", allele1.ReferenceAllele);
            Assert.Equal("C", allele1.AlternateAllele);

            Assert.Equal("chr1", allele2.Chromosome);
            Assert.Equal(109465143, allele2.ReferencePosition);
            Assert.Equal("CTGCCATACAGCTTCAACAACAACTT", allele2.ReferenceAllele);
            Assert.Equal("ATGCCATACAGCTTCAACAACAA", allele2.AlternateAllele);
        }

        [Fact]
        public void VcfLineToAllelesTests_ThreeIndelExample()
        {

            //chr2    19946216.ATGTGTG ATG,ATGTG,A 0   PASS metal = platinum; cgi =.; bwa_freebayes = HD:0,LOOHD: 0; bwa_platypus =.; bwa_gatk3 = HD:2,LOOHD: 2; cortex =.; isaac2 = HD:1,LOOHD: 1; dist2closest = 192 GT  1 | 2

            string platypusLine = "chr2\t19946216\t.\tATGTGTG\tATG,ATGTG,A\t0\tPASS\tmetal=platinum;cgi=.;bwa_freebayes=HD:0,LOOHD: 0;bwa_platypus =.;bwa_gatk3=HD:2,LOOHD:2;cortex=.;isaac2=HD:1,LOOHD:1;dist2closest=192\tGT\t1|2";


            var varCalls = AlleleReader.VcfLineToAlleles(platypusLine, true);
            var testvar0 = varCalls[0];
            var testvar1 = varCalls[1];
            var testvar2 = varCalls[2];
            Assert.Equal(3, varCalls.Count);

            Assert.Equal("chr2", testvar0.Chromosome);
            Assert.Equal(19946216, testvar0.ReferencePosition);
            Assert.Equal("ATGTG", testvar0.ReferenceAllele);
            Assert.Equal("A", testvar0.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar0.Genotype);
            Assert.Equal(0, testvar0.GenotypeQscore);
            Assert.Equal(0, testvar0.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar0.Type);
            Assert.Equal(0, testvar0.Filters.Count);
            Assert.Equal(0, testvar0.TotalCoverage);
            Assert.Equal(0, testvar0.AlleleSupport);
            Assert.Equal(0, testvar0.ReferenceSupport);
            Assert.Equal(0, testvar0.Frequency, 4);
            Assert.Equal(0, testvar0.NoiseLevelApplied);
            Assert.Equal(-100, testvar0.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar0.FractionNoCalls);
            Assert.Equal(8, testvar0.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar0.ReadCollapsedCountTotal.Length);

            Assert.Equal("chr2", testvar1.Chromosome);
            Assert.Equal(19946216, testvar1.ReferencePosition);
            Assert.Equal("ATG", testvar1.ReferenceAllele);
            Assert.Equal("A", testvar1.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar1.Genotype);
            Assert.Equal(0, testvar1.GenotypeQscore);
            Assert.Equal(0, testvar1.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar1.Type);
            Assert.Equal(0, testvar1.Filters.Count);
            Assert.Equal(0, testvar1.TotalCoverage);
            Assert.Equal(0, testvar1.AlleleSupport);
            Assert.Equal(0, testvar1.ReferenceSupport);
            Assert.Equal(0, testvar1.Frequency, 4);
            Assert.Equal(0, testvar1.NoiseLevelApplied);
            Assert.Equal(-100, testvar1.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar1.FractionNoCalls);
            Assert.Equal(8, testvar1.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar1.ReadCollapsedCountTotal.Length);

            Assert.Equal("chr2", testvar2.Chromosome);
            Assert.Equal(19946216, testvar2.ReferencePosition);
            Assert.Equal("ATGTGTG", testvar2.ReferenceAllele);
            Assert.Equal("A", testvar2.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar1.Genotype);
            Assert.Equal(0, testvar2.GenotypeQscore);
            Assert.Equal(0, testvar2.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar2.Type);
            Assert.Equal(0, testvar2.Filters.Count);
            Assert.Equal(0, testvar2.TotalCoverage);
            Assert.Equal(0, testvar2.AlleleSupport);
            Assert.Equal(0, testvar2.ReferenceSupport);
            Assert.Equal(0, testvar2.Frequency, 4);
            Assert.Equal(0, testvar2.NoiseLevelApplied);
            Assert.Equal(-100, testvar2.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar2.FractionNoCalls);
            Assert.Equal(8, testvar2.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar2.ReadCollapsedCountTotal.Length);
        }
        [Fact]
        public void VcfLineToAllelesTests()
        {
            //Basic line parsing, starting with simple variants, and ending with more complex examples.
            //Make sure everythign is read back properly.

            string line1 = "chr4\t7\t.\tC\tA\t3\tPASS\tDP=5394\tGT:GQ:AD:DP:VF:NL:SB:NC\t1/1:10:7,2387:5394:0.4425:23:0.0000:0.0000";
         
            var varCalls = AlleleReader.VcfLineToAlleles(line1);
            var testvar = varCalls[0];
          
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr4", testvar.Chromosome);
            Assert.Equal(7, testvar.ReferencePosition);
            Assert.Equal("C", testvar.ReferenceAllele);
            Assert.Equal("A", testvar.AlternateAllele);
            Assert.Equal(Genotype.HomozygousAlt, testvar.Genotype);
            Assert.Equal(10, testvar.GenotypeQscore);
            Assert.Equal(3, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Snv, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(5394, testvar.TotalCoverage);
            Assert.Equal(2387, testvar.AlleleSupport);
            Assert.Equal(7, testvar.ReferenceSupport);
            Assert.Equal(0.4425, testvar.Frequency, 4);
            Assert.Equal(23, testvar.NoiseLevelApplied);
            Assert.Equal(0, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);


            string line2 = "chr4\t8\t.\tC\t.\t0\tLowDP\tDP=0\tGT:GQ:AD:DP:VF:NL:SB:NC\t./.:0:0:0:0.0000:23:0.0000:0.0000";
            varCalls = AlleleReader.VcfLineToAlleles(line2);
            testvar = varCalls[0];


            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr4", testvar.Chromosome);
            Assert.Equal(8, testvar.ReferencePosition);
            Assert.Equal("C", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.RefLikeNoCall, testvar.Genotype);
            Assert.Equal(0, testvar.GenotypeQscore);
            Assert.Equal(0, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(1, testvar.Filters.Count);
            Assert.Equal(FilterType.LowDepth, testvar.Filters[0]);
            Assert.Equal(0, testvar.TotalCoverage);
            Assert.Equal(0, testvar.AlleleSupport);
            Assert.Equal(0, testvar.ReferenceSupport);
            Assert.Equal(0.0, testvar.Frequency, 4);
            Assert.Equal(23, testvar.NoiseLevelApplied);
            Assert.Equal(0, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);


            string line3 = "chr4\t10\t.\tAA\tGA,G\t0\tPASS\tDP=5394\tGT:GQ:AD:DP:VF:NL:SB:NC\t1/2:0:2387,2000:5394:0.8133:23:0.0000:0.0000";

            varCalls = AlleleReader.VcfLineToAlleles(line3);
            var testvar0 = varCalls[0];
            var testvar1 = varCalls[1];
            Assert.Equal(2, varCalls.Count);

            Assert.Equal("chr4", testvar0.Chromosome);
            Assert.Equal(10, testvar0.ReferencePosition);
            Assert.Equal("AA", testvar0.ReferenceAllele);
            Assert.Equal("GA", testvar0.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar0.Genotype);
            Assert.Equal(0, testvar0.GenotypeQscore);
            Assert.Equal(0, testvar0.VariantQscore);
            Assert.Equal(AlleleCategory.Mnv, testvar0.Type);
            Assert.Equal(0, testvar0.Filters.Count);
            Assert.Equal(5394, testvar0.TotalCoverage);
            Assert.Equal(2387, testvar0.AlleleSupport);
            Assert.Equal(5394 - 2387 - 2000, testvar0.ReferenceSupport);
            Assert.Equal(2387.0 / 5394.0, testvar0.Frequency, 4);
            Assert.Equal(23, testvar0.NoiseLevelApplied);
            Assert.Equal(0, testvar0.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar0.FractionNoCalls);
            Assert.Equal(8, testvar0.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar0.ReadCollapsedCountTotal.Length);


            Assert.Equal("chr4", testvar1.Chromosome);
            Assert.Equal(10, testvar1.ReferencePosition);
            Assert.Equal("AA", testvar1.ReferenceAllele);
            Assert.Equal("G", testvar1.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar1.Genotype);
            Assert.Equal(0, testvar1.GenotypeQscore);
            Assert.Equal(0, testvar1.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar1.Type);
            Assert.Equal(0, testvar1.Filters.Count);
            Assert.Equal(5394, testvar1.TotalCoverage);
            Assert.Equal(2000, testvar1.AlleleSupport);
            Assert.Equal(5394 - 2387 - 2000, testvar1.ReferenceSupport);
            Assert.Equal(2000.0 / 5394.0, testvar1.Frequency, 4);
            Assert.Equal(23, testvar1.NoiseLevelApplied);
            Assert.Equal(0, testvar1.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar1.FractionNoCalls);
            Assert.Equal(8, testvar1.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar1.ReadCollapsedCountTotal.Length);

            string line4 = "chr2\t87003972\t.\tTTATCTC\tT\t100\tPASS\tDP=532\tGT:GQ:AD:DP:VF:NL:SB\t0/1:100:276,256:532:0.48:20:-100.0000";

            varCalls = AlleleReader.VcfLineToAlleles(line4);
            testvar = varCalls[0];

            
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr2", testvar.Chromosome);
            Assert.Equal(87003972, testvar.ReferencePosition);
            Assert.Equal("TTATCTC", testvar.ReferenceAllele);
            Assert.Equal("T", testvar.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAltRef, testvar.Genotype);
            Assert.Equal(100, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(532, testvar.TotalCoverage);
            Assert.Equal(256, testvar.AlleleSupport);
            Assert.Equal(276, testvar.ReferenceSupport);
            Assert.Equal(256.0/ 532.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-100, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);

            
            string line5 = "chr2\t87003973\t.\tT\t.\t100\tPASS\tDP=532\tGT:GQ:AD:DP:VF:NL:SB\t0/0:100:276:532:0.48:20:-100.0000";


            varCalls = AlleleReader.VcfLineToAlleles(line5);
            testvar = varCalls[0];

            
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr2", testvar.Chromosome);
            Assert.Equal(87003973, testvar.ReferencePosition);
            Assert.Equal("T", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.HomozygousRef, testvar.Genotype);
            Assert.Equal(100, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(532, testvar.TotalCoverage);
            Assert.Equal(276, testvar.AlleleSupport);
            Assert.Equal(276, testvar.ReferenceSupport);
            Assert.Equal(276.0 / 532.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-100, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);

            string line6 = "chr2\t87003974\t.\tATCTC\tA\t100\tPASS\tDP=532\tGT:GQ:AD:DP:VF:NL:SB\t0/1:100:276,256:532:0.48:20:-100.0000";


            varCalls = AlleleReader.VcfLineToAlleles(line6);
            testvar = varCalls[0];

        
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr2", testvar.Chromosome);
            Assert.Equal(87003974, testvar.ReferencePosition);
            Assert.Equal("ATCTC", testvar.ReferenceAllele);
            Assert.Equal("A", testvar.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAltRef, testvar.Genotype);
            Assert.Equal(100, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Deletion, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(532, testvar.TotalCoverage);
            Assert.Equal(256, testvar.AlleleSupport);
            Assert.Equal(276, testvar.ReferenceSupport);
            Assert.Equal(256.0 / 532.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-100, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0.0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);

            string line7 = "chrY\t2655675\t.\tG\t.\t100\tPASS\tDP=532\tGT:GQ:AD:DP:VF:NL:SB\t0:100:532:532:0.00:20:-4.0000";

            varCalls = AlleleReader.VcfLineToAlleles(line7);
            testvar = varCalls[0];

            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chrY", testvar.Chromosome);
            Assert.Equal(2655675, testvar.ReferencePosition);
            Assert.Equal("G", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.HemizygousRef, testvar.Genotype);
            Assert.Equal(100, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(532, testvar.TotalCoverage);
            Assert.Equal(532, testvar.AlleleSupport);
            Assert.Equal(532, testvar.ReferenceSupport);
            Assert.Equal(1.0, testvar.Frequency, 4);  //note the VF in the CLASS is allele freq, and in this case the allele is ref. BUT when we write to VF, its 1-Freq for ref-calls..
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-4.0, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);

            string line8 = "phix\t2\t.\tA\tC\t0\tq30;SB;LowVariantFreq;ForcedReport\tDP=248\tGT:GQ:AD:DP:VF:NL:SB\t0/1:0:246,0:248:0.00000:0:0.0000";

            varCalls = AlleleReader.VcfLineToAlleles(line8);
            testvar = varCalls[0];

           
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("phix", testvar.Chromosome);
            Assert.Equal(2, testvar.ReferencePosition);
            Assert.Equal("A", testvar.ReferenceAllele);
            Assert.Equal("C", testvar.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAltRef, testvar.Genotype);
            Assert.Equal(0, testvar.GenotypeQscore);
            Assert.Equal(0, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Snv, testvar.Type);
            Assert.Equal(4, testvar.Filters.Count);
            Assert.Equal(FilterType.LowVariantQscore, testvar.Filters[0]);
            Assert.Equal(FilterType.StrandBias, testvar.Filters[1]);
            Assert.Equal(FilterType.LowVariantFrequency, testvar.Filters[2]);
            Assert.Equal(FilterType.ForcedReport, testvar.Filters[3]);
            Assert.Equal(248, testvar.TotalCoverage);
            Assert.Equal(0, testvar.AlleleSupport);
            Assert.Equal(246, testvar.ReferenceSupport);
            Assert.Equal(0.0, testvar.Frequency, 4);
            Assert.Equal(0, testvar.NoiseLevelApplied);
            Assert.Equal(0, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);


            string line9 = "phix\t2\t.\tA\tG\t35\tPASS\tDP=248\tGT:GQ:AD:DP:VF:NL:SB\t0/1:35:246,2:248:0.00806:40:-23.1080";

            varCalls = AlleleReader.VcfLineToAlleles(line9);
            testvar = varCalls[0];

            
            Assert.Equal(1, varCalls.Count);
            Assert.Equal("phix", testvar.Chromosome);
            Assert.Equal(2, testvar.ReferencePosition);
            Assert.Equal("A", testvar.ReferenceAllele);
            Assert.Equal("G", testvar.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAltRef, testvar.Genotype);
            Assert.Equal(35, testvar.GenotypeQscore);
            Assert.Equal(35, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Snv, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(248, testvar.TotalCoverage);
            Assert.Equal(2, testvar.AlleleSupport);
            Assert.Equal(246, testvar.ReferenceSupport);
            Assert.Equal(2.0 / 248.0, testvar.Frequency, 4);
            Assert.Equal(40, testvar.NoiseLevelApplied);
            Assert.Equal(-23.1080, testvar.StrandBiasResults.GATKBiasScore,4);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);


            string line10 = "phix\t2\t.\tAGTTT\tGGTTG\t16\tq30;ForcedReport\tDP=248\tGT:GQ:AD:DP:VF:NL:SB\t0/1:0:247,1:248:0.00403:40:-16.9682";

            varCalls = AlleleReader.VcfLineToAlleles(line10);
            testvar = varCalls[0];
            

            Assert.Equal(1, varCalls.Count);
            Assert.Equal("phix", testvar.Chromosome);
            Assert.Equal(2, testvar.ReferencePosition);
            Assert.Equal("AGTTT", testvar.ReferenceAllele);
            Assert.Equal("GGTTG", testvar.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAltRef, testvar.Genotype);
            Assert.Equal(0, testvar.GenotypeQscore);
            Assert.Equal(16, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Mnv, testvar.Type);
            Assert.Equal(2, testvar.Filters.Count);
            Assert.Equal(FilterType.LowVariantQscore, testvar.Filters[0]);
            Assert.Equal(FilterType.ForcedReport, testvar.Filters[1]);
            Assert.Equal(248, testvar.TotalCoverage);
            Assert.Equal(1, testvar.AlleleSupport);
            Assert.Equal(247, testvar.ReferenceSupport);
            Assert.Equal(1.0 / 248.0, testvar.Frequency, 4);
            Assert.Equal(40, testvar.NoiseLevelApplied);
            Assert.Equal(-16.9682, testvar.StrandBiasResults.GATKBiasScore, 4);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);

            string line11 = "chr1\t9770596\t.\tC\tA\t63\tPASS\tDP=6\tGT:GQ:AD:DP:VF:NL:SB:US\t./.:0:2,4:6:0.667:20:-33.5565:1,0,2,0,1,0,1,0,3,0,2,0";


            varCalls = AlleleReader.VcfLineToAlleles(line11);
            testvar = varCalls[0];


            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr1", testvar.Chromosome);
            Assert.Equal(9770596, testvar.ReferencePosition);
            Assert.Equal("C", testvar.ReferenceAllele);
            Assert.Equal("A", testvar.AlternateAllele);
            Assert.Equal(Genotype.AltLikeNoCall, testvar.Genotype);
            Assert.Equal(0, testvar.GenotypeQscore);
            Assert.Equal(63, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Snv, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(6, testvar.TotalCoverage);
            Assert.Equal(4, testvar.AlleleSupport);
            Assert.Equal(2, testvar.ReferenceSupport);
            Assert.Equal(4.0 / 6.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-33.5565, testvar.StrandBiasResults.GATKBiasScore, 4);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(1, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(2, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(1, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(3, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);

            string line12 = "chr1\t9770597\t.\tA\t.\t100\tPASS\tDP=6\tGT:GQ:AD:DP:VF:NL:SB:US\t./.:0:6:6:0.000:20:-53.5655:0,0,0,0,0,0,1,0,3,0,2,0";


            varCalls = AlleleReader.VcfLineToAlleles(line12);
            testvar = varCalls[0];


            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr1", testvar.Chromosome);
            Assert.Equal(9770597, testvar.ReferencePosition);
            Assert.Equal("A", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.RefLikeNoCall, testvar.Genotype);
            Assert.Equal(0, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(6, testvar.TotalCoverage);
            Assert.Equal(6, testvar.AlleleSupport);
            Assert.Equal(6, testvar.ReferenceSupport);
            Assert.Equal(1.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-53.5655, testvar.StrandBiasResults.GATKBiasScore,4);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(1, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(3, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);

            string line13 = "chr4\t169663548\t.\tT\tG,TCGGCAGCGTCAGATGTGTATAAGAGACAG\t66\tPASS\tDP=13\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t1/2:6:5,7:13:0.923:20:-100.0000:0.0000:0,0,0,0,0,0,0,1,0,0,0,2";


            varCalls = AlleleReader.VcfLineToAlleles(line13);
            Assert.Equal(2, varCalls.Count);
            
            testvar0 = varCalls[0];
            testvar1 = varCalls[1];


            Assert.Equal("chr4", testvar0.Chromosome);
            Assert.Equal(169663548, testvar0.ReferencePosition);
            Assert.Equal("T", testvar0.ReferenceAllele);
            Assert.Equal("G", testvar0.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar0.Genotype);
            Assert.Equal(6, testvar0.GenotypeQscore);
            Assert.Equal(66, testvar0.VariantQscore);
            Assert.Equal(AlleleCategory.Snv, testvar0.Type);
            Assert.Equal(0, testvar0.Filters.Count);
            Assert.Equal(13, testvar0.TotalCoverage);
            Assert.Equal(5, testvar0.AlleleSupport);
            Assert.Equal(1, testvar0.ReferenceSupport);
            Assert.Equal(5.0/13.0, testvar0.Frequency, 4);
            Assert.Equal(20, testvar0.NoiseLevelApplied);
            Assert.Equal(-100, testvar0.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar0.FractionNoCalls);
            Assert.Equal(8, testvar0.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar0.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(1, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(3, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);

            Assert.Equal("chr4", testvar1.Chromosome);
            Assert.Equal(169663548, testvar1.ReferencePosition);
            Assert.Equal("T", testvar1.ReferenceAllele);
            Assert.Equal("TCGGCAGCGTCAGATGTGTATAAGAGACAG", testvar1.AlternateAllele);
            Assert.Equal(Genotype.HeterozygousAlt1Alt2, testvar1.Genotype);
            Assert.Equal(6, testvar1.GenotypeQscore);
            Assert.Equal(66, testvar1.VariantQscore);
            Assert.Equal(AlleleCategory.Insertion, testvar1.Type);
            Assert.Equal(0, testvar1.Filters.Count);
            Assert.Equal(13, testvar1.TotalCoverage);
            Assert.Equal(7, testvar1.AlleleSupport);
            Assert.Equal(1, testvar1.ReferenceSupport);
            Assert.Equal(7.0 / 13.0, testvar1.Frequency, 4);
            Assert.Equal(20, testvar1.NoiseLevelApplied);
            Assert.Equal(-100, testvar1.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar1.FractionNoCalls);
            Assert.Equal(8, testvar1.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar1.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(1, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(3, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);
         
            string line14 = "chr4\t169663549\t.\tA\t.\t100\tPASS\tDP=21\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t0/0:42:21:21:0.000:20:-100.0000:0.0100:0,0,0,0,0,0,0,1,0,0,0,2";


            varCalls = AlleleReader.VcfLineToAlleles(line14);
            testvar = varCalls[0];


            Assert.Equal(1, varCalls.Count);
            Assert.Equal("chr4", testvar.Chromosome);
            Assert.Equal(169663549, testvar.ReferencePosition);
            Assert.Equal("A", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.HomozygousRef, testvar.Genotype);
            Assert.Equal(42, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(21, testvar.TotalCoverage);
            Assert.Equal(21, testvar.AlleleSupport);
            Assert.Equal(21, testvar.ReferenceSupport);
            Assert.Equal(1.0, testvar.Frequency, 4);
            Assert.Equal(20, testvar.NoiseLevelApplied);
            Assert.Equal(-100.0000, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0.0100, testvar.FractionNoCalls,4);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(2, testvar.ReadCollapsedCountTotal[7]);

            string line15 = "phix\t3\t.\tG\t.\t100\tPASS\tDP=248\tGT:GQ:AD:DP:VF:NL:SB\t0/.:100:247:248:0.00403:40:-100.0000";


            varCalls = AlleleReader.VcfLineToAlleles(line15);
            testvar = varCalls[0];


            Assert.Equal(1, varCalls.Count);
            Assert.Equal("phix", testvar.Chromosome);
            Assert.Equal(3, testvar.ReferencePosition);
            Assert.Equal("G", testvar.ReferenceAllele);
            Assert.Equal(".", testvar.AlternateAllele);
            Assert.Equal(Genotype.RefAndNoCall, testvar.Genotype);
            Assert.Equal(100, testvar.GenotypeQscore);
            Assert.Equal(100, testvar.VariantQscore);
            Assert.Equal(AlleleCategory.Reference, testvar.Type);
            Assert.Equal(0, testvar.Filters.Count);
            Assert.Equal(248, testvar.TotalCoverage);
            Assert.Equal(247, testvar.AlleleSupport);
            Assert.Equal(247, testvar.ReferenceSupport);
            Assert.Equal(247.0 / 248.0, testvar.Frequency, 4);
            Assert.Equal(40, testvar.NoiseLevelApplied);
            Assert.Equal(-100.0000, testvar.StrandBiasResults.GATKBiasScore);
            Assert.Equal(0, testvar.FractionNoCalls);
            Assert.Equal(8, testvar.ReadCollapsedCountsMut.Length);
            Assert.Equal(8, testvar.ReadCollapsedCountTotal.Length);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountsMut[7]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[0]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[4]);
            Assert.Equal(0, testvar.ReadCollapsedCountTotal[7]);

        }


        [Fact]
        public void GetVariantsTests()
        {
            var vr = new AlleleReader(VcfTestFile_1);
            var allVar = vr.GetVariants().ToList();
            Assert.Equal(24, allVar.Count);
            Assert.Equal(10, allVar.First().ReferencePosition);
            Assert.Equal(4000, allVar.Last().ReferencePosition);
        }




        [Fact]
        public void GetNextVariantTests()
        {
            var resultVariant = new CalledAllele();
            var resultVariants = new List<CalledAllele> { resultVariant };
            string resultString = string.Empty;
            var vr = new AlleleReader(VcfTestFile_1);
            vr.GetNextVariants(out resultVariants, out resultString);
            Assert.Equal(resultString.TrimEnd('\r'), @"chr1	10	.	A	.	25	PASS	DP=500	GT:GQ:AD:VF:NL:SB:NC	1/1:25:0,0:0.0000:23:0.0000:0.0010");
            Assert.Equal(resultVariants[0].Chromosome, "chr1");
            Assert.Equal(resultVariants[0].ReferenceAllele, "A");
            Assert.Equal(resultVariants[0].AlternateAllele, ".");

            //Note, we have seen this assert below fail for specific user configurations
            //When it fails the error mesg is as below:
            //Assert.Equal() Failure
            //Expected: 1428
            //Actual: 1452
            //If this happens to you, check your git attributes config file.
            //You might be handling vcf text file line endings differently so the white space counts differently in this test. 
            // In that case, the fail is purely cosmetic.
            //
            //try: Auto detect text files and perform LF normalization
            //# http://davidlaing.com/2012/09/19/customise-your-gitattributes-to-become-a-git-ninja/
            //*text = auto
            //*.cs     diff = csharp
            //*.bam binary
            //*.vcf text
            //.fa text eol = crlf

            if (vr.Position() == 1428)
            {
                Console.WriteLine("This isn't critical, but you might want to change your line endings convention. ");
                Console.WriteLine("This project was developed with \\CR\\LF , not \\LF convention.");
            }
            else
                Assert.Equal(1452, vr.Position());

            var resultStringArray = new string[] { };
            resultVariant = new CalledAllele();
            resultVariants = new List<CalledAllele> { resultVariant };

            vr.GetNextVariants(out resultVariants, out resultString);
            Assert.Equal(resultString.TrimEnd('\r'), @"chr1	20	.	A	T	25	PASS	DP=500	GT:GQ:AD:VF:NL:SB:NC	1/1:25:0,0:0.0000:23:0.0000:0.0010");
            for (var i = 0; i < resultStringArray.Length; i++)
                resultStringArray[i] = resultStringArray[i].TrimEnd('\r');
            Assert.Equal(resultVariants[0].Chromosome, "chr1");

            resultVariant = new CalledAllele();
            resultVariants = new List<CalledAllele> { resultVariant };

            vr.GetNextVariants(out resultVariants);
            Assert.Equal(resultVariants[0].Chromosome, "chr1");
            Assert.Equal(resultVariants[0].ReferenceAllele, "A");
            Assert.Equal(resultVariants[0].AlternateAllele, "AT");
        }



        [Fact]
        public void CloseColocatedGroupVariantTests()
        {
            List<string> resultStrings = new List<string>();
            string incomingHangingVariantLine = null;
            string outgoingHangingVariantLine = null;
            Dictionary<string, List<CalledAllele>> ColocatedAlleles = new Dictionary<string, List<CalledAllele>>();

            var vr = new AlleleReader(ColocatedVcfTestFile);
            var nextClosedLines = vr.CloseColocatedLines(incomingHangingVariantLine, out outgoingHangingVariantLine);
            var nextClosedGroup = AlleleReader.VcfLinesToAlleles(nextClosedLines);
            var outgoingHangingVariants = AlleleReader.VcfLineToAlleles(outgoingHangingVariantLine);

            //the algorithm should have grouped the first two, and left the last one hanging.
            //chr1    223906728.G.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    223906728.G   A   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:276,256:532:0.48:20:-100.0000
            //chr1    223906729.G.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000

            Assert.Equal(2, nextClosedGroup.Count);
            Assert.Equal(1, outgoingHangingVariants.Count);

            Assert.Equal(nextClosedGroup[0].Chromosome, "chr1");
            Assert.Equal(nextClosedGroup[0].ReferenceAllele, "G");
            Assert.Equal(nextClosedGroup[0].AlternateAllele, ".");
            Assert.Equal(nextClosedGroup[0].ReferencePosition, 223906728);

            Assert.Equal(nextClosedGroup[1].Chromosome, "chr1");
            Assert.Equal(nextClosedGroup[1].ReferenceAllele, "G");
            Assert.Equal(nextClosedGroup[1].AlternateAllele, "A");
            Assert.Equal(nextClosedGroup[1].ReferencePosition, 223906728);

            Assert.Equal(outgoingHangingVariants[0].Chromosome, "chr1");
            Assert.Equal(outgoingHangingVariants[0].ReferenceAllele, "G");
            Assert.Equal(outgoingHangingVariants[0].AlternateAllele, ".");
            Assert.Equal(outgoingHangingVariants[0].ReferencePosition, 223906729);

            ColocatedAlleles.Add(nextClosedGroup[0].Chromosome + "_" + nextClosedGroup[0].ReferencePosition, nextClosedGroup);


            //now read the rest of the file

            while (true)
            {
                incomingHangingVariantLine = outgoingHangingVariantLine;

                if (incomingHangingVariantLine == null)
                    break;

                var nextGroupLines = vr.CloseColocatedLines(incomingHangingVariantLine, out outgoingHangingVariantLine);
                var nextGroup = AlleleReader.VcfLinesToAlleles(nextGroupLines);
                ColocatedAlleles.Add(nextGroup[0].Chromosome + "_" + nextGroup[0].ReferencePosition, nextGroup);
            }

            //check that everything loaded correctly
            Assert.Equal(28, ColocatedAlleles.Keys.Count);

            //example ref site with one allele
            //chr1	223906730	.	G	.	100	PASS	DP=532	GT:GQ:AD:DP:VF:NL:SB	0/0:100:532:532:0.00:20:-100.0000
            var ex1 = ColocatedAlleles["chr1_223906730"];
            Assert.Equal(ex1.Count, 1);
            Assert.Equal(ex1[0].Chromosome, "chr1");
            Assert.Equal(ex1[0].ReferenceAllele, "G");
            Assert.Equal(ex1[0].AlternateAllele, ".");
            Assert.Equal(ex1[0].ReferencePosition, 223906730);

            //example mulit allelic site as one vcf line
            //chr1	223906731	.	C	A,T	100	PASS	DP=532	GT:GQ:AD:DP:VF:NL:SB	1/2:100:254,254:532:0.95:20:-100.0000
            var ex2 = ColocatedAlleles["chr1_223906731"];
            Assert.Equal(ex2.Count, 2);
            Assert.Equal(ex2[0].Chromosome, "chr1");
            Assert.Equal(ex2[0].ReferenceAllele, "C");
            Assert.Equal(ex2[0].AlternateAllele, "A");
            Assert.Equal(ex2[0].ReferencePosition, 223906731);
            Assert.Equal(ex2[1].Chromosome, "chr1");
            Assert.Equal(ex2[1].ReferenceAllele, "C");
            Assert.Equal(ex2[1].AlternateAllele, "T");
            Assert.Equal(ex2[1].ReferencePosition, 223906731);


            //example multi allelic site as multiple vcf lines
            //chr1    223906746.G.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    223906746.G   A   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:276,256:532:0.48:20:-100.0000
            //chr1    223906746.G   AC  100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:276,256:532:0.48:20:-100.0000
            //chr1    223906746.GG  AT  100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:276,256:532:0.48:20:-100.0000

            var ex3 = ColocatedAlleles["chr1_223906746"];
            Assert.Equal(ex3.Count, 4);
            Assert.Equal(ex3[0].Chromosome, "chr1");
            Assert.Equal(ex3[0].ReferenceAllele, "G");
            Assert.Equal(ex3[0].AlternateAllele, ".");
            Assert.Equal(ex3[0].ReferencePosition, 223906746);
            Assert.Equal(ex3[1].Chromosome, "chr1");
            Assert.Equal(ex3[1].ReferenceAllele, "G");
            Assert.Equal(ex3[1].AlternateAllele, "A");
            Assert.Equal(ex3[1].ReferencePosition, 223906746);
            Assert.Equal(ex3[2].Chromosome, "chr1");
            Assert.Equal(ex3[2].ReferenceAllele, "G");
            Assert.Equal(ex3[2].AlternateAllele, "AC");
            Assert.Equal(ex3[2].ReferencePosition, 223906746);
            Assert.Equal(ex3[3].Chromosome, "chr1");
            Assert.Equal(ex3[3].ReferenceAllele, "GG");
            Assert.Equal(ex3[3].AlternateAllele, "AT");
            Assert.Equal(ex3[3].ReferencePosition, 223906746);

            //check the last vcf lines
            //chrY	87003973	.	T	.	100	PASS	DP=532	GT:GQ:AD:DP:VF:NL:SB	0/0:100:276:532:0.48:20:-100.0000
            //chrY    87003973.ATCTC   A   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:276,256:532:0.48:20:-100.0000
            var ex4 = ColocatedAlleles["chrY_87003973"];
            Assert.Equal(ex4.Count, 2);
            Assert.Equal(ex4[0].Chromosome, "chrY");
            Assert.Equal(ex4[0].ReferenceAllele, "T");
            Assert.Equal(ex4[0].AlternateAllele, ".");
            Assert.Equal(ex4[0].ReferencePosition, 87003973);
            Assert.Equal(ex4[1].Chromosome, "chrY");
            Assert.Equal(ex4[1].ReferenceAllele, "ATCTC");
            Assert.Equal(ex4[1].AlternateAllele, "A");
            Assert.Equal(ex4[1].ReferencePosition, 87003973);

        }

        [Fact]
        public void OpenException()
        {
            Assert.Throws<FileNotFoundException>(() => new AlleleReader("NOT_A_PATH"));
        }

        [Fact]
        public void OpenSkipHeader()
        {
            var vr = new AlleleReader(VcfTestFile_1, skipHeader: true);
            Assert.Empty(vr.HeaderLines);
        }

        [Fact]
        public void AssignVariantTypeTests()
        { 
            var vr = new AlleleReader(VcfTestFile_1);

            // Testing 1/1
            Assert.True(TestVariant(vr, AlleleCategory.Reference));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));
            Assert.True(TestVariant(vr, AlleleCategory.Insertion));
            Assert.True(TestVariant(vr, AlleleCategory.Deletion));

            // Testing 1/0
            Assert.True(TestVariant(vr, AlleleCategory.Snv));
            Assert.True(TestVariant(vr, AlleleCategory.Insertion));
            Assert.True(TestVariant(vr, AlleleCategory.Deletion));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));

            // Testing 0/0
            //chr1    90.A.   25 PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC 0 / 0:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    100.A AT    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC 0 / 0:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    110.AT A    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC 0 / 0:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    120.A T 25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC 0 / 0:25:0,0:0.0000:23:0.0000:0.0010
            Assert.True(TestVariant(vr, AlleleCategory.Reference));
            Assert.True(TestVariant(vr, AlleleCategory.Insertion));   
            Assert.True(TestVariant(vr, AlleleCategory.Deletion));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));

            // Testing 0/1
            //chr1    130.A.   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 1:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    140.A   AT  25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 1:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    150.AT  A   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 1:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    160.A   T   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 1:25:0,0:0.0000:23:0.0000:0.0010
            Assert.True(TestVariant(vr, AlleleCategory.Reference));
            Assert.True(TestVariant(vr, AlleleCategory.Insertion));
            Assert.True(TestVariant(vr, AlleleCategory.Deletion));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));

            // Testing MNV
            //chr1    600.ATCA    TCGC    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 0:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    700.ATCA    TCGC    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    0 / 1:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    800.ATCA    TCGC    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    1 / 0:25:0,0:0.0000:23:0.0000:0.0010
            // chr1    900.ATCA    TCGC    25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC   1 / 1:25:0,0:0.0000:23:0.0000:0.0010
            Assert.True(TestVariant(vr, AlleleCategory.Mnv));
            Assert.True(TestVariant(vr, AlleleCategory.Mnv));
            Assert.True(TestVariant(vr, AlleleCategory.Mnv));
            Assert.True(TestVariant(vr, AlleleCategory.Mnv));

            // Testing ./. . ./1 1/.
            //chr1    1000.A   T   25  PASS DP = 0    GT: GQ: AD: VF: NL: SB: NC./.:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    2000.A   T   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC.:25:0,0:0.0000:23:0.0000:0.0010
            //chr1    3000.A   T   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC./ 1:25:0,0:0.0000:23:0.0000:0.0010
            // chr1    4000.A   T   25  PASS DP = 500  GT: GQ: AD: VF: NL: SB: NC    1 /.:25:0,0:0.0000:23:0.0000:0.0010
            Assert.True(TestVariant(vr, AlleleCategory.Snv));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));
            Assert.True(TestVariant(vr, AlleleCategory.Snv));

        }

        private bool TestVariant(AlleleReader vr, AlleleCategory type)
        {
            var testVarList = new List<CalledAllele>() { new CalledAllele() };
            vr.GetNextVariants(out testVarList);
            return (testVarList[0].Type == type);
        }
    }
}
