using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using RealignIndels.Interfaces;
using RealignIndels.Logic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace RealignIndels.Tests.Utilities
{
    // mock factory will use default behavior unless otherwise specified
    public class MockFactoryWithDefaults : Factory
    {
        public Mock<IChrRealigner> MockChrRealigner { get; set; }
        public Mock<IAlignmentExtractor> MockAlignmentExtractor { get; set; }
        public Mock<IIndelCandidateFinder> MockFinder { get; set; }
        public Mock<IIndelRanker> MockRanker { get; set; }
        public Mock<ITargetCaller> MockCaller { get; set; }
        public Mock<IRealignmentWriter> MockWriter { get; set; }

        public MockFactoryWithDefaults(HygeaOptions options) : base(options) { }

        public override IChrRealigner CreateRealigner(ChrReference chrReference, string bamFilePath, IRealignmentWriter writer)
        {
            return MockChrRealigner != null ? MockChrRealigner.Object : base.CreateRealigner(chrReference, bamFilePath, writer);
        }

        public override IAlignmentExtractor CreateAlignmentExtractor(string bamFilePath, string chromosomeName)
        {
            return MockAlignmentExtractor != null ? MockAlignmentExtractor.Object : base.CreateAlignmentExtractor(bamFilePath, chromosomeName);
        }

        public override IIndelCandidateFinder CreateCandidateFinder()
        {
            return MockFinder != null ? MockFinder.Object : base.CreateCandidateFinder();
        }

        public override IIndelRanker CreateRanker()
        {
            return MockRanker != null ? MockRanker.Object : base.CreateRanker();
        }

        public override ITargetCaller CreateCaller()
        {
            return MockCaller != null ? MockCaller.Object : base.CreateCaller();
        }

        public override IRealignmentWriter CreateWriter(string bamFilePath, string outputFilePath)
        {
            return MockWriter != null ? MockWriter.Object : base.CreateWriter(bamFilePath, outputFilePath);
        }
    }
}
