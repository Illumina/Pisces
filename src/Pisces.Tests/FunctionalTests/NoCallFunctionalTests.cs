using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Tests.MockBehaviors;
using Moq;
using SequencingFiles;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Tests;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Xunit;

namespace Pisces.Tests.FunctionalTests
{
    public class NoCallFunctionalTests
    {
        private string _chrName = "chr";
            
        [Fact]
        public void Fraction()
        {
            var options = new ApplicationOptions() { MinimumCoverage = 0 };

            var chrReference = new ChrReference()
            {
                Name = _chrName,
                Sequence = "ACTCTACTAAGGGGGGACTATCCCG"  // 25 chr
            };

            // no no-calls, 1 snp
            var readSets = new List<AlignmentSet>();
            AddAlignmentSet(readSets, 50, 1, "ACTCTA", 20, "ATCCCG");
            AddAlignmentSet(readSets, 25, 1, "ACCCTA", 20, "ATCCCG");

            var alleles = Call(readSets, chrReference, options);

            Assert.Equal(1, alleles.Count());
            var variant = alleles[0];
            Assert.Equal(0, variant.FractionNoCalls);
            Assert.Equal(75, variant.TotalCoverage);

            // add no-calls at snp position
            AddAlignmentSet(readSets, 10, 1, "ACNCTA", 20, "ATCCCG");
            alleles = Call(readSets, chrReference, options);

            Assert.Equal(1, alleles.Count());
            variant = alleles[0];
            Assert.Equal(75, variant.TotalCoverage);
            Assert.Equal(10f / 85f, variant.FractionNoCalls);

            // add no-calls at reference position
            options.OutputgVCFFiles = true;
            AddAlignmentSet(readSets, 40, 1, "ACTCTN", 20, "ATCCCG");
            alleles = Call(readSets, chrReference, options);

            Assert.Equal(12, alleles.Count());
            Assert.Equal(1, alleles.Count(a => a is CalledVariant));
            variant = alleles.First(a => a is CalledVariant);
            Assert.Equal(115, variant.TotalCoverage);
            Assert.Equal(10f / 125f, variant.FractionNoCalls);

            foreach (var reference in alleles.Where(a => a is CalledReference))
            {
                Assert.Equal(reference.Coordinate == 6 ? 40f / 125f : 0f, reference.FractionNoCalls);
            }
        }

        private List<BaseCalledAllele> Call(List<AlignmentSet> readSets, ChrReference chrReference, ApplicationOptions options)
        {
            var calledAlleles = new List<BaseCalledAllele>();
            var caller = GetMockFactory(options, readSets).CreateSomaticVariantCaller(chrReference, "fakeBamFilePath", GetMockWriter(calledAlleles));
            caller.Execute();

            return calledAlleles;
        }

        private MockFactoryWithDefaults GetMockFactory(ApplicationOptions options, List<AlignmentSet> readSets)
        {
            var currentReadIndex = -1;

            var factory = new MockFactoryWithDefaults(options);

            // alignment source
            var mockAlignmentSource = new Mock<IAlignmentSource>();
            mockAlignmentSource.Setup(s => s.GetNextAlignmentSet()).Returns(() =>
            {
                currentReadIndex ++;
                return currentReadIndex < readSets.Count() ? readSets[currentReadIndex] : null;
            });
            mockAlignmentSource.Setup(s => s.LastClearedPosition).Returns(() => 0);
            factory.MockAlignmentSource = mockAlignmentSource;

            return factory;
        }

        private IVcfFileWriter<BaseCalledAllele> GetMockWriter(List<BaseCalledAllele> calledAlleles)
        {
            var mockWriter = new Mock<IVcfFileWriter<BaseCalledAllele>>();
            mockWriter.Setup(w => w.Write(It.IsAny<IEnumerable<BaseCalledAllele>>(), It.IsAny<IRegionMapper>())).
                Callback((IEnumerable<BaseCalledAllele> alleles, IRegionMapper mapper) => calledAlleles.AddRange(alleles));

            return mockWriter.Object;
        }

        private void AddAlignmentSet(List<AlignmentSet> readSets, int copies, int read1Position, string read1Sequence, int read2Position, string read2Sequence)
        {
            for (var i = 0; i < copies; i++)
                readSets.Add(new AlignmentSet(
                    DomainTestHelper.CreateRead(_chrName, read1Sequence, read1Position, new CigarAlignment("6M"), qualityForAll:30),
                    DomainTestHelper.CreateRead(_chrName, read2Sequence, read2Position, new CigarAlignment("6M"), qualityForAll:30), 
                    true));
        }
    }
}
