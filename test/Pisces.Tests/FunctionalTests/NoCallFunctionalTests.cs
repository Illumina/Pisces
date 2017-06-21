using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Interfaces;
using Pisces.Tests.MockBehaviors;
using Moq;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;
using Pisces.IO.Interfaces;
using TestUtilities;
using Xunit;

namespace Pisces.Tests.FunctionalTests
{
    public class NoCallFunctionalTests
    {
        private string _chrName = "chr";
            
        [Fact]
        public void Fraction()
        {
            var options = new PiscesApplicationOptions() {
                VariantCallingParameters = new Domain.Options.VariantCallingParameters()
                {MinimumCoverage = 0 },
                VcfWritingParameters = new VcfWritingParameters()
                {
                    OutputGvcfFile = false
                }
            };

            var chrReference = new ChrReference()
            {
                Name = _chrName,
                Sequence = "ACTCTACTAAGGGGGGACTATCCCG"  // 25 chr
            };

            // no no-calls, 1 snp
            var readSets = new List<Read>();
            AddReads(readSets, 50, 1, "ACTCTA", 20, "ATCCCG");
            AddReads(readSets, 25, 1, "ACCCTA", 20, "ATCCCG");

            var alleles = Call(readSets, chrReference, options);

            Assert.Equal(1, alleles.Count());
            var variant = alleles[0];
            Assert.Equal(0, variant.FractionNoCalls);
            Assert.Equal(75, variant.TotalCoverage);

            // add no-calls at snp position
            AddReads(readSets, 10, 1, "ACNCTA", 20, "ATCCCG");
            alleles = Call(readSets, chrReference, options);

            Assert.Equal(1, alleles.Count());
            variant = alleles[0];
            Assert.Equal(75, variant.TotalCoverage);
            Assert.Equal(10f / 85f, variant.FractionNoCalls);

            // add no-calls at reference position
            options.VcfWritingParameters.OutputGvcfFile = true;
            AddReads(readSets, 40, 1, "ACTCTN", 20, "ATCCCG");
            alleles = Call(readSets, chrReference, options);

            Assert.Equal(12, alleles.Count());
            Assert.Equal(1, alleles.Count(a => (a.Type != Domain.Types.AlleleCategory.Reference)));
            variant = alleles.First(a => (a.Type != Domain.Types.AlleleCategory.Reference));
            Assert.Equal(115, variant.TotalCoverage);
            Assert.Equal(10f / 125f, variant.FractionNoCalls);

            foreach (var reference in alleles.Where(a => (a.Type == Domain.Types.AlleleCategory.Reference)))
            {
                Assert.Equal(reference.ReferencePosition == 6 ? 40f / 125f : 0f, reference.FractionNoCalls);
            }
        }

        private List<CalledAllele> Call(List<Read> readSets, ChrReference chrReference, PiscesApplicationOptions options)
        {
            var calledAlleles = new List<CalledAllele>();
            var caller = GetMockFactory(options, readSets).CreateSomaticVariantCaller(chrReference, "fakeBamFilePath", GetMockWriter(calledAlleles));
            caller.Execute();

            return calledAlleles;
        }

        private MockFactoryWithDefaults GetMockFactory(PiscesApplicationOptions options, List<Read> readSets)
        {
            var currentReadIndex = -1;

            var factory = new MockFactoryWithDefaults(options);

            // alignment source
            var mockAlignmentSource = new Mock<IAlignmentSource>();
            mockAlignmentSource.Setup(s => s.GetNextRead()).Returns(() =>
            {
                currentReadIndex ++;
                return currentReadIndex < readSets.Count() ? readSets[currentReadIndex] : null;
            });
            mockAlignmentSource.Setup(s => s.LastClearedPosition).Returns(() => 0);
            factory.MockAlignmentSource = mockAlignmentSource;

            return factory;
        }

        private IVcfFileWriter<CalledAllele> GetMockWriter(List<CalledAllele> calledAlleles)
        {
            var mockWriter = new Mock<IVcfFileWriter<CalledAllele>>();
            mockWriter.Setup(w => w.Write(It.IsAny<IEnumerable<CalledAllele>>(), It.IsAny<IRegionMapper>())).
                Callback((IEnumerable<CalledAllele> alleles, IRegionMapper mapper) => calledAlleles.AddRange(alleles));

            return mockWriter.Object;
        }

        private void AddReads(List<Read> readSets, int copies, int read1Position, string read1Sequence,
            int read2Position, string read2Sequence)
        {
            for (var i = 0; i < copies; i++)
                readSets.AddRange(new List<Read>() { 
            ReadTestHelper.CreateRead(_chrName, read1Sequence, read1Position, new CigarAlignment("6M"),
                qualityForAll: 30),
            ReadTestHelper.CreateRead(_chrName, read2Sequence, read2Position, new CigarAlignment("6M"),
                qualityForAll: 30)
                }
                );
        }
    }
}
