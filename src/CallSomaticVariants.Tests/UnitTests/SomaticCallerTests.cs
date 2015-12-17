using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.MockBehaviors;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using Moq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests
{
    public class SomaticCallerTests
    {
        private CandidateBatch _batch;
        private List<CandidateAllele> _candidateList;
        private List<BaseCalledAllele> _calledList = new List<BaseCalledAllele>();

        private readonly ChrReference _chrReference = new ChrReference{ Name = "testChr", Sequence = "TTTTTTTTTTTT" };

        [Fact]
        public void Constructor()
        {
            // different chr name
            Assert.Throws<ArgumentException>(() => GetMockedFlowFactory(1).CreateSomaticVariantCaller(new ChrReference { Name = "otherChr", Sequence = _chrReference.Sequence }, 
                "bamFilePath", new Mock<IVcfFileWriter>().Object, new Mock<IStrandBiasFileWriter>().Object));
        }

        [Fact]
        public void Flow()
        {
            ExecuteAndVerifyFlow(5, false);
            ExecuteAndVerifyFlow(3, true);
            ExecuteWithNoCandidates(2);
        }

        private void ExecuteAndVerifyFlow(int numIterations, bool includeRefAlleles)
        {
            var factory = GetMockedFlowFactory(numIterations);
            factory.MockVariantCaller.Setup(s => s.Call(It.IsAny<CandidateBatch>(), factory.MockStateManager.Object))
                .Returns(_calledList);

            var mockWriter = new Mock<IVcfFileWriter>();
            var caller = factory.CreateSomaticVariantCaller(_chrReference, "bamFilePath", mockWriter.Object, new Mock<IStrandBiasFileWriter>().Object);
            caller.Execute();

            // alignment operations 
            factory.MockAlignmentSource.Verify(w => w.GetNextAlignmentSet(), Times.Exactly(numIterations + 1)); // extra time to determine we're done
            factory.MockAlignmentSource.Verify(w => w.LastClearedPosition, Times.Exactly(numIterations));
            factory.MockStateManager.Verify(s => s.AddAlleleCounts(It.IsAny<AlignmentSet>()), Times.Exactly(numIterations));
            factory.MockStateManager.Verify(s => s.AddCandidates(_candidateList), Times.Exactly(numIterations));
            factory.MockVariantFinder.Verify(v => v.FindCandidates(It.IsAny<AlignmentSet>(), _chrReference.Sequence, _chrReference.Name), Times.Exactly(numIterations));

            // calling operations 
            // should happen per iteration, plus once more for remainder
            factory.MockVariantCaller.Verify(s => s.Call(It.IsAny<ICandidateBatch>(), factory.MockStateManager.Object), Times.Exactly(numIterations + 1));
            factory.MockRegionMapper.Verify(s => s.Pad(It.IsAny<ICandidateBatch>(), false), Times.Exactly(numIterations));
            factory.MockRegionMapper.Verify(s => s.Pad(It.IsAny<ICandidateBatch>(), true), Times.Exactly(1));

            for (var i = 1; i <= numIterations; i++)
            {
                var clearedPosition = i;
                factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(clearedPosition, _chrReference), Times.Once);
            }
            factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(null, _chrReference), Times.Once);  // remainder
            factory.MockStateManager.Verify(s => s.DoneProcessing(It.IsAny<ICandidateBatch>()), Times.Exactly(numIterations + 1)); // total

            // writing operations
            mockWriter.Verify(w => w.Write(_calledList), Times.Exactly(numIterations + 1));  // once per calling operation
        }

        private void ExecuteWithNoCandidates(int numIterations)
        {
            var factory = GetMockedFlowFactory(numIterations);

            factory.MockStateManager.Setup(s => s.GetCandidatesToProcess(It.IsAny<int?>(), _chrReference)).Returns(new CandidateBatch());

            var mockWriter = new Mock<IVcfFileWriter>();

            var caller = factory.CreateSomaticVariantCaller(_chrReference, "bamFilePath", mockWriter.Object);
            caller.Execute();

            // calling never happens if no candidates to process 
            // should happen per iteration, plus once more for remainder
            factory.MockVariantCaller.Verify(s => s.Call(It.IsAny<ICandidateBatch>(), factory.MockStateManager.Object), Times.Never);
            factory.MockRegionMapper.Verify(s => s.Pad(It.IsAny<ICandidateBatch>(), false), Times.Exactly(numIterations));
            factory.MockRegionMapper.Verify(s => s.Pad(It.IsAny<ICandidateBatch>(), true), Times.Exactly(1));

            for (var i = 1; i <= numIterations; i++)
            {
                var clearedPosition = i;
                factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(clearedPosition, _chrReference), Times.Once);
            }
            factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(null, _chrReference), Times.Once);  // remainder
            factory.MockStateManager.Verify(s => s.DoneProcessing(It.IsAny<ICandidateBatch>()), Times.Exactly(numIterations + 1)); // remainder

            // writing operations
            mockWriter.Verify(w => w.Write(_calledList), Times.Never);  // once per calling operation
        }

        private MockFactoryWithDefaults GetMockedFlowFactory(int numIterations)
        {
            var currentIteration = 0;

            var factory = new MockFactoryWithDefaults(new ApplicationOptions());

            // alignment source
            var mockAlignmentSource = new Mock<IAlignmentSource>();
            mockAlignmentSource.Setup(s => s.GetNextAlignmentSet()).Returns(() => 
                currentIteration < numIterations ? new AlignmentSet(TestHelper.CreateRead(_chrReference.Name, "AAA", 1 + currentIteration++), null) : null);
            mockAlignmentSource.Setup(s => s.LastClearedPosition).Returns(() => currentIteration );
            mockAlignmentSource.Setup(s => s.ChromosomeFilter).Returns(_chrReference.Name);
            factory.MockAlignmentSource = mockAlignmentSource;

            // state manager
            _candidateList = new List<CandidateAllele>() { new CandidateAllele("chr1", 100, "A", "G", AlleleCategory.Snv)};
            _batch = new CandidateBatch(_candidateList);

            var mockStateManager = new Mock<IStateManager>();
            mockStateManager.Setup(s => s.GetCandidatesToProcess(It.IsAny<int?>(), _chrReference)).Returns(_batch);
            factory.MockStateManager = mockStateManager;

            // variant finder
            var mockVariantFinder = new Mock<ICandidateVariantFinder>();
            mockVariantFinder.Setup(v => v.FindCandidates(It.IsAny<AlignmentSet>(), _chrReference.Sequence, _chrReference.Name)).Returns(_candidateList);
            factory.MockVariantFinder = mockVariantFinder;

            // variant caller
            var mockVariantCaller = new Mock<IAlleleCaller>();
            mockVariantCaller.Setup(v => v.Call(_batch, mockStateManager.Object)).Returns(_calledList);
            factory.MockVariantCaller = mockVariantCaller;

            // region mapper
            var mockRegionMapper = new Mock<IRegionPadder>();
            factory.MockRegionMapper = mockRegionMapper;

            return factory;
        }
    }
}
