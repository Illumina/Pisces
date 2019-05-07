using System.IO;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Options;
using Pisces.Domain.Models.Alleles;
using Pisces.IO.Sequencing;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class VcfUpdaterTests
    {
        public class SomeData
        {
            public string NewReferenceChr = "FrogChr";
        }

        [Fact]
        public void UpdateVcfTest_TestOnSingleAlleleAction()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "VcfUpdaterTestsOutDir");
            var inputDir = Path.Combine(TestPaths.LocalTestDataDirectory);
            var inputVcfFilePath = Path.Combine(inputDir, "crushed.genome.vcf");
            var outputFile1 = Path.Combine(outDir, "RewriteExample1.vcf");
            var outputFile2 = Path.Combine(outDir, "RewriteExample2.vcf");
            var outputFile3 = Path.Combine(outDir, "RewriteExample3.vcf");
            var outputFile4 = Path.Combine(outDir, "RewriteExample4.vcf");
            var outputFile5 = Path.Combine(outDir, "RewriteExample5.vcf");
            var outputFile6 = Path.Combine(outDir, "RewriteExample6.vcf");

            var expectedFile1 = Path.Combine(inputDir, "VcfReWriter_NoChangeToVariants.vcf");
            var expectedFile2 = Path.Combine(inputDir, "VcfReWriter_AllChangeToVariants.vcf");
            var expectedFile3 = Path.Combine(inputDir, "VcfReWriter_SomeChangeToVariants.vcf");
            var expectedFile4 = Path.Combine(inputDir, "VcfReWriter_RemoveAllVariants.vcf");
            var expectedFile5 = Path.Combine(inputDir, "VcfReWriter_RemoveSomeVariants.vcf");
            var expectedFile6 = Path.Combine(inputDir, "VcfReWriter_ComplexChangesVariants.vcf");

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            var myData = new SomeData();
            var options = new VcfConsumerAppOptions();
            options.VcfPath = inputVcfFilePath;
            options.VariantCallingParams.AmpliconBiasFilterThreshold = null;//turning this off because these tests predate the AB filter. This allows the pre-exisiting vcf headers to stay the same.

            //edit NO lines
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile1, options, true, myData, UpdateChrToFrog, CanAlwaysSkipVcfLine, GetVcfFileWriter);

            //edit ALL lines
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile2, options, true, myData, UpdateChrToFrog, CanNeverSkipVcfLine, GetVcfFileWriter);

            //do something silly to lines with a "C" allele
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile3, options, true, myData, UpdateChrToFrog, CanSometimesSkipVcfLine, GetVcfFileWriter);

            //remove all vcf entries
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile4, options, true, myData, UpdateChrToFrog, CanAlwaysDeleteVcfLine, GetVcfFileWriter);

            //remove all vcf entries with a "C" allele
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile5, options, true, myData, UpdateChrToFrog, CanSometimesDeleteVcfLine, GetVcfFileWriter);


            //Look at lines with a "C" allele.
            //If lines with a C allele (ref or alt) have T as an alt, make the chr = "MadeAChangeHERE".
            //If lines with a C allele (ref or alt) DO NOT have T as an alt, delete the line entirely.
            VcfUpdater<SomeData>.UpdateVcfAlleleByAllele(outputFile6, options, true, myData, UpdateChrToFrogOrDelete, CanSometimesSkipVcfLine, GetVcfFileWriter);

            //so, this one is left as is;
            //chr1    223906730.G.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000

            //this one, the C->A should get removed, and the C->T should have  chr = "MadeAChangeHERE".
            //chr1    223906731.C   A,T 100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    1 / 2:100:254,254:532:0.95:20:-100.0000

            // these are also all removed           
            //chr1    223906744.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    228526603.C.   100 PASS DP = 536  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:536:536:0.00:20:-100.0000
            //chr1    228526606.C.   100 PASS DP = 536  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:536:536:0.00:20:-100.0000
            //chr1    247812092.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    247812094.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    247812096.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    247812099.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr1    247812108.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000
            //chr2    55862775.C.   100 PASS DP = 532  GT: GQ: AD: DP: VF: NL: SB    0 / 0:100:532:532:0.00:20:-100.0000


            //check files
            TestUtilities.TestHelper.CompareFiles(outputFile1, expectedFile1);
            TestUtilities.TestHelper.CompareFiles(outputFile2, expectedFile2);
            TestUtilities.TestHelper.CompareFiles(outputFile3, expectedFile3);
            TestUtilities.TestHelper.CompareFiles(outputFile4, expectedFile4);
            TestUtilities.TestHelper.CompareFiles(outputFile5, expectedFile5);
            TestUtilities.TestHelper.CompareFiles(outputFile6, expectedFile6);

            //explicit checks for the complicated one, so users can see what we are looking for:

            var variantsTest6 = AlleleReader.GetAllVariantsInFile(outputFile6);
            var variantsInput = AlleleReader.GetAllVariantsInFile(inputVcfFilePath);

            Assert.Equal(91, variantsInput.Count());
            Assert.Equal(91 - 10, variantsTest6.Count()); //accounting for removed lines

            Assert.Equal(223906728, variantsInput[0].ReferencePosition);
            Assert.Equal("chr1", variantsInput[0].Chromosome);

            Assert.Equal(223906728, variantsTest6[0].ReferencePosition);
            Assert.Equal("chr1", variantsTest6[0].Chromosome);

            Assert.Equal(223906731, variantsInput[3].ReferencePosition);
            Assert.Equal("chr1", variantsInput[3].Chromosome);

            Assert.Equal(223906731, variantsTest6[3].ReferencePosition);
            Assert.Equal("FrogChr", variantsTest6[3].Chromosome);
        }


        [Fact]
        public void UpdateVcfTest_TestOnAllLociAlleleAction()
        {
            var outDir = Path.Combine(TestPaths.LocalScratchDirectory, "ModifyCoLocated");
            var inputDir = Path.Combine(TestPaths.LocalTestDataDirectory);
            var inputVcfFilePath = Path.Combine(inputDir, "colocated.genome.vcf");
            var outputFile1 = Path.Combine(outDir, "Rewrite_NoChangeToVariants.vcf");
            var outputFile2 = Path.Combine(outDir, "Rewrite_TagMultiAllelicSites.vcf");
            var outputFile3 = Path.Combine(outDir, "Rewrite_TagIndelSites.vcf");

            var expectedFile1 = Path.Combine(inputDir, "VcfReWriter_NoChangeToLoci.vcf");
            var expectedFile2 = Path.Combine(inputDir, "VcfReWriter_TagMultiAllelicSites.vcf");
            var expectedFile3 = Path.Combine(inputDir, "VcfReWriter_TagIndelSites.vcf");
  

            TestUtilities.TestHelper.RecreateDirectory(outDir);

            var myData = new SomeData();
            var options = new VcfConsumerAppOptions();
            options.VcfPath = inputVcfFilePath;
            options.VariantCallingParams.AmpliconBiasFilterThreshold = null;//turning this off because these tests predate the AB filter. This allows the pre-exisiting vcf headers to stay the same.

            //edit NO lines
            VcfUpdater<SomeData>.UpdateVcfLociByLoci(outputFile1, options, true, myData, VcfUpdater<SomeData>.NeverUpdateByLoci, CanAlwaysSkipVcfLine, GetVcfFileWriter);

            //TagMultiAllelicSites
            VcfUpdater<SomeData>.UpdateVcfLociByLoci(outputFile2, options, true, myData, TagMultiAllelicSites, CanNeverSkipVcfLine, GetVcfFileWriter);

            //TagIndelSites
            VcfUpdater<SomeData>.UpdateVcfLociByLoci(outputFile3, options, true, myData, TagIndelSites, CanNeverSkipVcfLine, GetVcfFileWriter);

            //check files
            TestUtilities.TestHelper.CompareFiles(outputFile1, expectedFile1);
            TestUtilities.TestHelper.CompareFiles(outputFile2, expectedFile2);
            TestUtilities.TestHelper.CompareFiles(outputFile3, expectedFile3);

        }





        public static VcfFileWriter GetVcfFileWriter(VcfConsumerAppOptions options, string outputFilePath)
        {
            var vcp = options.VariantCallingParams;
            var vwp = options.VcfWritingParams;
            var bfp = options.BamFilterParams;
            var vcfConfig = new VcfWriterConfig(vcp, vwp, bfp, null, false, false);
            var headerLines = AlleleReader.GetAllHeaderLines(options.VcfPath);

            var vqrCommandLineForVcfHeader = "##VQR_cmdline=" + options.QuotedCommandLineArgumentsString;
            return (new VcfFileWriter(outputFilePath, vcfConfig, new VcfWriterInputContext()));
        }


        private static TypeOfUpdateNeeded UpdateChrToFrog(VcfConsumerAppOptions appOptions, SomeData newData, CalledAllele inAllele, out List<CalledAllele> outAlleles)
        {
            inAllele.Chromosome = newData.NewReferenceChr;
            outAlleles = new List<CalledAllele> { inAllele };

            if (inAllele.AlternateAllele == "T")
            {
                inAllele.AlternateAllele = "MadeAChangeHERE";
            }
            return TypeOfUpdateNeeded.Modify;
        }


        private static TypeOfUpdateNeeded UpdateChrToFrogOrDelete(VcfConsumerAppOptions appOptions, SomeData newData, CalledAllele inAllele, out List<CalledAllele> outAlleles)
        {
            inAllele.Chromosome = newData.NewReferenceChr;
            outAlleles = new List<CalledAllele> { inAllele };

            if (inAllele.AlternateAllele == "T")
            {
                inAllele.AlternateAllele = "MadeAChangeHERE";

                return TypeOfUpdateNeeded.Modify;
            }
            return TypeOfUpdateNeeded.DeleteCompletely;
        }

        public static TypeOfUpdateNeeded CanAlwaysSkipVcfLine(List<string> originalVarString)
        {
            return TypeOfUpdateNeeded.NoChangeNeeded;
        }

        public static TypeOfUpdateNeeded CanAlwaysDeleteVcfLine(List<string> originalVarString)
        {
            return TypeOfUpdateNeeded.DeleteCompletely;
        }

        public static TypeOfUpdateNeeded CanNeverSkipVcfLine(List<string> originalVarString)
        {
            return TypeOfUpdateNeeded.Modify;
        }
        public static TypeOfUpdateNeeded CanSometimesSkipVcfLine(List<string> originalVarString)
        {
            foreach (var s in originalVarString)
            {
                if (s.Contains("\tC\t"))
                    return TypeOfUpdateNeeded.Modify;

            }
            return TypeOfUpdateNeeded.NoChangeNeeded;
        }

        public static TypeOfUpdateNeeded CanSometimesDeleteVcfLine(List<string> originalVarString)
        {
            foreach (var s in originalVarString)
            {
                if (s.Contains("\tC\t"))
                    if (s.Contains("\tC\t"))
                        return TypeOfUpdateNeeded.DeleteCompletely;
            }

            return TypeOfUpdateNeeded.NoChangeNeeded;
        }


        private static TypeOfUpdateNeeded TagMultiAllelicSites(VcfConsumerAppOptions appOptions, SomeData newData, List<CalledAllele> inAlleles, out List<CalledAllele> outAlleles)
        {
            bool giveTag = inAlleles.Count > 1;

            foreach (var allele in inAlleles)
            {
                if (giveTag)
                {
                    allele.Chromosome = "MultiAllelicSite";
                }
            }

            outAlleles = inAlleles;
            return TypeOfUpdateNeeded.Modify;
        }

        private static TypeOfUpdateNeeded TagIndelSites(VcfConsumerAppOptions appOptions, SomeData newData, List<CalledAllele> inAlleles, out List<CalledAllele> outAlleles)
        {
            bool giveTag = false;

            foreach (var allele in inAlleles)
            {
                if ((allele.Type == Domain.Types.AlleleCategory.Deletion)
                    || (allele.Type == Domain.Types.AlleleCategory.Insertion))
                {
                    giveTag = true;
                }
            }

            foreach (var allele in inAlleles)
            {
                if (giveTag)
                {
                    allele.Chromosome = "IndelSite";
                }
            }

            outAlleles = inAlleles;
            return TypeOfUpdateNeeded.Modify;
        }
    }
}

