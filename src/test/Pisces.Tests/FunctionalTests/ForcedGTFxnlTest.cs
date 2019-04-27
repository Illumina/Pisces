using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Pisces.Tests.FunctionalTests
{
    public class ForcedGTFxnlTest
    {
        [Fact]
        public void RunForcedGT()
        {

            var phixBam = Path.Combine(TestPaths.SharedBamDirectory, "PhiX_S3.bam");
            var genomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "PhiX", "WholeGenomeFasta");
            var outputDirectory = Path.Combine(TestPaths.LocalScratchDirectory,"ForcedGT");
            var inputForcedGTVcf = Path.Combine(TestPaths.LocalTestDataDirectory, "PhiX_S3.forcedGTInput.vcf");
            var expectedVcfWithoutForcedGT = Path.Combine(TestPaths.LocalTestDataDirectory, "PhiX_S3.noisy.vcf");
            var expectedVcfWithForcedGT_test1 = Path.Combine(TestPaths.LocalTestDataDirectory, "PhiX_S3.Forced1.vcf");
            var expectedVcfWithForcedGT_test2 = Path.Combine(TestPaths.LocalTestDataDirectory, "PhiX_S3.Forced2.vcf");
            var outputVcf = Path.Combine(outputDirectory, "PhiX_S3.genome.vcf");

            if (File.Exists(outputVcf))
                File.Delete(outputVcf);

            //set to call very low frequency, to cause a lot of MNVs. Makes a good test.

            //should ouput a lot of noise, some of which will be poor quailty MNVS.
            Program.Main(new string[] { "-g", genomeDirectory, "-bam", phixBam, "-o",
                outputDirectory, "-c", "2" , "-minbq" , "10", "-minvq", "1" , "-minvf" , "0.00001" ,
                "-nl" , "40" ,
                "-callMNVs" , "TRUE" , "-maxmnvlength","10", "-maxgapbetweenmnv", "5", "-ncfilter", "1", "-abfilter", "0.01" } );


            //this is the MNV we are concerned about. 
            //phix    2.A   G   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:246,1:248:0.00403:40:-16.9682
            //phix    2.AGTTT   GGTTG   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:247,1:248:0.00403:40:-16.9682
            //phix    3.G.   100 PASS DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 /.:100:247:248:0.00403:40:-100.0000

            Assert.True(File.Exists(outputVcf));
            TestUtilities.TestHelper.CompareFiles(outputVcf, expectedVcfWithoutForcedGT);
            if (File.Exists(outputVcf))
                File.Delete(outputVcf);


            //now, rerun with forced GT and observe the expected output:
            Program.Main(new string[] { "-g", genomeDirectory, "-bam", phixBam, "-o",
                outputDirectory, "-c", "2" , "-minbq" , "10", "-minvq", "1" , "-minvf" , "0.00001" ,
                "-nl" , "40" ,
                "-callMNVs" , "TRUE" , "-maxmnvlength","10", "-maxgapbetweenmnv", "5" ,
                "-forcedalleles", inputForcedGTVcf , "-ncfilter", "1","-abfilter", "0.01" });
       



            //we should have injected 9 forcedReport varaints, as below
            //(1)phix    2.A   C   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:246,1:248:0.00403:40:-16.9682
            //(2)phix    2.AGTTT   GGTTG   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:247,1:248:0.00403:40:-16.9682
            //(3)phix    4.T   G   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:246,1:248:0.00403:40:-16.9682      
            //(4)phix    5.T   G   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:246,1:248:0.00403:40:-16.9682
            //(5)phix    7.T   C   100 PASS DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 0:0:248:248:0.00000:40:-100.0000
            //(6)phix    8.A   T   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:247,1:248:0.00403:40:-16.9682
            //(7)phix    10.CGCC    TTTT    100 PASS DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 0:0:248:248:0.00000:40:-100.0000
            //(8)phix    12.C   G   16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:246,1:248:0.00403:40:-16.9682
            //(9)phix    19.GACGCAG TACGCAT 16  q30 DP = 248  GT: GQ: AD: DP: VF: NL: SB    0 / 1:16:247,1:248:0.00403:40:-16.9682

            //this gives us the following expectations:
            //(1) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(2) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(3) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(4) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(5) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(6) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(7) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(8) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(9) will not be found as a real varaint, so should be filtered as "ForcedReport"

            Assert.True(File.Exists(outputVcf));
            TestUtilities.TestHelper.CompareFiles(outputVcf, expectedVcfWithForcedGT_test1);
            if (File.Exists(outputVcf))
                File.Delete(outputVcf);

            //now we are going to raise the variant Qscore filter from 1 to its normal value of 20, 
            //so those noise-level MNVs that we were looking for because of the forcedGT iput vcf
            //count as "failed" MNVs (this is what used to cause PICS-854)

            //this gives us the following expectations:
            //(1) will not be found as a real varaint, so should be filtered as "ForcedReport"
            //(2) found, no change to vcf line
            //(3) found, no change to vcf line  
            //(4) found, no change to vcf line
            //(5) this was origianlly a reference call. Now we will add it as a "ForcedReport"
            //(6) found, no change to vcf line
            //(7) will not be found as a real variant, so should be filtered as "ForcedReport"
            //(8) found, no change to vcf line
            //(9) found, no change to vcf line



            //Rerun with forced GT and observe the expected output:
            Program.Main(new string[] { "-g", genomeDirectory, "-bam", phixBam, "-o",
                outputDirectory, "-c", "2" , "-minbq" , "10", "-minvq", "20" , "-minvf" , "0.00001" ,
                "-nl" , "40" ,
                "-callMNVs" , "TRUE" , "-maxmnvlength","10", "-maxgapbetweenmnv", "5" ,
                "-forcedalleles", inputForcedGTVcf, "-ncfilter", "1" ,"-abfilter", "0.01" });

            Assert.True(File.Exists(outputVcf));
            TestUtilities.TestHelper.CompareFiles(outputVcf, expectedVcfWithForcedGT_test2);
            if (File.Exists(outputVcf))
                File.Delete(outputVcf);
        }
    }
}
