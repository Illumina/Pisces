using CallSomaticVariants.Logic;
using Moq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;

namespace CallSomaticVariants.Tests.MockBehaviors
{
    // mock factory will use default behavior unless otherwise specified
    public class MockFactoryWithDefaults : Factory
    {
        public Mock<IAlignmentSource> MockAlignmentSource { get; set; }
        public Mock<IAlignmentExtractor> MockAlignmentExtractor { get; set; }
        public Mock<IAlignmentStitcher> MockAlignmentStitcher { get; set; }
        public Mock<IAlignmentMateFinder> MockAlignmentMateFinder { get; set; }
        public Mock<IStateManager> MockStateManager { get; set; }
        public Mock<ICandidateVariantFinder> MockVariantFinder { get; set; }
        public Mock<IAlleleCaller> MockVariantCaller { get; set; }
        public Mock<IRegionPadder> MockRegionMapper { get; set; }
        public Mock<ISomaticVariantCaller> MockSomaticVariantCaller { get; set; }

        public MockFactoryWithDefaults(ApplicationOptions options) : base(options) { }

        protected override IAlignmentSource CreateAlignmentSource(ChrReference chrReference, string bamFilePath)
        {
            return MockAlignmentSource != null ? MockAlignmentSource.Object : base.CreateAlignmentSource(chrReference, bamFilePath);
        }

        protected override ICandidateVariantFinder CreateVariantFinder()
        {
            return MockVariantFinder != null ? MockVariantFinder.Object : base.CreateVariantFinder();
        }

        protected override IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet)
        {
            return MockVariantCaller != null ? MockVariantCaller.Object : base.CreateVariantCaller(chrReference, intervalSet);
        }

        protected override IStateManager CreateStateManager(ChrIntervalSet intervalSet)
        {
            return MockStateManager != null ? MockStateManager.Object : base.CreateStateManager(intervalSet);
        }

        protected override IRegionPadder CreateRegionPadder(ChrReference chrReference, ChrIntervalSet intervalSet, bool includeReferences)
        {
            return MockRegionMapper != null ? MockRegionMapper.Object : base.CreateRegionPadder(chrReference, intervalSet, includeReferences);
        }

        public override ISomaticVariantCaller CreateSomaticVariantCaller(ChrReference chrReference, string bamFilePath, IVcfWriter vcfWriter, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null)
        {
            return MockSomaticVariantCaller != null ? MockSomaticVariantCaller.Object : base.CreateSomaticVariantCaller(chrReference, bamFilePath, vcfWriter, biasFileWriter, intervalFilePath);
        }
    }
}
