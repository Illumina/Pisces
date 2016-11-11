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
            allele.Coordinate = 1;
            allele.Reference = "A";
            allele.Alternate = "T";
            allele.Type = AlleleCategory.Snv;
            allele.SupportByDirection = new[] {10, 20, 30};
            var mappedAllele = AlleleHelper.Map(allele);
            Assert.Equal(mappedAllele.Chromosome, allele.Chromosome);
            Assert.Equal(mappedAllele.Coordinate, allele.Coordinate);
            Assert.Equal(mappedAllele.Reference, allele.Reference);
            Assert.Equal(mappedAllele.Alternate, allele.Alternate);
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
            Assert.Equal(BaseCalledAllele.Coordinate, allele.Coordinate);
            Assert.Equal(BaseCalledAllele.Reference, allele.Reference);
            Assert.Equal(BaseCalledAllele.Alternate, allele.Alternate);
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
            Assert.Equal(calledReference.Coordinate, allele.Coordinate);
            Assert.Equal(calledReference.Reference, allele.Reference);
            Assert.Equal(calledReference.Alternate, allele.Alternate);
            Assert.Equal(calledReference.Type, allele.Type);
            Assert.Equal(calledReference.SupportByDirection.Count(), allele.SupportByDirection.Count());
            for (int i = 0; i < allele.SupportByDirection.Count(); i++)
            {
                Assert.Equal(calledReference.SupportByDirection[i], allele.SupportByDirection[i]);
            }
        }
        
    }
}