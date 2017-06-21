using System;
using System.Collections.Generic;
using Moq;
using Pisces.Logic;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Domain.Options;
using Pisces.Processing.Interfaces;
//using StitchingLogic; temporarily removed

namespace Pisces.Tests.MockBehaviors
{
    // mock factory will use default behavior unless otherwise specified
    public class MockFactoryWithDefaults : Factory
    {
        public Mock<IAlignmentSource> MockAlignmentSource { get; set; }
        public Mock<IAlignmentExtractor> MockAlignmentExtractor { get; set; }
        //public Mock<IAlignmentStitcher> MockAlignmentStitcher { get; set; } temporarily removed
        public Mock<IAlignmentMateFinder> MockAlignmentMateFinder { get; set; }
        public Mock<IStateManager> MockStateManager { get; set; }
        public Mock<ICandidateVariantFinder> MockVariantFinder { get; set; }
        public Mock<IAlleleCaller> MockVariantCaller { get; set; }
        public Mock<IRegionMapper> MockRegionMapper { get; set; }
        public Mock<ISomaticVariantCaller> MockSomaticVariantCaller { get; set; }

        public MockFactoryWithDefaults(PiscesApplicationOptions options) : base(options) { }

        protected override IAlignmentSource CreateAlignmentSource(ChrReference chrReference, string bamFilePath, List<string> chrsToProcess)
        {
            return MockAlignmentSource != null ? MockAlignmentSource.Object : base.CreateAlignmentSource(chrReference, bamFilePath, chrsToProcess);
        }

        protected override ICandidateVariantFinder CreateVariantFinder()
        {
            return MockVariantFinder != null ? MockVariantFinder.Object : base.CreateVariantFinder();
        }

        protected override IAlleleCaller CreateVariantCaller(ChrReference chrReference, ChrIntervalSet intervalSet, HashSet<Tuple<string, int, string, string>> forcedGtAlleles =null )
        {
            return MockVariantCaller != null ? MockVariantCaller.Object : base.CreateVariantCaller(chrReference, intervalSet,forcedGtAlleles);
        }

        protected override IStateManager CreateStateManager(ChrIntervalSet intervalSet, bool expectStitchedReads=false)
        {
            return MockStateManager != null ? MockStateManager.Object : base.CreateStateManager(intervalSet);
        }

        protected override IRegionMapper CreateRegionPadder(ChrReference chrReference, ChrIntervalSet intervalSet, bool includeReferences)
        {
            return MockRegionMapper != null ? MockRegionMapper.Object : base.CreateRegionPadder(chrReference, intervalSet, includeReferences);
        }

        public override ISomaticVariantCaller CreateSomaticVariantCaller(ChrReference chrReference, string bamFilePath, 
            IVcfWriter<CalledAllele> vcfWriter, IStrandBiasFileWriter biasFileWriter = null, string intervalFilePath = null, List<string> chrToProcess=null)
        {
            return MockSomaticVariantCaller != null ? MockSomaticVariantCaller.Object : base.CreateSomaticVariantCaller(chrReference, bamFilePath, vcfWriter, biasFileWriter, intervalFilePath, chrToProcess);
        }
    }
}
