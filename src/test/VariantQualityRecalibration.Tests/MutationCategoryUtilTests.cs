using System;
using Pisces.IO.Sequencing;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class MutationCategoryUtilTests
    {
        [Fact]
        public void GetMutationCategory_VariantInput()
        {
            var v = new VcfVariant();
           
            v.ReferenceAllele = "A";
            v.VariantAlleles = new string[] { "C" };
            Assert.Equal(MutationCategory.AtoC, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "T" };
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "A";
            v.VariantAlleles = new string[] { "c" };
            Assert.Equal(MutationCategory.AtoC, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "g";
            v.VariantAlleles = new string[] { "t" };
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "t" };
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "TT" };
            Assert.Equal(MutationCategory.Insertion, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "GGG";
            v.VariantAlleles = new string[] { "T" };
            Assert.Equal(MutationCategory.Deletion, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "GG";
            v.VariantAlleles = new string[] { "TZ" };
            Assert.Equal(MutationCategory.Other, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "G" };
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "g" };
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "g";
            v.VariantAlleles = new string[] { "G" };
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "g";
            v.VariantAlleles = new string[] { "g" };
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v.ReferenceAllele = "G";
            v.VariantAlleles = new string[] { "." };
            Assert.Equal(MutationCategory.Reference, MutationCategoryUtil.GetMutationCategory(v));
        }

        [Fact]
        public void GetMutationCategory_StringInput()
        {
            
            //Happy Path
            Assert.Equal(MutationCategory.AtoC, MutationCategoryUtil.GetMutationCategory("AtoC"));
            Assert.Equal(MutationCategory.GtoT, MutationCategoryUtil.GetMutationCategory("GtoT"));

            //we dont handle these cases
            Assert.Throws<ArgumentException>(() => (MutationCategory.AtoC, MutationCategoryUtil.GetMutationCategory("Atoc")));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("gtot"));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("TtoTT"));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("GGtoTZ"));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("GtoG"));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("Gto."));
            Assert.Throws<ArgumentException>(() => MutationCategoryUtil.GetMutationCategory("this will never work"));
        }

        [Fact]
        public void GetMutationCategory_IsValidInput()
        {

            //Happy Path
            Assert.True(MutationCategoryUtil.IsValidCategory("AtoC"));
            Assert.True(MutationCategoryUtil.IsValidCategory("GtoT"));

            //we dont handle these cases
            Assert.False(MutationCategoryUtil.IsValidCategory("Atoc"));
            Assert.False(MutationCategoryUtil.IsValidCategory("gtot"));
            Assert.False(MutationCategoryUtil.IsValidCategory("TtoTT"));
            Assert.False(MutationCategoryUtil.IsValidCategory("GGtoTZ"));
            Assert.False(MutationCategoryUtil.IsValidCategory("GtoG"));
            Assert.False(MutationCategoryUtil.IsValidCategory("Gto."));
            Assert.False(MutationCategoryUtil.IsValidCategory("this will never work"));
        }
    }
}
