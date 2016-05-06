using System;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Models.Alleles
{
    public class CandidateAlleleTests
    {
        [Fact]
        public void Constructor_Insertion()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "ATC";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Insertion);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate,candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Equal(2, candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Insertion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Insertion));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Insertion));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Insertion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Insertion));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Insertion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Insertion));   
        }

        [Fact]
        public void Constructor_Deletion()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "ATC";
            var alternate = "A";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Deletion);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate, candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Equal(2, candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Deletion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Deletion));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Deletion));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Deletion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Deletion));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Deletion));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Deletion));
        }

        [Fact]
        public void Constructor_MNV()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "ATC";
            var alternate = "GAA";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate, candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Equal(3, candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Mnv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Mnv));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Mnv));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Mnv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Mnv));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Mnv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Mnv));
        }

        [Fact]
        public void Constructor_SNV()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "T";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Snv);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate, candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Equal(1, candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Snv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Snv));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Snv));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Snv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Snv));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Snv));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Snv));
        }

        [Fact]
        public void Constructor_Reference()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "A";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Reference);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate, candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Equal(1, candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Reference));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Reference));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Reference));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Reference));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Reference));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Reference));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Reference));
            
        }

        [Fact]
        public void Constructor_Undefined()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "A";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Unsupported);
            Assert.Equal(chromosome, candidateVariant.Chromosome);
            Assert.Equal(coordinate, candidateVariant.Coordinate);
            Assert.Equal(reference, candidateVariant.Reference);
            Assert.Equal(alternate, candidateVariant.Alternate);
            //Verify Length
            Assert.Throws<Exception>(() => candidateVariant.Length);

            // error conditions
            // chromosome name
            Assert.Throws<ArgumentException>(() => new CandidateAllele("", coordinate, reference, alternate, AlleleCategory.Unsupported));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(null, coordinate, reference, alternate, AlleleCategory.Unsupported));
            // coordinate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, -1, reference, alternate, AlleleCategory.Unsupported));
            // reference
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, "", alternate, AlleleCategory.Unsupported));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, null, alternate, AlleleCategory.Unsupported));
            // alternate
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, "", AlleleCategory.Unsupported));
            Assert.Throws<ArgumentException>(() => new CandidateAllele(chromosome, coordinate, reference, null, AlleleCategory.Unsupported));

        }

        [Fact]
        public void Equality_Deletion()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "ATC";
            var alternate = "A";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Deletion);
            var otherVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Deletion);
            Assert.True(candidateVariant.Equals(otherVariant));

            // error conditions
            otherVariant = new CandidateAllele("chrX", coordinate, reference, alternate, AlleleCategory.Deletion); // different chrom
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, 9999, reference, alternate, AlleleCategory.Deletion); // different coord
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, "XYZ", alternate, AlleleCategory.Deletion); // different reference
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, reference, "XYZ", AlleleCategory.Deletion); // different alt
            Assert.False(candidateVariant.Equals(otherVariant));
            var otherVariantInsertion = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Insertion); // different variant type
            Assert.False(candidateVariant.Equals(otherVariantInsertion));
            otherVariant = null;
            Assert.False(candidateVariant.Equals(otherVariant));
        }

        [Fact]
        public void Equality_Insertion()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "ATC";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Insertion);
            var otherVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Insertion);
            Assert.True(candidateVariant.Equals(otherVariant));

            // error conditions
            otherVariant = new CandidateAllele("chrX", coordinate, reference, alternate, AlleleCategory.Insertion); // different chrom
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, 9999, reference, alternate, AlleleCategory.Insertion); // different coord
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, "XYZ", alternate, AlleleCategory.Insertion); // different reference
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, reference, "XYZ", AlleleCategory.Insertion); // different alt
            Assert.False(candidateVariant.Equals(otherVariant));
            var otherVariantInsertion = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv); // different variant type
            Assert.False(candidateVariant.Equals(otherVariantInsertion));
            otherVariant = null;
            Assert.False(candidateVariant.Equals(otherVariant));
        }

        [Fact]
        public void Equality_MNV()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "ATC";
            var alternate = "GAA";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv);
            var otherVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv);
            Assert.True(candidateVariant.Equals(otherVariant));

            // error conditions
            otherVariant = new CandidateAllele("chrX", coordinate, reference, alternate, AlleleCategory.Mnv); // different chrom
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, 9999, reference, alternate, AlleleCategory.Mnv); // different coord
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, "XYZ", alternate, AlleleCategory.Mnv); // different reference
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, reference, "XYZ", AlleleCategory.Mnv); // different alt
            Assert.False(candidateVariant.Equals(otherVariant));
            var otherVariantInsertion = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Deletion); // different variant type
            Assert.False(candidateVariant.Equals(otherVariantInsertion));
            otherVariant = null;
            Assert.False(candidateVariant.Equals(otherVariant));
        }

        [Fact]
        public void Equality_SNV()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "G";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Snv);
            var otherVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Snv);
            Assert.True(candidateVariant.Equals(otherVariant));

            // error conditions
            otherVariant = new CandidateAllele("chrX", coordinate, reference, alternate, AlleleCategory.Snv); // different chrom
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, 9999, reference, alternate, AlleleCategory.Snv); // different coord
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, "XYZ", alternate, AlleleCategory.Snv); // different reference
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, reference, "XYZ", AlleleCategory.Snv); // different alt
            Assert.False(candidateVariant.Equals(otherVariant));
            var otherVariantInsertion = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv); // different variant type
            Assert.False(candidateVariant.Equals(otherVariantInsertion));
            otherVariant = null;
            Assert.False(candidateVariant.Equals(otherVariant));
        }

        [Fact]
        public void Equality_Reference()
        {
            // happy path
            var chromosome = "chr1";
            var coordinate = 1;
            var reference = "A";
            var alternate = "A";
            var candidateVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Reference);
            var otherVariant = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Reference);
            Assert.True(candidateVariant.Equals(otherVariant));

            // error conditions
            otherVariant = new CandidateAllele("chrX", coordinate, reference, alternate, AlleleCategory.Reference); // different chrom
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, 9999, reference, alternate, AlleleCategory.Reference); // different coord
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, "XYZ", alternate, AlleleCategory.Reference); // different reference
            Assert.False(candidateVariant.Equals(otherVariant));
            otherVariant = new CandidateAllele(chromosome, coordinate, reference, "XYZ", AlleleCategory.Reference); // different alt
            Assert.False(candidateVariant.Equals(otherVariant));
            var otherVariantInsertion = new CandidateAllele(chromosome, coordinate, reference, alternate, AlleleCategory.Mnv); // different variant type
            Assert.False(candidateVariant.Equals(otherVariantInsertion));
            otherVariant = null;
            Assert.False(candidateVariant.Equals(otherVariant));
        }

        [Fact]
        public void CandidateAllele_IsFullyAnchored()
        {
            var candidateVariant = new CandidateAllele("chr1", 1, "AGC", "AGCTACT", AlleleCategory.Insertion);
            candidateVariant.OpenOnLeft = true;
            candidateVariant.OpenOnRight = true;
            Assert.False(candidateVariant.FullyAnchored);

            candidateVariant.OpenOnLeft = true;
            candidateVariant.OpenOnRight = false;
            Assert.False(candidateVariant.FullyAnchored);

            candidateVariant.OpenOnLeft = false;
            candidateVariant.OpenOnRight = true;
            Assert.False(candidateVariant.FullyAnchored);

            candidateVariant.OpenOnLeft = false;
            candidateVariant.OpenOnRight = false;
            Assert.True(candidateVariant.FullyAnchored);
        }
    }
}