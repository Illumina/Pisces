using System;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Utility
{
    public class AlleleHelperTests
    {
        [Fact]
        public void GetAlleleType()
        {
            Assert.Equal(AlleleType.A, AlleleHelper.GetAlleleType("A"));
            Assert.Equal(AlleleType.G, AlleleHelper.GetAlleleType("G"));
            Assert.Equal(AlleleType.C, AlleleHelper.GetAlleleType("C"));
            Assert.Equal(AlleleType.T, AlleleHelper.GetAlleleType("T"));
            Assert.Equal(AlleleType.N, AlleleHelper.GetAlleleType("N"));
            Assert.Equal(AlleleType.N, AlleleHelper.GetAlleleType("U"));
        }

        [Fact]
        public void MapToCandidateAllele()
        {
            var allele = new CalledAllele();
            allele.Chromosome = "chr1";
            allele.ReferencePosition = 1;
            allele.ReferenceAllele = "A";
            allele.AlternateAllele = "T";
            allele.Type = AlleleCategory.Snv;
            allele.SupportByDirection = new[] {10, 20, 30};
            var mappedAllele = AlleleHelper.Map(allele);
            Assert.Equal(mappedAllele.Chromosome, allele.Chromosome);
            Assert.Equal(mappedAllele.ReferencePosition, allele.ReferencePosition);
            Assert.Equal(mappedAllele.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(mappedAllele.AlternateAllele, allele.AlternateAllele);
            Assert.Equal(mappedAllele.Type, allele.Type);
            Assert.Equal(mappedAllele.SupportByDirection.Count(), allele.SupportByDirection.Count());
            for (int i = 0; i < allele.SupportByDirection.Count(); i++)
            {
                Assert.Equal(mappedAllele.SupportByDirection[i], allele.SupportByDirection[i]);
            }
        }

        [Fact]
        public void MapToBaseCalledAllele()
        {
            //Called variant
            var allele = new CandidateAllele("chr1",1,"A","G",AlleleCategory.Snv);
            allele.SupportByDirection = new[] { 10, 20, 30 };
            var BaseCalledAllele = AlleleHelper.Map(allele);
            Assert.True(BaseCalledAllele.Type != AlleleCategory.Reference);
            Assert.Equal(BaseCalledAllele.Chromosome, allele.Chromosome);
            Assert.Equal(BaseCalledAllele.ReferencePosition, allele.ReferencePosition);
            Assert.Equal(BaseCalledAllele.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(BaseCalledAllele.AlternateAllele, allele.AlternateAllele);
            Assert.Equal(BaseCalledAllele.Type, allele.Type);
            Assert.Equal(BaseCalledAllele.SupportByDirection.Count(), allele.SupportByDirection.Count());
            for (int i = 0; i < allele.SupportByDirection.Count(); i++)
            {
                Assert.Equal(BaseCalledAllele.SupportByDirection[i], allele.SupportByDirection[i]);
            }

            //Called reference
            allele.Type = AlleleCategory.Reference;
            var calledReference = AlleleHelper.Map(allele);
            Assert.True(calledReference.Type == AlleleCategory.Reference);
            Assert.Equal(calledReference.Chromosome, allele.Chromosome);
            Assert.Equal(calledReference.ReferencePosition, allele.ReferencePosition);
            Assert.Equal(calledReference.ReferenceAllele, allele.ReferenceAllele);
            Assert.Equal(calledReference.AlternateAllele, allele.AlternateAllele);
            Assert.Equal(calledReference.Type, allele.Type);
            Assert.Equal(calledReference.SupportByDirection.Count(), allele.SupportByDirection.Count());
            for (int i = 0; i < allele.SupportByDirection.Count(); i++)
            {
                Assert.Equal(calledReference.SupportByDirection[i], allele.SupportByDirection[i]);
            }
        }
        
    }
}