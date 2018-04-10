using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic.TargetCalling;
using RealignIndels.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class IndelRankerTests
    {
        [Fact]
        public void Rank()
        {
            var ranker = new IndelRanker();

            var known = GetBasic();
            known.IsKnown = true;

            var moreFrequent = GetBasic();
            moreFrequent.Frequency = 0.1f;

            var bigger = GetBasic();
            bigger.ReferenceAllele = "A";
            bigger.AlternateAllele = "ATTTTTT";
            bigger.Type = AlleleCategory.Insertion;

            var basicLeft = GetBasic();
            basicLeft.ReferencePosition--;

            var basic = GetBasic();

            var list = new List<CandidateIndel>();
            ranker.Rank(list);
            Assert.Equal(0, list.Count);

            list = new List<CandidateIndel> { basic };
            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { basic });

            list = new List<CandidateIndel> {basic, basicLeft};
            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { basicLeft, basic });
            
            list = new List<CandidateIndel> {basic, basicLeft, moreFrequent};
            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { moreFrequent, basicLeft, basic });

            list = new List<CandidateIndel> { basicLeft, bigger, basic, moreFrequent };
            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { moreFrequent, bigger, basicLeft, basic });

            list = new List<CandidateIndel> { basic, basicLeft, bigger, moreFrequent, known };
            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { known, moreFrequent, bigger, basicLeft, basic });

            ranker.Rank(list);
            VerifyOrder(list, new List<CandidateIndel> { known, moreFrequent, bigger, basicLeft, basic });
        }

        private void VerifyOrder(List<CandidateIndel> result, List<CandidateIndel> expected)
        {
            for (var i = 0; i < result.Count; i ++)
            {
                Assert.True(result[i] == expected[i]);
            }
        }

        private CandidateIndel GetBasic()
        {
            return new CandidateIndel(new CandidateAllele("chr1", 100, "ATTTTT", "A", AlleleCategory.Deletion));
            
        }
    }
}
