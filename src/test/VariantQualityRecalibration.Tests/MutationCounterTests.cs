using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Sequencing;
using TestUtilities;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class MutationCounterTests
    {
        [Fact]
        public void GetMutationCategory()
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
            v.VariantAlleles = new string[] { "." };
            Assert.Equal(MutationCategory.Reference, MutationCounter.GetMutationCategory(v));


        }
    }
}
