using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Xunit;
using Common.IO.Utility;
using Pisces.IO.Sequencing;

namespace AdaptiveGenotyper.Tests
{
    public class VariantReaderTests
    {
        [Fact]
        public void ParseDepthTests()
        {
            string vcf1Path = Path.Combine(TestPaths.LocalTestDataDirectory, "ParseDepthTest.vcf");
            string vcf2Path = Path.Combine(TestPaths.LocalTestDataDirectory, "VariantDepthReaderTest.vcf");

            VcfVariant variant = new VcfVariant();
            int dp;
            using (VcfReader reader1 = new VcfReader(vcf1Path)) 
            {
                reader1.GetNextVariant(variant);
                dp = VariantReader.ParseDepth(variant);
                Assert.Equal(500, dp);
            }

            using (VcfReader reader2 = new VcfReader(vcf2Path))
            {
                reader2.GetNextVariant(variant);
                dp = VariantReader.ParseDepth(variant);
                Assert.Equal(78, dp);
            }
        }

        [Fact]
        public void GetVFMultiAllelicTest()
        {
            string vcf = Path.Combine(TestPaths.LocalTestDataDirectory, "MultiAllelicVariantTest.vcf");
            VariantReader reader = new VariantReader();
            List<RecalibratedVariantsCollection> results = reader.GetVariantFrequencies(vcf);

            // First entry is reference; check if added for both SNV and indel
            Assert.True(results[0].ContainsKey("chr1:115252175"));
            Assert.Equal(4, results[0].Ad[0]);
            Assert.True(results[1].ContainsKey("chr1:115252175"));
            Assert.Equal(4, results[1].Ad[0]);

            // Second entry 1/0 SNV
            Assert.Equal(75, results[0].Dp[1]);
            Assert.Equal(45, results[0].Ad[1]);

            //  Third entry 1/1 SNV
            Assert.Equal(72, results[0].Ad[2]);

            // Fourth entry multiallelic SNV
            Assert.True(!results[0].ContainsKey("chr1:115252178"));

            // Fifth entry mixed type
            Assert.True(!results[0].ContainsKey("chr1:115252179"));
            Assert.True(!results[1].ContainsKey("chr1:115252179"));

            // Sixth entry multiallelic insertion with 1 major allele
            Assert.True(results[1].ContainsKey("chr1:115252180"));
            Assert.Equal(37, results[1].Ad[1]);
            Assert.Equal(77, results[1].Dp[1]);
        }

        [Fact]
        public void GetVFDeletionTest()
        {
            string vcf = Path.Combine(TestPaths.LocalTestDataDirectory, "DeletionVariantTest.vcf");
            VariantReader reader = new VariantReader();
            List<RecalibratedVariantsCollection> results = reader.GetVariantFrequencies(vcf);

            // First entry should be skipped
            Assert.DoesNotContain(115252175, results[0].ReferencePosition);
            Assert.DoesNotContain(115252175, results[1].ReferencePosition);            

            // Second entry 0/. deletion
            Assert.Equal(115252176, results[1].ReferencePosition[0]);
            Assert.Equal(75, results[1].Dp[0]);
            Assert.Equal(45, results[1].Ad[0]);
            Assert.DoesNotContain(115252177, results[0].ReferencePosition);
            Assert.DoesNotContain(115252177, results[1].ReferencePosition);

            //  Third entry 0/. deletion with multiallelic interior
            Assert.Equal(115252178, results[1].ReferencePosition[1]);
            Assert.DoesNotContain(115252179, results[1].ReferencePosition);
            Assert.DoesNotContain(115252179, results[0].ReferencePosition);

            // Fourth entry 0/. deletion with an interior SNV and an interior insertion
            Assert.Equal(115252180, results[1].ReferencePosition[2]);
            Assert.Equal(115252181, results[0].ReferencePosition[0]);
            Assert.Equal(115252182, results[1].ReferencePosition[3]);
            Assert.DoesNotContain(115252183, results[1].ReferencePosition);
            Assert.DoesNotContain(115252183, results[0].ReferencePosition);

            // Fifth entry is a SNV with an early break from the deletion
            Assert.Equal(115254000, results[0].ReferencePosition[1]);
        }

        [Fact]
        public void ReadDiploidVcfTest()
        {
            string vcf1 = Path.Combine(TestPaths.LocalTestDataDirectory, "diploid1.vcf"); // tests "diploid"
            string vcf2 = Path.Combine(TestPaths.LocalTestDataDirectory, "diploid2.vcf"); // tests "Diploid"
            string vcf3 = Path.Combine(TestPaths.LocalTestDataDirectory, "diploid3.vcf"); // tests "DIPLOID"
            VariantReader reader = new VariantReader();

            Assert.Throws<Exception>(() => reader.GetVariantFrequencies(vcf1));
            Assert.Throws<Exception>(() => reader.GetVariantFrequencies(vcf2));
            Assert.Throws<Exception>(() => reader.GetVariantFrequencies(vcf3));
        }

        [Fact]
        public void ReadMinVqTest()
        {
            string vcf1 = Path.Combine(TestPaths.LocalTestDataDirectory, "minvq1.vcf"); 
            string vcf2 = Path.Combine(TestPaths.LocalTestDataDirectory, "minvq2.vcf");
            string vcf3 = Path.Combine(TestPaths.LocalTestDataDirectory, "minvq3.vcf");
            VariantReader reader = new VariantReader();

            Assert.Throws<Exception>(() => reader.GetVariantFrequencies(vcf1));
            Assert.Throws<Exception>(() => reader.GetVariantFrequencies(vcf2));

            reader = new VariantReader();
            var variants = reader.GetVariantFrequencies(vcf3);
            Assert.True(variants[1].Count > 0);
        }

        [Fact]
        public void ReadCrushedVcfTest()
        {
            Assert.Throws<Exception>(() => new VariantReader().GetVariantFrequencies(
                Path.Combine(TestPaths.LocalTestDataDirectory, "crushed.vcf")));
        }
    }
}
