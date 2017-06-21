using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models.Alleles;
using TestUtilities;
using VariantPhasing.Logic;
using Pisces.IO;
using Pisces.Domain.Types;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class PhasedVcfWriterTests
    {
        private string _outputFile = Path.Combine(TestPaths.LocalScratchDirectory, "PhasedVcfWriterTest.Output.vcf");
        private List<string> _origHeader = new List<string> { "HeaderLine1", "HeaderLine2", "FinalLine" };


        // Test the Filter Header section is correct.
        [Fact]
        public void FilterHeader()
        {
            var outputFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "PhasedVcfFileWriterTests.vcf");
            File.Delete(outputFilePath);

            var context = new VcfWriterInputContext
            {
                CommandLine = new [] { "myCommandLine"},
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };

            // Variant strand bias too high or coverage on only one strand 
            var config = new VcfWriterConfig
            {
                DepthFilterThreshold = 500,
                VariantQualityFilterThreshold = 30,
                FrequencyFilterThreshold = 0.007f,
                ShouldOutputNoCallFraction = true,
                ShouldOutputStrandBiasAndNoiseLevel = true,            
                EstimatedBaseCallQuality = 23,
                PloidyModel = PloidyModel.Diploid,
            };

            //note, scylla has no SB or RMxN or R8 filters.


            var variants = new List<CalledAllele>
            {
                PhasedVariantTestUtilities.CreateDummyAllele("chrX", 123, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr10", 124, "A", "C",1000, 156),
            };

             var originalHeader = new List<string>
            {
                "##fileformat=VCFv4.1",
                "##fileDate=20160620",
                "##source=Pisces 1.0.0.0",
                "##Pisces_cmdline=\"-B KRAS_42_S1.bam -g -MinimumFrequency 0.01 -MinBaseCallQuality 21 -MaxVariantQScore 100 -MinCoverage 300 -MaxAcceptableStrandBiasFilter 0.5 -MinVariantQScore 20 -VariantQualityFilter 20 -gVCF true -CallMNVs True -out \\myout",
                "##reference=WholeGenomeFASTA",
                "##INFO=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth\">",
                "##FILTER=<ID=q20,Description=\"Quality score less than 20\">",
                "##FILTER=<ID=SB,Description=\"Variant strand bias too high\">",
                "##FILTER=<ID=R5x9,Description=\"Repeats of part or all of the variant allele (max repeat length 5) in the reference greater than or equal to 9\">",
                "##FORMAT=<ID=GT,Number=1,Type=String,Description=\"Genotype\">",
                "##FORMAT=<ID=GQ,Number=1,Type=Integer,Description=\"Genotype Quality\">",
                                "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	HD700n560_miseq1_S7.bam"
             };


            var writer = new PhasedVcfWriter(outputFilePath, config, new VcfWriterInputContext(), originalHeader, null);

            writer.WriteHeader();
            writer.Write(variants);
            writer.Dispose();

            VcfReader reader = new VcfReader(outputFilePath);
            List<string> writtenHeader = reader.HeaderLines;
            reader.Dispose();

            var expectedHeader1 = new List<string>
            {
                "##fileformat=VCFv4.1",
                "##fileDate=20160620",
                "##source=Pisces 1.0.0.0",
                "##Pisces_cmdline=\"-B KRAS_42_S1.bam -g -MinimumFrequency 0.01 -MinBaseCallQuality 21 -MaxVariantQScore 100 -MinCoverage 300 -MaxAcceptableStrandBiasFilter 0.5 -MinVariantQScore 20 -VariantQualityFilter 20 -gVCF true -CallMNVs True -out \\myout",
                "##VariantPhaser=Scylla 1.0.0.0",
                "##reference=WholeGenomeFASTA",
                "##INFO=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth\">",
                "##FILTER=<ID=q20,Description=\"Quality score less than 20\">",
                "##FILTER=<ID=SB,Description=\"Variant strand bias too high\">",
                "##FILTER=<ID=R5x9,Description=\"Repeats of part or all of the variant allele (max repeat length 5) in the reference greater than or equal to 9\">",
                "##FILTER=<ID=q30,Description=\"Quality score less than 30, by Scylla\">",
                "##FILTER=<ID=LowDP,Description=\"Low coverage (DP tag), therefore no genotype called, by Scylla\">",
                "##FILTER=<ID=LowVariantFreq,Description=\"Variant frequency less than 0.0070, by Scylla\">",
                "##FILTER=<ID=MultiAllelicSite,Description=\"Variant does not conform to diploid model, by Scylla\">",
                "##FORMAT=<ID=GT,Number=1,Type=String,Description=\"Genotype\">",
                "##FORMAT=<ID=GQ,Number=1,Type=Integer,Description=\"Genotype Quality\">",
                "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	HD700n560_miseq1_S7.bam"
            };


            Assert.Equal(expectedHeader1.Count, writtenHeader.Count);
            for (int i = 0; i < expectedHeader1.Count; i++)
            {
                //let version numbers differ
                if (expectedHeader1[i].StartsWith("##VariantPhaser=Scylla"))
                {
                    Assert.True(writtenHeader[i].StartsWith("##VariantPhaser=Scylla"));
                    continue;
                }
                Assert.Equal(expectedHeader1[i], writtenHeader[i]);
            }

            config = new VcfWriterConfig
            {
                DepthFilterThreshold = 500,
                VariantQualityFilterThreshold = 22,
                FrequencyFilterThreshold = 0.007f,
                EstimatedBaseCallQuality = 23,
                PloidyModel = PloidyModel.Somatic,
            };


            originalHeader = new List<string>
            {
                "##fileformat=VCFv4.1",
                "##fileDate=20160620",
                "##source=Pisces 1.0.0.0",
                "##Pisces_cmdline=\"-B KRAS_42_S1.bam -g -MinimumFrequency 0.01 -MinBaseCallQuality 21 -MaxVariantQScore 100 -MinCoverage 300 -MaxAcceptableStrandBiasFilter 0.5 -MinVariantQScore 20 -VariantQualityFilter 20 -gVCF true -CallMNVs True -out \\myout",
                "##reference=WholeGenomeFASTA",
                "##INFO=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth\">",
                "##FORMAT=<ID=GT,Number=1,Type=String,Description=\"Genotype\">",
                "##FORMAT=<ID=GQ,Number=1,Type=Integer,Description=\"Genotype Quality\">",
                "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	HD700n560_miseq1_S7.bam"
             };
            writer = new PhasedVcfWriter(outputFilePath, config, new VcfWriterInputContext(), originalHeader, null);


            var expectedHeader2 = new List<string>
            {
                "##fileformat=VCFv4.1",
                "##fileDate=20160620",
                "##source=Pisces 1.0.0.0",
                "##Pisces_cmdline=\"-B KRAS_42_S1.bam -g -MinimumFrequency 0.01 -MinBaseCallQuality 21 -MaxVariantQScore 100 -MinCoverage 300 -MaxAcceptableStrandBiasFilter 0.5 -MinVariantQScore 20 -VariantQualityFilter 20 -gVCF true -CallMNVs True -out \\myout",
                "##VariantPhaser=Scylla 1.0.0.0",
                "##reference=WholeGenomeFASTA",
                "##INFO=<ID=DP,Number=1,Type=Integer,Description=\"Total Depth\">",
                "##FORMAT=<ID=GT,Number=1,Type=String,Description=\"Genotype\">",
                "##FORMAT=<ID=GQ,Number=1,Type=Integer,Description=\"Genotype Quality\">",
                "##FILTER=<ID=q22,Description=\"Quality score less than 22, by Scylla\">",
                "##FILTER=<ID=LowDP,Description=\"Low coverage (DP tag), therefore no genotype called, by Scylla\">",
                "##FILTER=<ID=LowVariantFreq,Description=\"Variant frequency less than 0.0070, by Scylla\">",
                "#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	HD700n560_miseq1_S7.bam"
            };

            writer.WriteHeader();
            writer.Write(variants);
            writer.Dispose();

            reader = new VcfReader(outputFilePath);
            writtenHeader = reader.HeaderLines;
            reader.Dispose();

            Assert.Equal(expectedHeader2.Count, writtenHeader.Count);
            for (int i = 0; i < expectedHeader2.Count; i++)
            {
                //let version numbers differ
                if (expectedHeader1[i].StartsWith("##VariantPhaser=Scylla"))
                {
                    Assert.True(writtenHeader[i].StartsWith("##VariantPhaser=Scylla"));
                    continue;
                }
                Assert.Equal(expectedHeader2[i], writtenHeader[i]);
            }
        }

        [Fact]
        public void Write()
        {
            //write a normal vcf
            var writer = InitializeWriter(false);

            //Writer should order the variants by chrom, coord, ref, then alt.
            var variants = new List<CalledAllele>
            {
                PhasedVariantTestUtilities.CreateDummyAllele("chrX", 123, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr10", 124, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr9", 123, "T", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr9", 123, "T", "A",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr9", 123, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr8", 123, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chr9", 124, "A", "C",1000, 156),
                PhasedVariantTestUtilities.CreateDummyAllele("chrM", 123, "A", "C",1000, 156),
            };

            // Order should be:
            var expected = new List<string> {
                "chrM\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr8\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t123\t.\tT\tA\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t123\t.\tT\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t124\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr10\t124\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chrX\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000" };

            writer.Write(variants);
            writer.Dispose();

            Assert.Throws<IOException>(() => writer.WriteHeader());
            Assert.Throws<IOException>(() => writer.Write(new List<CalledAllele> { PhasedVariantTestUtilities.CreateDummyAllele("chr1", 123, "A", "G", 1000, 156) }));
            writer.Dispose();

            var fileLines = File.ReadAllLines(_outputFile);

            Assert.Equal(variants.Count, fileLines.Length);

            for (int i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], fileLines[i]);

            //write a crushed vcf
            writer = InitializeWriter(true);
            writer.Write(variants);
            writer.Dispose();
            fileLines = File.ReadAllLines(_outputFile);

            expected = new List<string> {
                "chrM\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr8\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t123\t.\tA\tC,A,C\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr9\t124\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chr10\t124\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000",
                "chrX\t123\t.\tA\tC\t100\tPASS\tDP=1000\tGT:GQ:AD:DP:VF:NL:SB:NC\t0/1:0:844,156:1000:0.156:0:0.0000:0.0000" };

            Assert.Equal(6, fileLines.Length); //only variants at diff positions
            for (int i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], fileLines[i]);

        }

        [Fact]
        public void WriteHeader()
        {
            //WriteHeader should write the original header and add a line about phaser used right before the column headers

            var writer = InitializeWriter(false);

            writer.WriteHeader();
            writer.Dispose();

            Assert.Throws<IOException>(() => writer.WriteHeader());
            Assert.Throws<IOException>(() => writer.Write(new List<CalledAllele>{PhasedVariantTestUtilities.CreateDummyAllele("chr1",123,"A","G", 1000, 156) }));
            writer.Dispose();

            Assert.True(File.Exists(_outputFile));
            var fileLines = File.ReadAllLines(_outputFile);
            Assert.Equal(_origHeader[0],fileLines[0]);
            Assert.Equal(_origHeader[1], fileLines[1]);
            Assert.NotEqual(_origHeader[2], fileLines[2]);
            Assert.True(fileLines[2].StartsWith("##VariantPhaser=Scylla"));
            Assert.Equal(_origHeader[2], fileLines[4]);
        }

        private PhasedVcfWriter InitializeWriter(bool crushVcf)
        {
            if (File.Exists(_outputFile))
                File.Delete(_outputFile);

            var writerConfig = new VcfWriterConfig();
            writerConfig.AllowMultipleVcfLinesPerLoci = (!crushVcf);
            writerConfig.ShouldOutputStrandBiasAndNoiseLevel = true;
            writerConfig.ShouldOutputNoCallFraction = true;
            writerConfig.MinFrequencyThreshold = 0.01f;
            var writer = new PhasedVcfWriter(_outputFile, writerConfig, new VcfWriterInputContext(), _origHeader, "tester command line");
            return writer;
        }
    }
}
