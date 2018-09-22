using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Calculators;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.TargetCalling;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class IndelTargetCallerTests
    {
        [Fact]
        public void Call()
        {
            var caller = new IndelTargetCaller(0.05f);

            var mockSource = new Mock<IAlleleSource>();
            mockSource.Setup(s => s.GetAlleleCount(1000, AlleleType.A, DirectionType.Forward, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(100);
            mockSource.Setup(s => s.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(100);

            // known indel, never encountered - pass
            var knownNeverEncountered = GetBasicIndel();
            knownNeverEncountered.IsKnown = true;

            // known indel, encountered at too low freq - pass
            var knownLowFreq = GetBasicIndel();
            knownLowFreq.IsKnown = true;
            knownLowFreq.SupportByDirection[0] = 4;

            // denovo indel, encountered at enough freq - pass
            var denovoHighFreq = GetBasicIndel();
            denovoHighFreq.SupportByDirection[0] = 5;

            // denovo indel, encountered at too low freq - filtered
            var denovoLowFreq = GetBasicIndel();
            denovoLowFreq.SupportByDirection[0] = 4;

            var passed = caller.Call(new List<CandidateIndel>
            {
                knownNeverEncountered,
                knownLowFreq,
                denovoHighFreq,
                denovoLowFreq
            }, mockSource.Object);

            Assert.Equal(3, passed.Count);
            Assert.True(passed.Contains(knownNeverEncountered));
            Assert.True(passed.Contains(knownLowFreq));
            Assert.True(passed.Contains(denovoHighFreq));

        }

        private CandidateIndel GetBasicIndel()
        {
            return new CandidateIndel(new CandidateAllele("chr1", 1000, "A", "ATCG", AlleleCategory.Insertion));
        }
    }
}
