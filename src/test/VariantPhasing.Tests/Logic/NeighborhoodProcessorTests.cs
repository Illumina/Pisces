using System.Collections.Generic;
using System.IO;
using System.Threading;
using Moq;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;
using Common.IO.Utility;
using VariantPhasing.Interfaces;
using VariantPhasing.Logic;
using VariantPhasing.Models;
using TestUtilities;
using Xunit;

namespace VariantPhasing.Tests.Logic
{
    public class MockFactoryWithDefaults : Factory
    {
        public Mock<INeighborhoodBuilder> MockNeighborhoodBuilder { get; set; }
        public Mock<IVcfFileWriter<CalledAllele>> MockVcfWriter { get; set; }
        public Mock<IVeadGroupSource> MockVeadSource { get; set; }
        public Mock<IAlleleSource> MockVariantSource { get; set; }

        public MockFactoryWithDefaults(ScyllaApplicationOptions options) : base(options)
        {
        }

        public override IAlleleSource CreateOriginalVariantSource()
        {
            return MockVariantSource != null ? MockVariantSource.Object : base.CreateOriginalVariantSource();
        }

        public override INeighborhoodBuilder CreateNeighborhoodBuilder(int batchSize)
        {
            return MockNeighborhoodBuilder != null ? MockNeighborhoodBuilder.Object : base.CreateNeighborhoodBuilder(batchSize);
        }

        public override IVcfFileWriter<CalledAllele> CreatePhasedVcfWriter()
        {
            return MockVcfWriter != null ? MockVcfWriter.Object : base.CreatePhasedVcfWriter();
        }

        public override IVeadGroupSource CreateVeadGroupSource()
        {
            return MockVeadSource != null ? MockVeadSource.Object : base.CreateVeadGroupSource();
        }
    }

    public class NeighborhoodProcessorTests
    {

        [Fact]
        [Trait("ReqID", "SDS-52")]
        public void Execute()
        {
            ExecuteNeighborhoodThreadingTest(1, 1);
            ExecuteNeighborhoodThreadingTest(2, 2);
            ExecuteNeighborhoodThreadingTest(3, 2);
        }

        private void ExecuteNeighborhoodThreadingTest(int numberOfThreads, int expectedNumberOfThreads)
        {
            var bamFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "MNV-25-var216_S216.bam");
            var vcfFilePath = Path.Combine(TestPaths.LocalTestDataDirectory, "MNV-25-var216_S216.vcf");
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory, "Out");

            var options = new ScyllaApplicationOptions
            {
                BamPath = bamFilePath,
                VcfPath = vcfFilePath,
                OutputDirectory = outFolder
            };

            options.SetIODirectories("Scylla");

            var logFile = Path.Combine(options.LogFolder, options.LogFileNameBase);
            if (File.Exists(logFile))
                File.Delete(logFile);
         

            var factory = new MockFactoryWithDefaults(options);
            factory.MockVcfWriter = new Mock<IVcfFileWriter<CalledAllele>>();
            factory.MockVcfWriter.Setup(s => s.Write(It.IsAny<IEnumerable<CalledAllele>>(), It.IsAny<IRegionMapper>())).Callback(() =>
            {
                Thread.Sleep(500);
            });



            var neighborhoods = GetNeighborhoods(expectedNumberOfThreads);

            factory.MockNeighborhoodBuilder = new Mock<INeighborhoodBuilder>();
            var numRaw = 0;
            factory.MockNeighborhoodBuilder.Setup(s => s.GetBatchOfCallableNeighborhoods(0, out numRaw))
                .Returns(neighborhoods);

            factory.MockVeadSource = MockVeadSource();

            factory.MockVariantSource = new Mock<IAlleleSource>();
            factory.MockVariantSource.Setup(s => s.GetVariants()).Returns(new List<CalledAllele>()
            {
                new CalledAllele()
                {
                    Chromosome = "chr1",
                    ReferencePosition = 123,
                    AlternateAllele = "A",
                    ReferenceAllele = "C",
                    Genotype = Pisces.Domain.Types.Genotype.HeterozygousAltRef,
                    TotalCoverage = 10000
                },
                new CalledAllele()
                {
                    Chromosome= "chr2",
                    ReferencePosition = 123,
                    AlternateAllele = "A",
                    ReferenceAllele = "T",
                    Genotype = Pisces.Domain.Types.Genotype.HeterozygousAltRef,
                    TotalCoverage = 10000
                }
            });

            Logger.OpenLog(options.LogFolder, options.LogFileNameBase, true);
            var processor = new VariantPhaser(factory);

            processor.Execute(numberOfThreads);

            Logger.CloseLog();

            var threadsSpawnedBeforeFirstCompleted = 0;

            using (var reader = new StreamReader(new FileStream(logFile, FileMode.Open)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("Completed processing")) break;

                    if (line.Contains("Processing Neighborhood"))
                        threadsSpawnedBeforeFirstCompleted++;
                }
            }

            Assert.Equal(expectedNumberOfThreads, threadsSpawnedBeforeFirstCompleted);
        }

        private List<CallableNeighborhood> GetNeighborhoods(int expectedNumberOfThreads)
        {
            var neighborhoods = new List<CallableNeighborhood>();

            for (var i = 0; i < expectedNumberOfThreads; i++)
            {
                var neighborhood = new VcfNeighborhood(0, "chr1", new VariantSite(120), new VariantSite(121))
                {
                    VcfVariantSites = new List<VariantSite>
                    {
                        new VariantSite(123)
                        {
                            ReferenceName = "chr1",
                            OriginalAlleleFromVcf = TestHelper.CreateDummyAllele("chr1", 123, "A", "T", 1000, 156)
                //orignally at index 0
            },
                    }
                };

                neighborhoods.Add(new CallableNeighborhood( neighborhood, new VariantCallingParameters()));

            }
            return neighborhoods;
        }

        private Mock<IVeadGroupSource> MockVeadSource()
        {
            var returnVeads = new List<VeadGroup>
            {
                new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r1", new [,]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}})),
                new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r2", new [,]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}})),
                new VeadGroup(PhasedVariantTestUtilities.CreateVeadFromStringArray("r5", new [,]{{"N","N"},{"N","N"},{"C","A"},{"C","A"},{"C","A"},{"C","A"}})),
            };

            var veadSource = new Mock<IVeadGroupSource>();
            veadSource.Setup(s => s.GetVeadGroups(It.IsAny<CallableNeighborhood>())).Returns(returnVeads);
            return veadSource;
        }

    }
}
