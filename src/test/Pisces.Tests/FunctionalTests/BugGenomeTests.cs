using System.Collections.Generic;
using System.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Options;
using Xunit;

namespace Pisces.Tests.FunctionalTests
{

    public class BugGenomeTests
    {

        private readonly string _bam_Sample = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1.bam");
        private readonly string _interval_Sample = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1.picard");
        private readonly string _interval_Sample_negative = Path.Combine(TestPaths.LocalTestDataDirectory, "Sample_S1_negative.picard");

        private readonly string _bamSmallS1 = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S2.bam");
        private readonly string _genomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");


        [Fact]
        [Trait("Category", "BamTesting")]
        public void Pisces_Bcereus() // be serious. very, very, serious.
        {
            var bacilusBam = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "Bacillus_cereus", "Sequence", "WholeGenomeFasta");
            functionalTestRunner.OutputDirectory = TestPaths.LocalScratchDirectory;


            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 827,
                    ReferenceAllele = "A",
                    AlternateAllele = "G",
                    Chromosome = "chr"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 1480,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "chr"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 2282,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "chr"
                },

            };

            //chr 827.A   G   100 PASS DP = 37   GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:35,2:37:0.054:1000:-100.0000
            //chr 1480.A   T   100 PASS DP = 18   GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:16,2:18:0.111:1000:-100.0000
            //chr 2282.A   T   100 PASS DP = 21   GT: GQ: AD: DP: VF: NL: SB    0 / 1:100:19,2:21:0.095:1000:-100.0000


            PiscesApplicationOptions appOptions = new PiscesApplicationOptions();
            appOptions.VcfWritingParameters.OutputGvcfFile = true;
            appOptions.BAMPaths = new string[] { bacilusBam };
            appOptions.GenomePaths = new string[] { functionalTestRunner.GenomeDirectory };
            appOptions.OutputDirectory = functionalTestRunner.OutputDirectory;
            appOptions.VariantCallingParameters.NoiseLevelUsedForQScoring = 1000;
            
            var vcfFilePath = Path.Combine(TestPaths.LocalScratchDirectory, "Bcereus_S4.genome.vcf");

            // without reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(bacilusBam, vcfFilePath, null, expectedAlleles, applicationOptions: appOptions);
        }


        [Fact]
        [Trait("Category", "BamTesting")]
        public void Pisces_PhiX() //Phix it and forget it.
        {
            var bacilusBam = Path.Combine(TestPaths.SharedBamDirectory, "PhiX_S3.bam");
            var functionalTestRunner = new SomaticVariantCallerFunctionalTestSetup();
            functionalTestRunner.GenomeDirectory = Path.Combine(TestPaths.SharedGenomesDirectory, "PhiX", "WholeGenomeFasta");
            functionalTestRunner.OutputDirectory = TestPaths.LocalScratchDirectory;


            var expectedAlleles = new List<CalledAllele>
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 14,
                    ReferenceAllele = "T",
                    AlternateAllele = "C",
                    Chromosome = "phix"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 14,
                    ReferenceAllele = "T",
                    AlternateAllele = "G",
                    Chromosome = "phix"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 19,
                    ReferenceAllele = "G",
                    AlternateAllele = "T",
                    Chromosome = "phix"
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 22,
                    ReferenceAllele = "G",
                    AlternateAllele = "A",
                    Chromosome = "phix"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 25,
                    ReferenceAllele = "G",
                    AlternateAllele = "T",
                    Chromosome = "phix"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 26,
                    ReferenceAllele = "A",
                    AlternateAllele = "C",
                    Chromosome = "phix"
                },

                new CalledAllele(AlleleCategory.Snv)
                {
                    ReferencePosition = 42,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Chromosome = "phix"
                }

            };

            //phix    14.T   C   3   q30; LowVariantFreq DP = 236  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:234,1:236:0.00424:1000:-100.0000
            //phix    14.T   G   3   q30; LowVariantFreq DP = 236  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:234,1:236:0.00424:1000:-100.0000
            //phix    19.G   T   3   q30; LowVariantFreq DP = 243  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:242,1:243:0.00412:1000:-100.0000
            //phix    22.G   A   3   q30; LowVariantFreq DP = 225  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:224,1:225:0.00444:1000:-100.0000
            //phix    25.G   T   3   q30; LowVariantFreq DP = 244  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:243,1:244:0.00410:1000:-100.0000
            //phix    26.A   C   3   q30; LowVariantFreq DP = 242  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:241,1:242:0.00413:1000:-100.0000
            //phix    42.A   T   3   q30; LowVariantFreq DP = 199  GT: GQ: AD: DP: VF: NL: SB    0 / 1:3:198,1:199:0.00503:1000:-100.0000


            PiscesApplicationOptions appOptions = new PiscesApplicationOptions();
            appOptions.VcfWritingParameters.OutputGvcfFile = true;
            appOptions.BAMPaths = new string[] { bacilusBam };
            appOptions.GenomePaths = new string[] { functionalTestRunner.GenomeDirectory };
            appOptions.OutputDirectory = functionalTestRunner.OutputDirectory;
            appOptions.VariantCallingParameters.NoiseLevelUsedForQScoring = 1000;
            appOptions.VariantCallingParameters.MinimumFrequency = 0.0001f; //make sure we catch something in this little bam
            appOptions.VariantCallingParameters.MinimumVariantQScore = 3; //make sure we catch something in this little bam

            var vcfFilePath = Path.Combine(TestPaths.LocalScratchDirectory, "PhiX_S3.genome.vcf");

            // without reference calls
            File.Delete(vcfFilePath);
            functionalTestRunner.Execute(bacilusBam, vcfFilePath, null, expectedAlleles, applicationOptions: appOptions);
        }

    }
}