using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class VcfReaderTests
    {
        public string VcfTestFile_1 = Path.Combine("TestData", "VcfReaderTests_Test1.vcf");

        [Fact]
        public void GetNextVariantTests()
        {
            var resultVariant = new VcfVariant();
            string resultString = string.Empty;
            var vr = new VcfReader(VcfTestFile_1);
            vr.GetNextVariant(resultVariant, out resultString);
            Assert.Equal( resultString.TrimEnd('\r'), @"chr1	10	.	A	.	25	PASS	DP=500	GT:GQ:AD:VF:NL:SB:NC	1/1:25:0,0:0.0000:23:0.0000:0.0010");
            Assert.Equal(resultVariant.ReferenceName, "chr1");
            Assert.Equal(resultVariant.ReferenceAllele, "A");
            Assert.Equal(resultVariant.VariantAlleles.First(), ".");

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

            Assert.Equal(vr.Position(), 1452);

            var resultStringArray = new string[] {};
            resultVariant = new VcfVariant();

            vr.GetNextVariant(resultVariant, out resultString, out resultStringArray);
            Assert.Equal(resultString.TrimEnd('\r'), @"chr1	20	.	A	T	25	PASS	DP=500	GT:GQ:AD:VF:NL:SB:NC	1/1:25:0,0:0.0000:23:0.0000:0.0010");
            for (var i = 0; i < resultStringArray.Length; i++)
                resultStringArray[i] = resultStringArray[i].TrimEnd('\r');
            Assert.Equal(resultStringArray, @"chr1	20	.	A	T	25	PASS	DP=500	GT:GQ:AD:VF:NL:SB:NC	1/1:25:0,0:0.0000:23:0.0000:0.0010".Split('\t'));
            Assert.Equal(resultVariant.ReferenceName, "chr1");

            resultVariant = new VcfVariant();

            vr.GetNextVariant(resultVariant);
            Assert.Equal(resultVariant.ReferenceName, "chr1");
            Assert.Equal(resultVariant.ReferenceAllele, "A");
            Assert.Equal(resultVariant.VariantAlleles.First(), "AT");
        }

        [Fact]
        public void OpenException()
        {
            Assert.Throws<FileNotFoundException>(() => new VcfReader("NOT_A_PATH"));
        }

        [Fact]
        public void OpenSkipHeader()
        {
            var vr = new VcfReader(VcfTestFile_1, skipHeader:true);
            Assert.Empty(vr.HeaderLines);
        }
        
        [Fact]
        public void AssignVariantTypeTests()
        {
            var vr = new VcfReader(VcfTestFile_1);
            // Testing 1/1
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.SNV, VariantType.SNV));
            Assert.True(TestVariant(vr, VariantType.Insertion, VariantType.Insertion));
            Assert.True(TestVariant(vr, VariantType.Deletion, VariantType.Deletion));

            // Testing 1/0
            Assert.True(TestVariant(vr, VariantType.SNV, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Insertion, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Deletion, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.SNV, VariantType.Reference));

            // Testing 0/0
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));

            // Testing 0/1
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Insertion));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Deletion));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.SNV));

            // Testing MNV
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.Reference, VariantType.MNP));
            Assert.True(TestVariant(vr, VariantType.MNP, VariantType.Reference));
            Assert.True(TestVariant(vr, VariantType.MNP, VariantType.MNP));

            // Testing ./. . ./1 1/.
            Assert.True(TestVariant(vr, VariantType.Missing, VariantType.Missing));
            Assert.True(TestVariant(vr, VariantType.Missing, VariantType.Missing));
            Assert.True(TestVariant(vr, VariantType.SNV, VariantType.SNV));
            Assert.True(TestVariant(vr, VariantType.SNV, VariantType.SNV));

        }

        private bool TestVariant(VcfReader vr, VariantType type1, VariantType type2)
        {
            var testVar = new VcfVariant();
            vr.GetNextVariant(testVar);
            return (testVar.VarType1 == type1) && (testVar.VarType2 == type2);
        }
    }
}
