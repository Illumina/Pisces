using System;
using System.Collections.Generic;
using System.IO;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;


namespace Pisces.IO.Tests.UnitTests
{
    class GvcfWritingTest
    {
        // multiple bams, one default genome
        private const string CHR19 = "chr19";
        private const string CHR17 = "chr17";
        private const int CHR19_LENGTH = 3119100;
        private const int CHR17_LENGTH = 7573100;

        private readonly string _bamChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.bam");
        private readonly string _bamChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr17Chr19.bam");

        private readonly string _bamChr17Chr19Dup = Path.Combine(TestPaths.LocalTestDataDirectory,
            "Chr17Chr19_removedSQlines.bam");

        private readonly string _intervalsChr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "Chr19.picard");

        private readonly string _intervalsChr17 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17only.picard");
        private readonly string _intervalsChr17Chr19 = Path.Combine(TestPaths.LocalTestDataDirectory, "chr17chr19.picard");

        private readonly string _intervalsInvalid = Path.Combine(TestPaths.LocalTestDataDirectory,
            "invalidIntervals.picard");

        private readonly string _genomeChr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private readonly string _genomeChr17Chr19 = Path.Combine(TestPaths.SharedGenomesDirectory, "chr17chr19");
        private readonly string _genomeChr1Chr19_fake = Path.Combine(TestPaths.SharedGenomesDirectory, "fakeChr1Chr19");
        private readonly List<CalledAllele> _defaultCandidates = new List<CalledAllele>()
            {
                new CalledAllele(AlleleCategory.Snv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 123,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {}
                },
                new CalledAllele(AlleleCategory.Mnv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 234,
                    ReferenceAllele = "ATCA",
                    AlternateAllele = "TCGC",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {}
                },
                new CalledAllele()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 456,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    Genotype = Genotype.HomozygousRef,
                    VariantQscore = 27,
                    //FractionNoCalls = float.Parse("0.0124"),
                    TotalCoverage = 99,
                    AlleleSupport = 155,
                    StrandBiasResults = new BiasResults() {GATKBiasScore = float.Parse("0.25")}
                },
                new CalledAllele()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 567,
                    ReferenceAllele = "A",
                    AlternateAllele = ".",
                    VariantQscore = 20,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {FilterType.LowDepth, FilterType.LowVariantQscore, FilterType.StrandBias}
                },
                new CalledAllele(AlleleCategory.Snv)
                {
                    Chromosome = "chr1",
                    ReferencePosition = 678,
                    ReferenceAllele = "A",
                    AlternateAllele = "T",
                    VariantQscore = 25,
                    Genotype = Genotype.HeterozygousAltRef,
                    //FractionNoCalls = float.Parse("0.001"),
                    Filters = new List<FilterType>() {FilterType.LowDepth}
                }
            };

        // Verify the format of the gVCF file is the same as a vcf file.
        [Fact]
        [Trait("ReqID", "SDS-25")]
        public void GvcfHeaderFormat()
        {

            var appOptions = new PiscesApplicationOptions
            {
                BAMPaths = new[] { _bamChr19, _bamChr17Chr19, _bamChr17Chr19Dup },
                IntervalPaths = new[] { _intervalsChr17, _intervalsChr19, null },
                GenomePaths = new[] { _genomeChr17Chr19 },
                VcfWritingParameters = new VcfWritingParameters() { OutputGvcfFile = true }
                };
           
            var factory = new Factory(appOptions);

            var context = new VcfWriterInputContext
            {
                QuotedCommandLineString = "myCommandLine",
                SampleName = "mySample",
                ReferenceName = "myReference",
                ContigsByChr = new List<Tuple<string, long>>
                {
                    new Tuple<string, long>("chr1", 10001),
                    new Tuple<string, long>("chrX", 500)
                }
            };
            var outputFile = factory.GetOutputFile(appOptions.BAMPaths[0]);
            var writer = factory.CreateVcfWriter(outputFile, context);

            var candidates = _defaultCandidates;

            writer.WriteHeader();
            writer.Write(candidates);
            writer.Dispose();

            // Time to read the header
            VcfFileWriterTests.VcfFileFormatValidation(outputFile, 5);
        }
    }
}
