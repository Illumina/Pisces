using System;
using Pisces.Domain.Models.Alleles;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class MutationCategoryUtilTests
    {
        private CalledAllele MakeDummyAllele(string reference, string alt)
        {
            var v = new CalledAllele() { ReferenceAllele = reference, AlternateAllele = alt };
            v.SetType();
            return v;
        }

        [Fact]
        public void GetMutationCategory_VariantInput()
        {
            var v = MakeDummyAllele("A", "C");
            Assert.Equal(MutationCategory.AtoC, MutationCounter.GetMutationCategory(v));
    
            v = MakeDummyAllele("G", "T");
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("A", "c");
            Assert.Equal(MutationCategory.AtoC, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", "t");
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", "t");
            Assert.Equal(MutationCategory.GtoT, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", "TT");
            Assert.Equal(MutationCategory.Insertion, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("GGG", "T");
            Assert.Equal(MutationCategory.Deletion, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("GG", "TZ");
            Assert.Equal(MutationCategory.Other, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", "G");
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", "g");
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("g", "G");
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("g", "g");
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));

            v = MakeDummyAllele("G", ".");
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
