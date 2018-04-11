using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Pisces.IO;
using Pisces.IO.Sequencing;
using Pisces.Domain.Options;
using Pisces.Domain.Models.Alleles;
using Pisces.Calculators;

namespace VennVcf.Tests
{
    public class VennProcessorTests
    {

        string _TestDataPath = TestPaths.LocalTestDataDirectory;
        VennVcfOptions _basicOptions = new VennVcfOptions();
       
        [Fact]
        public void VennVcf_FxnlTest()
        {
            var outDir = TestPaths.LocalScratchDirectory;
            var VcfPathRoot = _TestDataPath;

            string VcfA = Path.Combine(VcfPathRoot, "control_S15.vcf");
            string VcfB = Path.Combine(VcfPathRoot, "control_S18.vcf");
            string OutputPath = Path.Combine(outDir, "Consensus.vcf");
            string ExpectedPath = Path.Combine(VcfPathRoot, "ExpectedConsensus.vcf");

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = Path.Combine(outDir, "Consensus.vcf");
            parameters.OutputDirectory = outDir;
            parameters.DebugMode = true;

            VennProcessor Venn = new VennProcessor(new string[] { VcfA, VcfB }, parameters);
            Venn.DoPairwiseVenn(false);

            Assert.True(File.Exists(OutputPath));

            using (VcfReader ReaderE = new VcfReader(ExpectedPath))
            {
                using (VcfReader ReaderO = new VcfReader(OutputPath))
                {
                    VcfVariant ExpectedVariant = new VcfVariant();
                    VcfVariant OutputVariant = new VcfVariant();

                    while (true)
                    {

                        bool ExpectedVariantExists = ReaderE.GetNextVariant(ExpectedVariant);
                        bool OutputVariantExists = ReaderO.GetNextVariant(OutputVariant);

                        Assert.Equal(ExpectedVariantExists, OutputVariantExists);

                        if (!ExpectedVariantExists || !OutputVariantExists)
                            break;

                        Assert.Equal(ExpectedVariant.ToString(), OutputVariant.ToString());

                    }
                }
            }
        }

        [Fact]
        public void VennVcf_EmptyInputTest()
        {
            var outDir = TestPaths.LocalTestDataDirectory;
            var VcfPathRoot = _TestDataPath;

            string VcfA = Path.Combine(VcfPathRoot, "Empty_S1.vcf");
            string VcfB = Path.Combine(VcfPathRoot, "Empty_S2.vcf");
            string OutputPath = Path.Combine(outDir, "EmptyConsensus.vcf");
            
            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = Path.Combine(outDir, "EmptyConsensus.vcf");
            parameters.OutputDirectory = outDir;
            parameters.DebugMode = true;

            VennProcessor Venn = new VennProcessor(new string[] { VcfA, VcfB }, parameters);
            Venn.DoPairwiseVenn(false);

            Assert.True(File.Exists(OutputPath));
            var observedVariants = VcfReader.GetAllVariantsInFile(OutputPath);

            Assert.Equal(0, observedVariants.Count);
        }


        [Fact]
        public void VennVcf_GtTest()
        {
            var outDir = TestPaths.LocalScratchDirectory;
            var VcfPathRoot = _TestDataPath;

            string VcfA = Path.Combine(VcfPathRoot, "gtTests_S15.vcf");
            string VcfB = Path.Combine(VcfPathRoot, "gtTests_S18.vcf");
            string OutputPath = Path.Combine(outDir, "gtConsensusOut.vcf");
            string ExpectedPath = Path.Combine(VcfPathRoot, "gtConsensus.vcf");

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = OutputPath;
            parameters.OutputDirectory = outDir;

            VennProcessor Venn = new VennProcessor(new string[] { VcfA, VcfB }, parameters);
            Venn.DoPairwiseVenn(false);

            Assert.True(File.Exists(OutputPath));
            var expectedVariants = VcfReader.GetAllVariantsInFile(ExpectedPath);
            var observedVariants = VcfReader.GetAllVariantsInFile(OutputPath);

            Assert.Equal(expectedVariants.Count, observedVariants.Count);

            for(int i=0;i<expectedVariants.Count;i++)
            {
                var ExpectedVariant = expectedVariants[i];
                var OutputVariant = observedVariants[i];
                Assert.Equal(ExpectedVariant.ToString(), OutputVariant.ToString());
            }
        }


        [Fact]
        public void VennVcf_CombineTwoPoolVariants_ProbePoolBias_Tests()
        {

            //this is  from an issue anita had where a variant was in one pool at 1%, the other at 0%, and showed up as 6% in the combined pool.

            var outDir = TestPaths.LocalScratchDirectory;
            var VcfPathRoot = _TestDataPath;

            string VcfPath_PoolA = Path.Combine(VcfPathRoot, "small_S14.genome.vcf");
            string VcfPath_PoolB = Path.Combine(VcfPathRoot, "small_S17.genome.vcf");

          

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = Path.Combine(outDir, "Consensus.vcf");
            parameters.OutputDirectory = outDir;
            if (File.Exists(parameters.ConsensusFileName)) File.Delete(parameters.ConsensusFileName);


            VennProcessor Venn = new VennProcessor(new string[] { VcfPath_PoolA, VcfPath_PoolB }, parameters);
            Venn.DoPairwiseVenn(false);

            Assert.Equal(File.Exists(parameters.ConsensusFileName), true);

            List<VcfVariant> CombinedVariants = VcfReader.GetAllVariantsInFile(parameters.ConsensusFileName);
            List<VcfVariant> AandBVariants = VcfReader.GetAllVariantsInFile(Path.Combine(outDir, "small_S14_and_S17.vcf"));
            List<VcfVariant> BandAVariants = VcfReader.GetAllVariantsInFile(Path.Combine(outDir, "small_S17_and_S14.vcf"));
            List<VcfVariant> AnotBVariants = VcfReader.GetAllVariantsInFile(Path.Combine(outDir, "small_S14_not_S17.vcf"));
            List<VcfVariant> BnotAVariants = VcfReader.GetAllVariantsInFile(Path.Combine(outDir, "small_S17_not_S14.vcf"));

            //poolA
            //chr1	     115258743	.	A	.	100	PASS	DP=35354	GT:GQ:AD:VF:NL:SB	0/0:100:30256:0.1442:20:-100.0000
            //chr1       115258743           .               AC          TT           100         PASS      DP=35354            GT:GQ:AD:VF:NL:SB                0/1:100:30720,4634:0.1311:20:-100.0000
            //chr1       115258744           .               C             .               100         PASS      DP=35253            GT:GQ:AD:VF:NL:SB                0/0:100:30277:0.1412:20:-100.0000
            //chr1       115258745           .               C             .               100         PASS      DP=35160            GT:GQ:AD:VF:NL:SB                0/0:100:35130:0.0009:20:-100.0000


            //poolB
            //chr1       115258743           .               AC          TT           100         PASS      DP=49612            GT:GQ:AD:VF:NL:SB                0/1:100:44202,5410:0.1090:20:-100.0000
            //chr1       115258743           .               A             T              100         PASS      DP=49612            GT:GQ:AD:VF:NL:SB                0/1:100:43362,670:0.0135:20:-46.0807
            //chr1       115258744           .               C             T              24           PASS      DP=49902            GT:GQ:AD:VF:NL:SB                0/1:24:43905,560:0.0112:20:-8.3857


            //when we had bug:
            //chr1       115258743           .               AC          TT           100.00   PASS      DP=84966            GT:GQ:AD:VF:NL:                0/1:100:74922,10044:0.1182:20:-100:-100.0000:100
            //chr1       115258743           .               A             T              100.00   PB;LowVF            DP=49612            GT:GQ:AD:VF:NL:                ./.:100:43362,670:0.0135:20:-46.0807:0.0000:100
            //chr1       115258743           .               A             .               100.00   PASS      DP=35354            GT:GQ:AD:VF:NL:                0/0:100:30256:0.1442:20:-100.0000:-100.0000:100
            //chr1       115258744           .               C             T              100.00   PB           DP=85155            GT:GQ:AD:VF:NL:                0/1:100:74182,5536:0.0650:20:-
            //(issue#1) at 743 we had a A->. in only one pool. It should be marked as BIAS and not PASS.
            //(issue#2) at 744 we had a C->T at 6% when it should be at ~0%, and called as a ref.

            string VFstring = "";
            VcfVariant FunnyResult0 = CombinedVariants[3];
            VFstring = FunnyResult0.Genotypes[0]["VF"] ;
            Assert.Equal(VFstring, "0.144");
            Assert.Equal(FunnyResult0.Filters, "PB");
            Assert.Equal(FunnyResult0.ReferenceAllele, "A");
            Assert.Equal(FunnyResult0.VariantAlleles[0], ".");
            //this used to be a reference as a pass, even though it was only called in one pool.

            VcfVariant FunnyResult = CombinedVariants[6];
            Assert.Equal(FunnyResult.ReferencePosition, 115258744);

            VFstring = FunnyResult.Genotypes[0]["VF"];
            Assert.Equal(VFstring, "0.129");
            Assert.Equal(FunnyResult.Filters, "PASS");
            Assert.Equal(FunnyResult.ReferenceAllele, "C");
            Assert.Equal(FunnyResult.VariantAlleles[0], ".");
            //when we had the bug, this used to get called at 6%. 


            //now, check the Venn functionality:
            Assert.Equal(2, AandBVariants.Count());
            Assert.Equal(2, BandAVariants.Count());
            Assert.Equal(2, AnotBVariants.Count());
            Assert.Equal(0, BnotAVariants.Count());

            Assert.Equal(115258743, AandBVariants[0].ReferencePosition);
            Assert.Equal("AC", AandBVariants[0].ReferenceAllele);
            Assert.Equal("TT", AandBVariants[0].VariantAlleles[0]);

            Assert.Equal(115258747, AandBVariants[1].ReferencePosition);
            Assert.Equal("C", AandBVariants[1].ReferenceAllele);
            Assert.Equal("T", AandBVariants[1].VariantAlleles[0]);

            Assert.Equal(115258743, BandAVariants[0].ReferencePosition);
            Assert.Equal("AC", BandAVariants[0].ReferenceAllele);
            Assert.Equal("TT", BandAVariants[0].VariantAlleles[0]);

            Assert.Equal(115258747, BandAVariants[1].ReferencePosition);
            Assert.Equal("C", BandAVariants[1].ReferenceAllele);
            Assert.Equal("T", BandAVariants[1].VariantAlleles[0]);

            Assert.Equal(115258743, AnotBVariants[0].ReferencePosition);
            Assert.Equal("A", AnotBVariants[0].ReferenceAllele);
            Assert.Equal("T", AnotBVariants[0].VariantAlleles[0]);

            Assert.Equal(115258744, AnotBVariants[1].ReferencePosition);
            Assert.Equal("C", AnotBVariants[1].ReferenceAllele);
            Assert.Equal("T", AnotBVariants[1].VariantAlleles[0]);


        }

        [Fact]
        public void VennVcf_CombineTwoPoolVariants_RulesAthroughD_Tests()
        {
            var outDir = TestPaths.LocalScratchDirectory;
            var VcfPathRoot = _TestDataPath;

            string OutputPath = Path.Combine(outDir, "outEandF.vcf");
            if (File.Exists(OutputPath)) File.Delete(OutputPath);

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = OutputPath;

            string VcfPath_PoolA = Path.Combine(VcfPathRoot, "09H-03403-MT1-1_S7.genome.vcf");
            List<CalledAllele> PoolAVariants = VcfVariantUtilities.Convert(VcfReader.GetAllVariantsInFile(VcfPath_PoolA)).ToList();

            string VcfPath_PoolB = Path.Combine(VcfPathRoot, "09H-03403-MT1-1_S8.genome.vcf");
            List<CalledAllele> PoolBVariants = VcfVariantUtilities.Convert(VcfReader.GetAllVariantsInFile(VcfPath_PoolB)).ToList();

            CalledAllele VariantA =  PoolAVariants[0];
            CalledAllele VariantB = PoolBVariants[0];

            List<CalledAllele[]> pairs = VennProcessor.SelectPairs(
                new List<CalledAllele>()
                {
                    VariantA
                },
                new List<CalledAllele>
                {
                    VariantB
                });

            VariantComparisonCase ComparisonCase = VennProcessor.GetComparisonCase(pairs[0][0], pairs[0][1]);
            ConsensusBuilder consensusBuilder = new ConsensusBuilder("",parameters);
            CalledAllele Consensus = consensusBuilder.CombineVariants(
                VariantA, VariantB, ComparisonCase);

            //Rule "A" test
            //A	if combined VF<1% and less than 2.6% in each pool, call REF
            //(note, we were Alt in one pool and ref in another)

            Assert.Equal(VariantA.Genotype, Pisces.Domain.Types.Genotype.HomozygousRef);
            Assert.Equal(VariantA.Frequency, 0.9979,4);
            Assert.Equal(VariantA.VariantQscore, 100);
            Assert.Equal(VariantA.Filters, new List< Pisces.Domain.Types.FilterType>{ });

            Assert.Equal(VariantB.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantB.Frequency, 0.0173,4);
            Assert.Equal(VariantB.VariantQscore, 100);
            Assert.Equal(VariantB.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(ComparisonCase, VariantComparisonCase.OneReferenceOneAlternate);
            Assert.Equal(Consensus.Genotype, Pisces.Domain.Types.Genotype.HomozygousRef);
            Assert.Equal(Consensus.Frequency, 0.9907,4);
            Assert.Equal(Consensus.VariantQscore, 100);
            Assert.Equal(Consensus.Filters, new List<Pisces.Domain.Types.FilterType> { }); //<-low VF tag will NOT added by post-processing b/c is ref call

            //B	if combined VF<1% and more than 2.6% in one pool, call NO CALL

            VariantA = PoolAVariants[1];
            VariantB = PoolBVariants[1];

            ComparisonCase = VennProcessor.GetComparisonCase(VariantA, VariantB);
            Consensus = consensusBuilder.CombineVariants(
                VariantA, VariantB, ComparisonCase);

            Assert.Equal(VariantA.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantA.Frequency, 0.0776,4);
            Assert.Equal(VariantA.VariantQscore, 100);
            Assert.Equal(VariantA.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(VariantB.Genotype,Pisces.Domain.Types.Genotype.HomozygousRef);
            Assert.Equal(VariantB.Frequency, 0.9989,4);
            Assert.Equal(VariantB.VariantQscore, 100);
            Assert.Equal(VariantB.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(ComparisonCase, VariantComparisonCase.OneReferenceOneAlternate);
            Assert.Equal(Consensus.Genotype, Pisces.Domain.Types.Genotype.AltLikeNoCall);
            Assert.Equal(Consensus.Frequency, 0.0070,4);
            Assert.Equal(Consensus.VariantQscore, 0);
            Assert.Equal(Consensus.Filters, new List<Pisces.Domain.Types.FilterType>
            {Pisces.Domain.Types.FilterType.PoolBias }); //<-low VF tag will also get added by post-processing

            //Rule "Ca" test
            //C-a	if combined 1%<VF<2.6% 
            // and more than 2.6% in one pool and less than 1% in the other, call NO CALL w/PB

            VariantA = PoolAVariants[2];
            VariantB = PoolBVariants[2];

            ComparisonCase = VennProcessor.GetComparisonCase(VariantA, VariantB);
            Consensus = consensusBuilder.CombineVariants(
                VariantA, VariantB, ComparisonCase);

            Assert.Equal(VariantA.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantA.Frequency, 0.0367,4);
            Assert.Equal(VariantA.VariantQscore, 100);
            Assert.Equal(VariantA.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(VariantB.Genotype, Pisces.Domain.Types.Genotype.HomozygousRef);
            Assert.Equal(VariantB.Frequency, 0.9976,4);
            Assert.Equal(VariantB.VariantQscore, 100);
            Assert.Equal(VariantB.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(ComparisonCase, VariantComparisonCase.OneReferenceOneAlternate);
            Assert.Equal(Consensus.Genotype, Pisces.Domain.Types.Genotype.AltLikeNoCall);
            Assert.Equal(Consensus.Frequency, 0.0117,4);
            Assert.Equal(Consensus.VariantQscore, 23);
            Assert.Equal(Consensus.Filters, new List<Pisces.Domain.Types.FilterType> { Pisces.Domain.Types.FilterType.PoolBias});
            //Rule "Cb" test
            //C-a	if combined 1%<VF<2.6% 
            // and more than 2.6% in one pool and between 1% and 2.6% in the other, call NO CALL w/ no PB

            VariantA = PoolAVariants[3];
            VariantB = PoolBVariants[3];

            ComparisonCase = VennProcessor.GetComparisonCase(VariantA, VariantB);
            Consensus = consensusBuilder.CombineVariants(
                VariantA, VariantB, ComparisonCase);

            Assert.Equal(VariantA.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantA.Frequency, 0.01725,4);
            Assert.Equal(VariantA.VariantQscore, 100);
            Assert.Equal(VariantA.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(VariantB.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantB.Frequency, 0.03667,4);
            Assert.Equal(VariantB.VariantQscore, 100);
            Assert.Equal(VariantB.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(ComparisonCase, VariantComparisonCase.AgreedOnAlternate);
            Assert.Equal(Consensus.Genotype, Pisces.Domain.Types.Genotype.AltLikeNoCall);
            Assert.Equal(Consensus.Frequency, 0.02347,4);
            Assert.Equal(Consensus.VariantQscore, 100);
            Assert.Equal(Consensus.Filters, new List<Pisces.Domain.Types.FilterType> { }); //<-low VF tag will also get added by post-processing

            //Rule "D" test
            //D	if combined VF>=2.6% call VARIANT (PB if only present in one pool, using 1% as the cutoff)

            VariantA = PoolAVariants[4];
            VariantB = PoolBVariants[4];

            ComparisonCase = VennProcessor.GetComparisonCase(VariantA, VariantB);
            Consensus = consensusBuilder.CombineVariants(
                VariantA, VariantB, ComparisonCase);

            Assert.Equal(VariantA.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantA.Frequency, 0.2509, 4);
            Assert.Equal(VariantA.VariantQscore, 100);
            Assert.Equal(VariantA.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(VariantB.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(VariantB.Frequency, 0.0367,4);
            Assert.Equal(VariantB.VariantQscore, 100);
            Assert.Equal(VariantB.Filters, new List<Pisces.Domain.Types.FilterType> { });

            Assert.Equal(ComparisonCase, VariantComparisonCase.AgreedOnAlternate);
            Assert.Equal(Consensus.Genotype, Pisces.Domain.Types.Genotype.HeterozygousAltRef);
            Assert.Equal(Consensus.Frequency, 0.1716,4);
            Assert.Equal(Consensus.VariantQscore, 100);
            Assert.Equal(Consensus.Filters, new List<Pisces.Domain.Types.FilterType> { }); //<-low VF tag will also get set by post processor

        }

        [Fact]
        public void VennVcf_CombineTwoPoolVariants_RulesEandF_Tests()
        {

            //Rule "E" test    (ie an Alt+ref call converges to a REf, and we also had a ref call following it)       
            //E	if we end up with multiple REF calls for the same loci, combine those .VCF lines into one ref call.

            //Rule "F" test    (ie various alt calls all ended up as no-call.  we dont want multiple no call lines in the vcf.)                   
            //F	if we end up with multiple NOCALL calls for the same loci, leave those .VCF lines separate

            var outDir = TestPaths.LocalScratchDirectory;
            var VcfPathRoot = _TestDataPath;

            string VcfPath_PoolA = Path.Combine(VcfPathRoot, "RulesEandF_S1.genome.vcf");
            string VcfPath_PoolB = Path.Combine(VcfPathRoot, "RulesEandF_S2.genome.vcf");

            string OutputPath = Path.Combine(outDir, "outEandF.vcf");
            if (File.Exists(OutputPath)) File.Delete(OutputPath);

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.ConsensusFileName = OutputPath;
            parameters.OutputDirectory = outDir;
            VennProcessor VennVcf = new VennProcessor(
                    new string[] { VcfPath_PoolA, VcfPath_PoolB }, parameters);
            VennVcf.DoPairwiseVenn(false);

            Assert.Equal(File.Exists(OutputPath), true);

            List<VcfVariant> PoolAVariants = VcfReader.GetAllVariantsInFile(VcfPath_PoolA);
            List<VcfVariant> PoolBVariants = VcfReader.GetAllVariantsInFile(VcfPath_PoolB);
            List<VcfVariant> CombinedVariants = VcfReader.GetAllVariantsInFile(OutputPath);

            //Rule "E" test    (ie an Alt+ref call converges to a REf, and we also had a ref call following it)       
            //E	if we end up with multiple REF calls for the same loci, combine those .VCF lines into one ref call.

            VcfVariant VariantA_1 = PoolAVariants[0];
            Assert.Equal(VariantA_1.Genotypes[0]["GT"], "0/0");
            Assert.Equal(VariantA_1.Genotypes[0]["VF"], "0.0021");
            Assert.Equal(VariantA_1.Quality, 100);
            Assert.Equal(VariantA_1.Filters, "PASS");
            Assert.Equal(VariantA_1.ReferencePosition, 25378561);

            VcfVariant VariantA_2 = PoolAVariants[1];
            Assert.Equal(VariantA_2.ReferencePosition, 25378562);

            VcfVariant VariantB_1 = PoolBVariants[0];
            Assert.Equal(VariantB_1.Genotypes[0]["GT"], "0/1");
            Assert.Equal(VariantB_1.Genotypes[0]["VF"], "0.0173");
            Assert.Equal(VariantB_1.Quality, 100);
            Assert.Equal(VariantB_1.Filters, "PASS");
            Assert.Equal(VariantB_1.ReferencePosition, 25378561);

            VcfVariant VariantB_2 = PoolBVariants[1];
            Assert.Equal(VariantB_2.Genotypes[0]["GT"], "0/0");
            Assert.Equal(VariantB_2.Genotypes[0]["VF"], "0.0021");
            Assert.Equal(VariantB_2.Quality, 100);
            Assert.Equal(VariantB_2.Filters, "PASS");
            Assert.Equal(VariantB_2.ReferencePosition, 25378561);

            VcfVariant Consensus_1 = CombinedVariants[0];
            Assert.Equal(Consensus_1.Genotypes[0]["GT"], "0/0");
            Assert.Equal(Consensus_1.Genotypes[0]["VF"], "0.009"); //slightly improved from .008
            Assert.Equal(Consensus_1.Quality, 100);
            Assert.Equal(Consensus_1.Filters, "PASS"); //<-low VF tag will NOT added by post-processing b/c is ref call
            Assert.Equal(Consensus_1.ReferencePosition, 25378561);

            VcfVariant Consensus_2 = CombinedVariants[1];
            Assert.Equal(Consensus_2.ReferencePosition, 25378562);

            //Rule "F" test    (ie various alt calls all ended up as no-call. 
            //F	if we end up with multiple NOCALL calls for the same loci, leave those .VCF lines separate


            VariantA_1 = PoolAVariants[1];
            Assert.Equal(VariantA_1.Genotypes[0]["GT"], "0/1");
            Assert.Equal(VariantA_1.Genotypes[0]["VF"], "0.0725");
            Assert.Equal(VariantA_1.Quality, 100);
            Assert.Equal(VariantA_1.Filters, "PASS");
            Assert.Equal(VariantA_1.ReferencePosition, 25378562);

            VariantA_2 = PoolAVariants[2];
            Assert.Equal(VariantA_2.Genotypes[0]["GT"], "0/1");
            Assert.Equal(VariantA_2.Genotypes[0]["VF"], "0.0725");
            Assert.Equal(VariantA_2.Quality, 100);
            Assert.Equal(VariantA_2.Filters, "PASS");
            Assert.Equal(VariantA_2.ReferencePosition, 25378562);

            VcfVariant VariantA_3 = PoolAVariants[3];
            Assert.Equal(VariantA_3.Genotypes[0]["GT"], "0/1");
            Assert.Equal(VariantA_3.Genotypes[0]["VF"], "0.0725");
            Assert.Equal(VariantA_3.Quality, 100);
            Assert.Equal(VariantA_3.Filters, "PASS");
            Assert.Equal(VariantA_3.ReferencePosition, 25378562);

            VariantB_1 = PoolBVariants[2];
            Assert.Equal(VariantB_1.Genotypes[0]["GT"], "0/0");
            Assert.Equal(VariantB_1.Genotypes[0]["VF"], "0.0024");
            Assert.Equal(VariantB_1.Quality, 100);
            Assert.Equal(VariantB_1.Filters, "PASS");
            Assert.Equal(VariantB_1.ReferencePosition, 25378562);

            VariantB_2 = PoolBVariants[3];
            Assert.Equal(VariantB_2.ReferencePosition, 25378563);

            Consensus_1 = CombinedVariants[1];
            Assert.Equal(Consensus_1.ReferencePosition, 25378562);
            Assert.Equal(Consensus_1.Genotypes[0]["GT"], "./.");
            Assert.Equal(Consensus_1.Genotypes[0]["VF"], "0.007");
            Assert.Equal(Consensus_1.Quality, 0);
            Assert.Equal(Consensus_1.Filters, "PB"); //<-low VF tag will also get added by post-processing
            Assert.Equal(Consensus_1.ReferenceAllele, "C");
            Assert.Equal(Consensus_1.VariantAlleles[0], "T");

            Consensus_2 = CombinedVariants[2];
            Assert.Equal(Consensus_2.ReferencePosition, 25378562);
            Assert.Equal(Consensus_2.Genotypes[0]["GT"], "./.");
            Assert.Equal(Consensus_2.Genotypes[0]["VF"], "0.007");
            Assert.Equal(Consensus_2.Quality, 0);
            Assert.Equal(Consensus_2.Filters, "PB"); //<-low VF tag will also get added by post-processing
            Assert.Equal(Consensus_2.ReferenceAllele, "C");
            Assert.Equal(Consensus_2.VariantAlleles[0], "TT");

            VcfVariant Consensus_3 = CombinedVariants[3];
            Assert.Equal(Consensus_3.ReferencePosition, 25378562);
            Assert.Equal(Consensus_3.Genotypes[0]["GT"], "./.");
            Assert.Equal(Consensus_3.Genotypes[0]["VF"], "0.007");
            Assert.Equal(Consensus_3.Quality, 0);
            Assert.Equal(Consensus_3.Filters, "PB"); //<-low VF tag will also get added by post-processing
            Assert.Equal(Consensus_3.ReferenceAllele, "CC");
            Assert.Equal(Consensus_3.VariantAlleles[0], "T");

            VcfVariant Consensus_4 = CombinedVariants[4];
            Assert.Equal(Consensus_4.ReferencePosition, 25378563);

            if (File.Exists(OutputPath)) File.Delete(OutputPath);

        }

        [Fact]
        public void VennVcf_CombineTwoPoolVariants_Qscore_Test()
        {
            //Var A, kdot-43_S3.genome.vcf
            //chr3	41266161	.	A	G	30	PASS	DP=3067	GT:GQ:AD:VF:NL:SB	0/1:30:3005,54:0.0176:35:-100.0000
            CalledAllele VarA = new CalledAllele();
            VarA.Chromosome = "chr3";
            VarA.ReferencePosition = 41266161;
            VarA.TotalCoverage = 3067;
            VarA.Genotype = Pisces.Domain.Types.Genotype.HeterozygousAltRef;
            VarA.GenotypeQscore = 30;
            VarA.AlleleSupport = 54;
            VarA.ReferenceSupport = 3005;
            //              "VF", "0.0176"
            VarA.NoiseLevelApplied = 35;
            VarA.StrandBiasResults = new Pisces.Domain.Models.BiasResults() { GATKBiasScore = -100 };
            VarA.ReferenceAllele = "A";
            VarA.AlternateAllele = "G";
            VarA.Filters = new List<Pisces.Domain.Types.FilterType>();
            VarA.VariantQscore = 30;
            VarA.Type = Pisces.Domain.Types.AlleleCategory.Snv;

            //Var B, kdot-43_S4.genome.vcf
            //chr3	41266161	.	A	.	75	PASS	DP=3795	GT:GQ:AD:VF:NL:SB	0/0:75:3780:0.0040:35:-100.0000
            CalledAllele VarB = new CalledAllele();
            VarB.Chromosome = "chr3";
            VarB.ReferencePosition = 41266161;
            VarB.TotalCoverage = 3795;
            VarB.Genotype = Pisces.Domain.Types.Genotype.HomozygousRef;
            VarB.GenotypeQscore = 75;
            VarB.AlleleSupport = 3780;
            VarB.ReferenceSupport = 3780;
            //              "VF", "0.0040"
            VarB.NoiseLevelApplied = 35;
            VarB.StrandBiasResults = new Pisces.Domain.Models.BiasResults() { GATKBiasScore = -100 };           
            VarB.ReferenceAllele = "A";
            VarB.AlternateAllele = ".";
            VarB.Filters = new List<Pisces.Domain.Types.FilterType>();
            VarB.VariantQscore = 75;
            VarB.Type = Pisces.Domain.Types.AlleleCategory.Reference;

            //old answer
            //chr3	41266161	.	A	.	100.00	PASS	DP=6862;cosmic=COSM1423020,COSM1423021;EVS=0|69.0|6503;phastCons	GT:GQ:AD:VF:NL:SB:PB:GQX	0/0:100:6785:0.0079:35:-100:-100.0000:100

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.VariantCallingParams.MinimumFrequency = 0.01f;
            parameters.BamFilterParams.MinimumBaseCallQuality = 20;
            parameters.SampleAggregationParameters.ProbePoolBiasThreshold = 0.5f;
            parameters.SampleAggregationParameters.HowToCombineQScore = SampleAggregationParameters.CombineQScoreMethod.TakeMin;

            VariantComparisonCase ComparisonCase = VennProcessor.GetComparisonCase(VarA, VarB);
            ConsensusBuilder consensusBuilder = new ConsensusBuilder("",parameters);
            AggregateAllele consensus = consensusBuilder.CombineVariants(VarA, VarB, ComparisonCase);

            //GT:GQ:AD:VF:NL:SB:PB:GQX	0/0:100:6785:0.0079:35:-100:-100.0000:100
            Assert.NotNull(consensus);
            Assert.Equal(consensus.VariantQscore, 100);
            Assert.Equal(consensus.GenotypeQscore, 100);
            Assert.Equal(consensus.ReferenceAllele, "A");
            Assert.Equal(consensus.AlternateAllele, ".");
            Assert.Equal(consensus.Genotype, Pisces.Domain.Types.Genotype.HomozygousRef );
            Assert.Equal(consensus.GenotypeQscore, 100);
            Assert.Equal(consensus.AlleleSupport, 6785);
            Assert.Equal(consensus.ReferenceSupport, 6785);
            Assert.Equal(consensus.TotalCoverage, 6862);
            Assert.Equal(consensus.Frequency, 0.98877, 4);
            Assert.Equal(consensus.NoiseLevelApplied, 35);
            Assert.Equal(consensus.StrandBiasResults.GATKBiasScore, -100);
            Assert.Equal(consensus.PoolBiasResults.GATKBiasScore, -100);

        }

        [Fact]
        public void VennVcf_RegexTest()
        {

            string SampleName, SampleNum;
            VennProcessor.GuessSampleNameFromVcf("C43-Ct-4_S2.genome.vcf.gz", out SampleName, out SampleNum);

            Assert.Equal(SampleName, "C43-Ct-4");
            Assert.Equal(SampleNum, "S2");

            SampleName = "X";
            SampleNum = "X";
            VennProcessor.GuessSampleNameFromVcf("C43-Ct-4_S3.vcf.gz", out SampleName, out SampleNum);

            Assert.Equal(SampleName, "C43-Ct-4");
            Assert.Equal(SampleNum, "S3");

            SampleName = "X";
            SampleNum = "X";
            VennProcessor.GuessSampleNameFromVcf("C43-Ct-4_S12.vcf", out SampleName, out SampleNum);

            Assert.Equal(SampleName, "C43-Ct-4");
            Assert.Equal(SampleNum, "S12");

            SampleName = "X";
            SampleNum = "X";
            VennProcessor.GuessSampleNameFromVcf("C43-Ct-4_S12.vcf.ant", out SampleName, out SampleNum);

            Assert.Equal(SampleName, "C43-Ct-4_S12.ant");
            Assert.Equal(SampleNum, "C43-Ct-4_S12.ant");

            VennProcessor.GuessSampleNameFromVcf("C43-Ct-4_S12.boo", out SampleName, out SampleNum);

            Assert.Equal(SampleName, "C43-Ct-4_S12.boo");
            Assert.Equal(SampleNum, "C43-Ct-4_S12.boo");
        }

        [Fact]
        public void VennVcf_CombineTwoPoolVariants_MergeRefCalls()
        {
            //this is  from an issue where there were multiple co-located variants in one pool,
            //and just ref in the other, at chr15	92604460.  The consensus answer should be 
            // a single ref call (and not multiple ref calls!). 
            var outDir = TestPaths.LocalScratchDirectory;
            var vcfPathRoot = _TestDataPath;
            
            string VcfPath_PoolA = Path.Combine(vcfPathRoot, "C64-Ct-4_S17.genome.vcf");
            string VcfPath_PoolB = Path.Combine(vcfPathRoot, "C64-Ct-4_S18.genome.vcf");
            string VcfPath_Consensus = Path.Combine(vcfPathRoot, "ExpectedConsensus2.vcf");

            string OutputPath = Path.Combine(outDir, "Consensus2.vcf");
            if (File.Exists(OutputPath)) File.Delete(OutputPath);

            VennVcfOptions parameters = new VennVcfOptions();
            parameters.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            parameters.InputFiles = new string[] { VcfPath_PoolA, VcfPath_PoolB };
            parameters.OutputDirectory = outDir; //Path.Combine(outDir, "RefMergeOut.vcf");
            parameters.ConsensusFileName = OutputPath;
            VennProcessor venn = new VennProcessor(parameters.InputFiles, parameters);
            venn.DoPairwiseVenn(false);

            Assert.Equal(File.Exists(OutputPath), true);
            List<VcfVariant> CombinedVariants = VcfReader.GetAllVariantsInFile(OutputPath);
            List<VcfVariant> ExpectedVariants = VcfReader.GetAllVariantsInFile(VcfPath_Consensus);
            Assert.Equal(ExpectedVariants.Count, CombinedVariants.Count);

            int NumVariantsAtPos92604460 = 0;

            for (int i = 0; i < ExpectedVariants.Count; i++)
            {
                VcfVariant EVariant = ExpectedVariants[i];
                VcfVariant Variant = CombinedVariants[i];

                if ((Variant.ReferencePosition == 92604460)
                    && (Variant.ReferenceName == "chr15"))
                {
                    NumVariantsAtPos92604460++;
                }

                Assert.Equal(EVariant.ToString(), Variant.ToString());
            }

            Assert.Equal(NumVariantsAtPos92604460, 1);

        }

        //this issue only recently came up, when we hade to combine recalibrated Q scores.
        // 1) check we can combine NL correctly.
        // 2) check we dont barf if either input is null.
        [Fact]
        public void VennVcf_CombineTwoPoolVariants_Qscore_DiffentNL_Test()
        {
            //chr3	41266161	.	A	G	30	PASS	DP=3067	GT:GQ:AD:VF:NL:SB	0/1:30:3005,54:0.0176:35:-100.0000
            CalledAllele VarA = new CalledAllele()
            {
                Chromosome = "chr3",
                ReferencePosition = 41266161,
                TotalCoverage = 3067,
                Genotype = Pisces.Domain.Types.Genotype.HeterozygousAltRef,
                VariantQscore = 30,
                GenotypeQscore = 30,
                AlleleSupport = 54,
                ReferenceSupport = 3005,
                NoiseLevelApplied = 35,
                StrandBiasResults = new Pisces.Domain.Models.BiasResults()
                        { GATKBiasScore = -100.0000 },
                ReferenceAllele = "A",
                AlternateAllele = "G",
                Type = Pisces.Domain.Types.AlleleCategory.Snv
            };


            ///chr3	41266161	.	A	.	75	PASS	DP=3795	GT:GQ:AD:VF:NL:SB	0/0:75:3780:0.0040:2:-100.0000
            CalledAllele VarB = new CalledAllele()
            {
                Chromosome = "chr3",
                ReferencePosition = 41266161,
                TotalCoverage = 3795,
                Genotype = Pisces.Domain.Types.Genotype.HomozygousRef,
                VariantQscore = 75,
                GenotypeQscore = 75,
                AlleleSupport = 3780,
                ReferenceSupport = 3780,
                NoiseLevelApplied = 2,
                StrandBiasResults = new Pisces.Domain.Models.BiasResults()
                        { GATKBiasScore = -100.0000 },
                ReferenceAllele = "A",
                AlternateAllele = ".",
                Type = Pisces.Domain.Types.AlleleCategory.Reference
            };
           
      

            //old answer
            //chr3	41266161	.	A	.	100.00	PASS	DP=6862;cosmic=COSM1423020,COSM1423021;EVS=0|69.0|6503;phastCons	GT:GQ:AD:VF:NL:SB:PB:GQX	0/0:100:6785:0.0079:35:-100:-100.0000:100

            SampleAggregationParameters SampleAggregationOptions = new SampleAggregationParameters();
            SampleAggregationOptions.ProbePoolBiasThreshold = 0.5f;
            SampleAggregationOptions.HowToCombineQScore = SampleAggregationParameters.CombineQScoreMethod.CombinePoolsAndReCalculate;
            
            _basicOptions.BamFilterParams.MinimumBaseCallQuality = 20;
            _basicOptions.VariantCallingParams.MinimumFrequency = 0.01f;
            _basicOptions.VariantCallingParams.MinimumFrequencyFilter = 0.03f;
            _basicOptions.SampleAggregationParameters = SampleAggregationOptions;

            string consensusOut = Path.Combine(_TestDataPath, "ConsensusOut.vcf");
            VariantComparisonCase ComparisonCase = VennProcessor.GetComparisonCase(VarA, VarB);
            ConsensusBuilder consensusBuilder = new ConsensusBuilder(consensusOut,_basicOptions);
            AggregateAllele consensus = consensusBuilder.CombineVariants(VarA, VarB, ComparisonCase);
            Console.WriteLine(consensus.ToString());

            double expectedNoiseLevel = MathOperations.PtoQ(
                (MathOperations.QtoP(35) + MathOperations.QtoP(2)) / (2.0)); // 5

            //GT:GQ:AD:VF:NL:SB:PB:GQX	0/0:100:6785:0.0079:35:-100:-100.0000:100
            Assert.NotNull(consensus);
            Assert.Equal(consensus.VariantQscore, 100);
            Assert.Equal(consensus.ReferenceAllele, "A");
            Assert.Equal(consensus.AlternateAllele, ".");
            Assert.Equal(consensus.Genotype, Pisces.Domain.Types.Genotype.HomozygousRef);
            Assert.Equal(consensus.TotalCoverage, 6862);
            Assert.Equal(consensus.ReferenceSupport, 6785);
            Assert.Equal(consensus.AlleleSupport, 6785);
            Assert.Equal(consensus.GenotypeQscore, 100);
            Assert.Equal(consensus.Frequency, 0.98877877f);
            Assert.Equal(consensus.NoiseLevelApplied, ((int)expectedNoiseLevel));
            Assert.Equal(consensus.NoiseLevelApplied, 5);
            Assert.Equal(consensus.StrandBiasResults.GATKBiasScore, -100);
            Assert.Equal(consensus.PoolBiasResults.GATKBiasScore, -100.0000);
            
    
            //now check, we take the min NL score if we are taking the min Q score.
            // (in this case of combined alt+ref -> ref, the q score will still need to be recalculated.
            //just with the MIN NL.
            SampleAggregationOptions.HowToCombineQScore = SampleAggregationParameters.CombineQScoreMethod.TakeMin;
            ComparisonCase = VennProcessor.GetComparisonCase(VarA, VarB);
            consensus = consensusBuilder.CombineVariants(VarA, VarB, ComparisonCase);
            Assert.Equal(consensus.NoiseLevelApplied, 2);
            Assert.Equal(consensus.VariantQscore, 100);


            //ok, now sanity check we dont barf if either input is null:
            ComparisonCase = VennProcessor.GetComparisonCase(VarA, null);
            consensus = consensusBuilder.CombineVariants(VarA, null, ComparisonCase);
            Assert.Equal(consensus.NoiseLevelApplied, 35);
            Assert.Equal(consensus.VariantQscore, 100);

            ComparisonCase = VennProcessor.GetComparisonCase(null, VarB);
            consensus = consensusBuilder.CombineVariants(null, VarB, ComparisonCase);
            Assert.Equal(consensus.NoiseLevelApplied, 2);
            Assert.Equal(consensus.VariantQscore, 100);

            //ok, lets check this again, for the PoolQScores option.    
            //sanity check we dont barf if either input is null:
            SampleAggregationOptions.HowToCombineQScore = SampleAggregationParameters.CombineQScoreMethod.CombinePoolsAndReCalculate;
            ComparisonCase = VennProcessor.GetComparisonCase(VarA, null);
            consensus = consensusBuilder.CombineVariants(VarA, null, ComparisonCase);
            Assert.Equal(consensus.NoiseLevelApplied, 35);
            Assert.Equal(consensus.VariantQscore, 100);//low freq variant -> nocall. note, qscore would be 41 if NL = 20.

            ComparisonCase = VennProcessor.GetComparisonCase(null, VarB);
            consensus = consensusBuilder.CombineVariants(null, VarB, ComparisonCase);
            Assert.Equal(consensus.NoiseLevelApplied, 2);
            Assert.Equal(consensus.VariantQscore, 100); //sold ref

    
        }


    }
}