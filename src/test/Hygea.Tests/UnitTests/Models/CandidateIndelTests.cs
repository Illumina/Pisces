using RealignIndels.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace RealignIndels.Tests.UnitTests.Models
{
    public class CandidateIndelTests
    {
        [Fact]
        public void Equality()
        {
            var candidate = GetDefaultIndel();
            var otherCandidate = GetDefaultIndel();

            Assert.False(candidate.Equals(null));

            Assert.True(candidate.Equals(otherCandidate));

            otherCandidate.Type = AlleleCategory.Deletion;
            Assert.False(candidate.Equals(otherCandidate));

            otherCandidate = GetDefaultIndel();
            otherCandidate.Chromosome = "chr2";
            Assert.False(candidate.Equals(otherCandidate));

            otherCandidate = GetDefaultIndel();
            otherCandidate.ReferencePosition = 144;
            Assert.False(candidate.Equals(otherCandidate));

            otherCandidate = GetDefaultIndel();
            otherCandidate.ReferenceAllele = "GT";
            Assert.False(candidate.Equals(otherCandidate));

            otherCandidate = GetDefaultIndel();
            otherCandidate.AlternateAllele = "GT";
            Assert.False(candidate.Equals(otherCandidate));

            Assert.False(candidate.Equals(null));
        }

        private CandidateIndel GetDefaultIndel()
        {
            return new CandidateIndel(new CandidateAllele("chr1", 100, "A", "ACG", AlleleCategory.Insertion));
        }
    }
}
