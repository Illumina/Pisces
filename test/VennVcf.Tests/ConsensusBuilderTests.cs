using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;
using Pisces.IO;
using Pisces.IO.Sequencing;
using Pisces.Domain.Options;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Calculators;

namespace VennVcf.Tests
{
    public class ConsensusBuilderTests
    {
        VennVcfOptions _options = new VennVcfOptions();

        [Fact]
        //check consensus builder works on GT "1" and "0" .
        //and in combination with 0/1, etc.
        public void CombineHaploidCalls()
        {
            var hemiAltA = new CalledAllele();
            hemiAltA.Chromosome = "chr1";
            hemiAltA.ReferencePosition = 1;
            hemiAltA.ReferenceAllele = "A";
            hemiAltA.AlternateAllele = "T";
            hemiAltA.Type = AlleleCategory.Snv;
            hemiAltA.TotalCoverage = 100;
            hemiAltA.AlleleSupport = 25;
            hemiAltA.Genotype = Genotype.HemizygousAlt;

            var hemiAltB = new CalledAllele();
            hemiAltB.Chromosome = "chr1";
            hemiAltB.ReferencePosition = 1;
            hemiAltB.ReferenceAllele = "A";
            hemiAltB.AlternateAllele = "T";
            hemiAltB.Type = AlleleCategory.Snv;
            hemiAltB.TotalCoverage = 100;
            hemiAltB.AlleleSupport = 25;
            hemiAltB.Genotype = Genotype.HemizygousAlt;

            var hemiRefA = new CalledAllele();
            hemiRefA.Chromosome = "chr1";
            hemiRefA.ReferencePosition = 1;
            hemiRefA.ReferenceAllele = "A";
            hemiRefA.AlternateAllele = ".";
            hemiRefA.Type = AlleleCategory.Reference;
            hemiRefA.TotalCoverage = 100;
            hemiRefA.AlleleSupport = 25;
            hemiRefA.ReferenceSupport = 25;
            hemiRefA.Genotype = Genotype.HemizygousRef;

            var hemiRefB = new CalledAllele();
            hemiRefB.Chromosome = "chr1";
            hemiRefB.ReferencePosition = 1;
            hemiRefB.ReferenceAllele = "A";
            hemiRefB.AlternateAllele = ".";
            hemiRefB.Type = AlleleCategory.Reference;
            hemiRefB.TotalCoverage = 100;
            hemiRefB.AlleleSupport = 25;
            hemiRefB.ReferenceSupport = 25;
            hemiRefB.Genotype = Genotype.HemizygousRef;

            var hemiNoCallA = new CalledAllele();
            hemiNoCallA.Chromosome = "chr1";
            hemiNoCallA.ReferencePosition = 1;
            hemiNoCallA.ReferenceAllele = "A";
            hemiNoCallA.AlternateAllele = ".";
            hemiNoCallA.Type = AlleleCategory.Reference;
            hemiNoCallA.TotalCoverage = 100;
            hemiNoCallA.AlleleSupport = 25;
            hemiNoCallA.ReferenceSupport = 25;
            hemiNoCallA.Genotype = Genotype.HemizygousNoCall;

            var hemiNoCallB = new CalledAllele();
            hemiNoCallB.Chromosome = "chr1";
            hemiNoCallB.ReferencePosition = 1;
            hemiNoCallB.ReferenceAllele = "A";
            hemiNoCallB.AlternateAllele = ".";
            hemiNoCallB.Type = AlleleCategory.Reference;
            hemiNoCallB.TotalCoverage = 100;
            hemiNoCallB.AlleleSupport = 25;
            hemiNoCallB.ReferenceSupport = 25;
            hemiNoCallB.Genotype = Genotype.HemizygousNoCall;

            var NormalAltA = new CalledAllele();
            NormalAltA.Chromosome = "chr1";
            NormalAltA.ReferencePosition = 1;
            NormalAltA.ReferenceAllele = "A";
            NormalAltA.AlternateAllele = "T";
            NormalAltA.Type = AlleleCategory.Snv;
            NormalAltA.TotalCoverage = 100;
            NormalAltA.AlleleSupport = 25;
            NormalAltA.Genotype = Genotype.HeterozygousAltRef;


            var NormalRefA = new CalledAllele();
            NormalRefA.Chromosome = "chr1";
            NormalRefA.ReferencePosition = 1;
            NormalRefA.ReferenceAllele = "A";
            NormalRefA.AlternateAllele = ".";
            NormalRefA.Type = AlleleCategory.Reference;
            NormalRefA.TotalCoverage = 300;
            NormalRefA.AlleleSupport = 50;
            NormalRefA.ReferenceSupport = 50;
            NormalRefA.Genotype = Genotype.HomozygousRef;

            var cb = new ConsensusBuilder("cbHaploid.vcf", _options);

            CheckSimpleCombinations(hemiAltA, hemiAltB, cb, Genotype.HomozygousAlt, hemiAltA.AlternateAllele, VariantComparisonCase.AgreedOnAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(hemiRefA, hemiRefB, cb, Genotype.HomozygousRef, hemiRefA.AlternateAllele, VariantComparisonCase.AgreedOnReference);
            CheckSimpleCombinations(hemiNoCallA, hemiNoCallB, cb, Genotype.RefLikeNoCall, hemiNoCallA.AlternateAllele, VariantComparisonCase.AgreedOnReference);

            CheckSimpleCombinations(hemiAltA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.AgreedOnAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(hemiRefA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);
            CheckSimpleCombinations(hemiNoCallA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);

            CheckSimpleCombinations(hemiAltA, NormalRefA, cb, Genotype.HeterozygousAltRef, hemiAltA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(hemiRefA, NormalRefA, cb, Genotype.HomozygousRef, NormalRefA.AlternateAllele, VariantComparisonCase.AgreedOnReference);
            CheckSimpleCombinations(hemiNoCallA, NormalRefA, cb, Genotype.HomozygousRef, NormalRefA.AlternateAllele, VariantComparisonCase.AgreedOnReference);


        }

        [Fact]
        //check consensus builder works on GT "1/." and "0/." .
        //and in combination with 0/1, etc.
        public void CombineHalfCallHalfNoCalls()
        {
            var AltAndNocallA = new CalledAllele();
            AltAndNocallA.Chromosome = "chr1";
            AltAndNocallA.ReferencePosition = 1;
            AltAndNocallA.ReferenceAllele = "A";
            AltAndNocallA.AlternateAllele = "T";
            AltAndNocallA.Type = AlleleCategory.Snv;
            AltAndNocallA.TotalCoverage = 100;
            AltAndNocallA.AlleleSupport = 25;
            AltAndNocallA.Genotype = Genotype.AltAndNoCall;

            var AltAndNocallB = new CalledAllele();
            AltAndNocallB.Chromosome = "chr1";
            AltAndNocallB.ReferencePosition = 1;
            AltAndNocallB.ReferenceAllele = "A";
            AltAndNocallB.AlternateAllele = "T";
            AltAndNocallB.Type = AlleleCategory.Snv;
            AltAndNocallB.TotalCoverage = 100;
            AltAndNocallB.AlleleSupport = 25;
            AltAndNocallB.Genotype = Genotype.AltAndNoCall;

            var RefAndNoCallA = new CalledAllele();
            RefAndNoCallA.Chromosome = "chr1";
            RefAndNoCallA.ReferencePosition = 1;
            RefAndNoCallA.ReferenceAllele = "A";
            RefAndNoCallA.AlternateAllele = ".";
            RefAndNoCallA.Type = AlleleCategory.Reference;
            RefAndNoCallA.TotalCoverage = 100;
            RefAndNoCallA.AlleleSupport = 25;
            RefAndNoCallA.ReferenceSupport = 25;
            RefAndNoCallA.Genotype = Genotype.RefAndNoCall;

            var RefAndNocallB = new CalledAllele();
            RefAndNocallB.Chromosome = "chr1";
            RefAndNocallB.ReferencePosition = 1;
            RefAndNocallB.ReferenceAllele = "A";
            RefAndNocallB.AlternateAllele = ".";
            RefAndNocallB.Type = AlleleCategory.Reference;
            RefAndNocallB.TotalCoverage = 100;
            RefAndNocallB.AlleleSupport = 25;
            RefAndNocallB.ReferenceSupport = 25;
            RefAndNocallB.Genotype = Genotype.RefAndNoCall;

            var NocallA = new CalledAllele();
            NocallA.Chromosome = "chr1";
            NocallA.ReferencePosition = 1;
            NocallA.ReferenceAllele = "A";
            NocallA.AlternateAllele = ".";
            NocallA.Type = AlleleCategory.Reference;
            NocallA.TotalCoverage = 100;
            NocallA.AlleleSupport = 25;
            NocallA.ReferenceSupport = 25;
            NocallA.Genotype = Genotype.RefLikeNoCall;

            var NoCallB = new CalledAllele();
            NoCallB.Chromosome = "chr1";
            NoCallB.ReferencePosition = 1;
            NoCallB.ReferenceAllele = "A";
            NoCallB.AlternateAllele = ".";
            NoCallB.Type = AlleleCategory.Reference;
            NoCallB.TotalCoverage = 100;
            NoCallB.AlleleSupport = 25;
            NoCallB.ReferenceSupport = 25;
            NoCallB.Genotype = Genotype.RefLikeNoCall;


            var NormalAltA = new CalledAllele();
            NormalAltA.Chromosome = "chr1";
            NormalAltA.ReferencePosition = 1;
            NormalAltA.ReferenceAllele = "A";
            NormalAltA.AlternateAllele = "T";
            NormalAltA.Type = AlleleCategory.Snv;
            NormalAltA.TotalCoverage = 100;
            NormalAltA.AlleleSupport = 25;
            NormalAltA.Genotype = Genotype.HeterozygousAltRef;


            var NormalRefA = new CalledAllele();
            NormalRefA.Chromosome = "chr1";
            NormalRefA.ReferencePosition = 1;
            NormalRefA.ReferenceAllele = "A";
            NormalRefA.AlternateAllele = ".";
            NormalRefA.Type = AlleleCategory.Reference;
            NormalRefA.TotalCoverage = 300;
            NormalRefA.AlleleSupport = 50;
            NormalRefA.ReferenceSupport = 50;
            NormalRefA.Genotype = Genotype.HomozygousRef;

            var cb = new ConsensusBuilder("cbHalfNocall.vcf", _options);

            CheckSimpleCombinations(AltAndNocallA, AltAndNocallB, cb, Genotype.HomozygousAlt, AltAndNocallB.AlternateAllele, VariantComparisonCase.AgreedOnAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(RefAndNoCallA, RefAndNocallB, cb, Genotype.HomozygousRef, RefAndNocallB.AlternateAllele, VariantComparisonCase.AgreedOnReference);
            CheckSimpleCombinations(NocallA, NoCallB, cb, Genotype.RefLikeNoCall, NocallA.AlternateAllele, VariantComparisonCase.AgreedOnReference);

            CheckSimpleCombinations(AltAndNocallA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.AgreedOnAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(RefAndNoCallA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);
            CheckSimpleCombinations(NocallA, NormalAltA, cb, Genotype.HeterozygousAltRef, NormalAltA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);

            CheckSimpleCombinations(AltAndNocallA, NormalRefA, cb, Genotype.HeterozygousAltRef, AltAndNocallA.AlternateAllele, VariantComparisonCase.OneReferenceOneAlternate);//<- default untill we update consensus code with 1/. etc.
            CheckSimpleCombinations(RefAndNoCallA, NormalRefA, cb, Genotype.HomozygousRef, NormalRefA.AlternateAllele, VariantComparisonCase.AgreedOnReference);
            CheckSimpleCombinations(NocallA, NormalRefA, cb, Genotype.HomozygousRef, NormalRefA.AlternateAllele, VariantComparisonCase.AgreedOnReference);
        }

        private static void CheckSimpleCombinations(
            CalledAllele VarA, CalledAllele VarB, ConsensusBuilder cb, 
            Genotype ExpectedGT, string ExpectedAlt,
            VariantComparisonCase ComparisonCase)
        {
            var AandB = cb.CombineVariants(VarA, VarB, ComparisonCase);
            var BandA = cb.CombineVariants(VarB, VarA, ComparisonCase);
            var AandNull = cb.CombineVariants(VarA, null, VariantComparisonCase.CanNotCombine);
            var NullAndB = cb.CombineVariants(null, VarB, VariantComparisonCase.CanNotCombine);
            int ExpectedAlleleCount = VarA.AlleleSupport + VarB.AlleleSupport;

            if (ComparisonCase == VariantComparisonCase.OneReferenceOneAlternate)
            {
                //for this test, we presume we are calling alternate.
                ExpectedAlleleCount = (VarA.Type == AlleleCategory.Snv) ? VarA.AlleleSupport : VarB.AlleleSupport;
            }

            //check alt-and-nocalls

            Assert.Equal(ExpectedGT, AandB.Genotype); 
            Assert.Equal(VarA.ReferenceAllele, AandB.ReferenceAllele);
            Assert.Equal(ExpectedAlt, AandB.AlternateAllele);
            Assert.Equal(VarA.TotalCoverage+VarB.TotalCoverage, AandB.TotalCoverage);
            Assert.Equal(ExpectedAlleleCount, AandB.AlleleSupport);

            Assert.Equal(ExpectedGT, BandA.Genotype);
            Assert.Equal(VarA.ReferenceAllele, BandA.ReferenceAllele);
            Assert.Equal(ExpectedAlt, BandA.AlternateAllele);
            Assert.Equal(VarA.TotalCoverage + VarB.TotalCoverage, BandA.TotalCoverage);
            Assert.Equal(ExpectedAlleleCount, BandA.AlleleSupport);

            Assert.Equal(ConsensusBuilder.DoDefensiveGenotyping(VarA), AandNull.Genotype); 
            Assert.Equal(VarA.ReferenceAllele, AandNull.ReferenceAllele);
            Assert.Equal(VarA.AlternateAllele, AandNull.AlternateAllele);
            Assert.Equal(VarA.TotalCoverage, AandNull.TotalCoverage);
            Assert.Equal(VarA.AlleleSupport, AandNull.AlleleSupport);

            Assert.Equal(ConsensusBuilder.DoDefensiveGenotyping(VarB), NullAndB.Genotype);
            Assert.Equal(VarB.ReferenceAllele, NullAndB.ReferenceAllele);
            Assert.Equal(VarB.AlternateAllele, NullAndB.AlternateAllele);
            Assert.Equal(VarB.TotalCoverage, NullAndB.TotalCoverage);
            Assert.Equal(VarB.AlleleSupport, NullAndB.AlleleSupport);
        }
    }
}
