using System;
using System.Collections.Generic;
using Pisces.Logic;
using Pisces.Tests.MockBehaviors;
using Moq;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Tests;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Processing.Interfaces;
using Pisces.Processing.Models;
using Xunit;

namespace Pisces.Tests.UnitTests
{
    public class SomaticCallerTests
    {
        private CandidateBatch _batch;
        private List<CandidateAllele> _candidateList;
        private SortedList<int, List<CalledAllele>> _calledList = new SortedList<int, List<CalledAllele>>();

        private readonly ChrReference _chrReference = new ChrReference{ Name = "testChr", Sequence = "TTTTTTTTTTTT" };

        [Fact]
        public void Flow()
        {
            ExecuteAndVerifyFlow(5, false);
            ExecuteAndVerifyFlow(3, true);
        }

        private void ExecuteAndVerifyFlow(int numIterations, bool includeRefAlleles)
        {
            var factory = GetMockedFlowFactory(numIterations);
            factory.MockVariantCaller.Setup(s => s.Call(It.IsAny<CandidateBatch>(), factory.MockStateManager.Object))
                .Returns(_calledList);

            var mockWriter = new Mock<IVcfFileWriter<CalledAllele>>();
            var caller = factory.CreateSomaticVariantCaller(_chrReference, "bamFilePath", mockWriter.Object, new Mock<IStrandBiasFileWriter>().Object);
            caller.Execute();

            // alignment operations 
            factory.MockAlignmentSource.Verify(w => w.GetNextRead(), Times.Exactly(numIterations + 1)); // extra time to determine we're done
            factory.MockAlignmentSource.Verify(w => w.LastClearedPosition, Times.Exactly(numIterations));
            factory.MockStateManager.Verify(s => s.AddAlleleCounts(It.IsAny<Read>()), Times.Exactly(numIterations));
            factory.MockStateManager.Verify(s => s.AddCandidates(_candidateList), Times.Exactly(numIterations));
            factory.MockVariantFinder.Verify(v => v.FindCandidates(It.IsAny<Read>(), _chrReference.Sequence, _chrReference.Name), Times.Exactly(numIterations));

            // calling operations 
            // should happen per iteration, plus once more for remainder
            factory.MockVariantCaller.Verify(s => s.Call(It.IsAny<ICandidateBatch>(), factory.MockStateManager.Object), Times.Exactly(numIterations + 1));

            for (var i = 1; i <= numIterations; i++)
            {
                var clearedPosition = i;
                factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(clearedPosition, _chrReference), Times.Once);
            }
            factory.MockStateManager.Verify(s => s.GetCandidatesToProcess(null, _chrReference), Times.Once);  // remainder
            factory.MockStateManager.Verify(s => s.DoneProcessing(It.IsAny<ICandidateBatch>()), Times.Exactly(numIterations + 1)); // total

            // writing operations
            mockWriter.Verify(w => w.Write(It.IsAny<List<CalledAllele>>(), It.IsAny<IRegionMapper>()), Times.Exactly(numIterations + 1));  // once per calling operation
            mockWriter.Verify(w => w.WriteRemaining(It.IsAny<IRegionMapper>()), Times.Exactly(1));  // final write
        }

        private MockFactoryWithDefaults GetMockedFlowFactory(int numIterations)
        {
            var currentIteration = 0;

            var factory = new MockFactoryWithDefaults(new ApplicationOptions());

            // alignment source
            var mockAlignmentSource = new Mock<IAlignmentSource>();
            mockAlignmentSource.Setup(s => s.GetNextRead()).Returns(() => 
                currentIteration < numIterations ? DomainTestHelper.CreateRead(_chrReference.Name, "AAA", 1 + currentIteration++) : null);
            mockAlignmentSource.Setup(s => s.LastClearedPosition).Returns(() => currentIteration );
            factory.MockAlignmentSource = mockAlignmentSource;

            // state manager
            _candidateList = new List<CandidateAllele>() { new CandidateAllele("chr1", 100, "A", "G", AlleleCategory.Snv)};
            _batch = new CandidateBatch(_candidateList);

            var mockStateManager = new Mock<IStateManager>();
            mockStateManager.Setup(s => s.GetCandidatesToProcess(It.IsAny<int?>(), _chrReference)).Returns(_batch);
            factory.MockStateManager = mockStateManager;

            // variant finder
            var mockVariantFinder = new Mock<ICandidateVariantFinder>();
            mockVariantFinder.Setup(v => v.FindCandidates(It.IsAny<Read>(), _chrReference.Sequence, _chrReference.Name)).Returns(_candidateList);
            factory.MockVariantFinder = mockVariantFinder;

            // variant caller
            var mockVariantCaller = new Mock<IAlleleCaller>();
            mockVariantCaller.Setup(v => v.Call(_batch, mockStateManager.Object)).Returns(_calledList);
            factory.MockVariantCaller = mockVariantCaller;

            // region mapper
            var mockRegionMapper = new Mock<IRegionMapper>();
            factory.MockRegionMapper = mockRegionMapper;

            return factory;
        }
    }
}
