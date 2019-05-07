using Pisces.IO;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class VcfUpdaterTests
    {


        [Fact]
        public void CanSkipVcfLineTests()
        {
            //lines we can skip

            string skip1 = "chr4\t169663557\t.\tT\t.\t100\tPASS\tDP=23\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t0/0:34:22:23:0.043:20:-100.0000:0.0000:0,0,0,0,0,0,0,1,0,0,0,2";
            string skip2 = "chr4\t169663558\t.\tT\tTGGTGAGTCGTCGGCAGCGTCAGATGTGTATAAGAGACAG\t100\tPASS\tDP=71\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t0/1:32:25,46:71:0.648:20:-10.1946:0.0000:0,0,0,0,0,0,0,1,0,0,0,2";
            string skip3 = "chr15\t91310151\t.\tATATCTGA\tATTAGATTC,<M>\t0\tq20;SB;LowVariantFreq;ForcedReport\tDP=34\tGT:GQ:AD:DP:VF\t2/2:10:34,0,0:34:0.000";
            string skip4 = "chr4\t169663673\t.\tAT\tTG\t100\tPASS\tDP=56\tGT:GQ:AD:DP:VF:NL:SB\t1/1:100:6,50:56:0.893:20:-15.2723";

            Assert.Equal(TypeOfUpdateNeeded.NoChangeNeeded, QualityRecalibration.CanSkipVcfLine(skip1));
            Assert.Equal(TypeOfUpdateNeeded.NoChangeNeeded, QualityRecalibration.CanSkipVcfLine(skip2));
            Assert.Equal(TypeOfUpdateNeeded.NoChangeNeeded, QualityRecalibration.CanSkipVcfLine(skip3));
            Assert.Equal(TypeOfUpdateNeeded.NoChangeNeeded, QualityRecalibration.CanSkipVcfLine(skip4));

            //lines we must process

            string do1 = "chr4\t169663557\t.\tT\tG\t100\tPASS\tDP=23\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t0/0:34:22:23:0.043:20:-100.0000:0.0000:0,0,0,0,0,0,0,1,0,0,0,2";
            string do2 = "chr4\t169663558\t.\tT\tA\t100\tPASS\tDP=71\tGT:GQ:AD:DP:VF:NL:SB:NC:US\t0/1:32:25,46:71:0.648:20:-10.1946:0.0000:0,0,0,0,0,0,0,1,0,0,0,2";
            string do3 = "chr15\t91310151\t.\tA\tC\t0\tq20;SB;LowVariantFreq;ForcedReport\tDP=34\tGT:GQ:AD:DP:VF\t2/2:10:34,0,0:34:0.000";
            string do4 = "chr4\t169663673\t.\tA\tG\t100\tPASS\tDP=56\tGT:GQ:AD:DP:VF:NL:SB\t1/1:100:6,50:56:0.893:20:-15.2723";

           
            Assert.Equal(TypeOfUpdateNeeded.Modify,QualityRecalibration.CanSkipVcfLine(do1));
            Assert.Equal(TypeOfUpdateNeeded.Modify, QualityRecalibration.CanSkipVcfLine(do2));
            Assert.Equal(TypeOfUpdateNeeded.Modify, QualityRecalibration.CanSkipVcfLine(do3));
            Assert.Equal(TypeOfUpdateNeeded.Modify, QualityRecalibration.CanSkipVcfLine(do4));

        }
    }
}
