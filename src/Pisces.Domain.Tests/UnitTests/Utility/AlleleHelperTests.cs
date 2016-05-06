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
            var allele = new BaseCalledAllele();
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
            var calledvariant = AlleleHelper.Map(allele);
            Assert.True(calledvariant is CalledVariant);
            Assert.Equal(calledvariant.Chromosome, allele.Chromosome);
            Assert.Equal(calledvariant.Coordinate, allele.Coordinate);
            Assert.Equal(calledvariant.Reference, allele.Reference);
            Assert.Equal(calledvariant.Alternate, allele.Alternate);
            Assert.Equal(calledvariant.Type, allele.Type);
            Assert.Equal(calledvariant.SupportByDirection.Count(), allele.SupportByDirection.Count());
            for (int i = 0; i < allele.SupportByDirection.Count(); i++)
            {
                Assert.Equal(calledvariant.SupportByDirection[i], allele.SupportByDirection[i]);
            }

            //Called reference
            allele.Type = AlleleCategory.Reference;
            var calledReference = AlleleHelper.Map(allele);
            Assert.True(calledReference is CalledReference);
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