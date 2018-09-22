using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.Interfaces;
using Pisces.Processing.Models;
using ReadRealignmentLogic.Models;
using Xunit;

namespace RealignIndels.Tests.UnitTests
{
    public class RealignStateManagerTests
    {
        [Fact]
        public void GetCandidates()
        {
            var stateManager = new RealignStateManager();

            var allCandidates = new List<CandidateIndel>
            {
                new CandidateIndel(new CandidateAllele("chr1", 500, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 1500, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 3000, "A", "AT", AlleleCategory.Insertion))
            };

            stateManager.AddCandidates(allCandidates);

            // enforce 1 block window for cleared region
            var batch = stateManager.GetCandidatesToProcess(1);
            Assert.Equal(null, batch.ClearedRegions);
            Assert.False(batch.HasCandidates);
            batch = stateManager.GetCandidatesToProcess(501);
            Assert.Equal(null, batch);

            // candidates from all previous blocks but nothing cleared
            batch = stateManager.GetCandidatesToProcess(1999);
            Assert.Equal(null, batch.ClearedRegions);
            VerifyBatchCandidates(batch, allCandidates.Where(c => c.ReferencePosition == 500).ToList());

            batch = stateManager.GetCandidatesToProcess(2001);
            Assert.Equal(1, batch.ClearedRegions.Count);
            Assert.Equal(1000, batch.ClearedRegions[0].EndPosition);
            VerifyBatchCandidates(batch, allCandidates.Where(c => c.ReferencePosition == 500 || c.ReferencePosition == 1500).ToList());

            batch = stateManager.GetCandidatesToProcess(4000);
            Assert.Equal(2, batch.ClearedRegions.Count);
            Assert.Equal(2000, batch.ClearedRegions[1].EndPosition);
            VerifyBatchCandidates(batch, allCandidates);

            batch = stateManager.GetCandidatesToProcess(null);
            Assert.Equal(null, batch.ClearedRegions);
            VerifyBatchCandidates(batch, allCandidates);
        }

        private void VerifyBatchCandidates(ICandidateBatch batch, List<CandidateIndel> expected)
        {
            var candidates = batch.GetCandidates();

            Assert.Equal(expected.Count, candidates.Count);
            foreach(var candidate in candidates)
                Assert.True(expected.Contains(candidate));
        }

        [Fact]
        public void GetCandidateGroup()
        {
            var stateManager = new RealignStateManager();

            var indel1 = new CandidateIndel(new CandidateAllele("chr1", 500, "A", "AT", AlleleCategory.Insertion));
            var indel2 = new CandidateIndel(new CandidateAllele("chr1", 510, "A", "AT", AlleleCategory.Insertion));
            var indel3 = new CandidateIndel(new CandidateAllele("chr1", 505, "A", "AT", AlleleCategory.Insertion));

            var indel4 = new CandidateIndel(new CandidateAllele("chr1", 1500, "A", "AT", AlleleCategory.Insertion));
            var indel5 = new CandidateIndel(new CandidateAllele("chr1", 1510, "A", "AT", AlleleCategory.Insertion));
            var indel6 = new CandidateIndel(new CandidateAllele("chr1", 3000, "A", "AT", AlleleCategory.Insertion));
            var indel7 = new CandidateIndel(new CandidateAllele("chr1", 3010, "A", "AT", AlleleCategory.Insertion));

            var allCandidates1 = new List<CandidateIndel> { indel1, indel2, indel3 };
            var allCandidates2 = new List<CandidateIndel> { indel4, indel5 };
            var allCandidates3 = new List<CandidateIndel> { indel6, indel7 };
            var testCandidates1 = new List<CandidateIndel> { indel1, indel2 };
            var testCandidates2 = new List<CandidateIndel> { indel3, indel2 };
            var testCandidates3 = new List<CandidateIndel> { indel1, indel3 };

            stateManager.AddCandidates(allCandidates1);
            stateManager.AddCandidates(allCandidates2);
            stateManager.AddCandidates(allCandidates3);

            var candidateIndelGroup = stateManager.GetCandidateGroups(1);
            Assert.True(candidateIndelGroup.Count == 0);

            candidateIndelGroup = stateManager.GetCandidateGroups(2001);
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel1.ToString(), indel3.ToString(), indel2.ToString())));
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel1.ToString(), indel3.ToString(), null)));
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel3.ToString(), indel2.ToString(), null)));
            Assert.False(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel1.ToString(), indel2.ToString(), null)));
            Assert.False(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel2.ToString(), indel3.ToString(), null)));

            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel4.ToString(), indel5.ToString(), null)));
            Assert.False(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel5.ToString(), indel4.ToString(), null)));

            Assert.False(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel6.ToString(), indel7.ToString(), null)));           

            candidateIndelGroup = stateManager.GetCandidateGroups(4000);
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel1.ToString(), indel3.ToString(), indel2.ToString())));
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel4.ToString(), indel5.ToString(), null)));
            Assert.True(candidateIndelGroup.Contains(new Tuple<string, string, string>(indel6.ToString(), indel7.ToString(), null)));

        }

        [Fact]
        public void DoneProcessing()
        {
            var stateManager = new RealignStateManager();

            var allCandidates = new List<CandidateIndel>
            {
                new CandidateIndel(new CandidateAllele("chr1", 500, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 1500, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 3000, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 4000, "A", "AT", AlleleCategory.Insertion)),
                new CandidateIndel(new CandidateAllele("chr1", 4010, "A", "AT", AlleleCategory.Insertion))
            };

            stateManager.AddCandidates(allCandidates);

            var batch = stateManager.GetCandidatesToProcess(1);
            Assert.Equal(null, batch.ClearedRegions);
            Assert.False(batch.HasCandidates);
            stateManager.DoneProcessing(batch);  // effectively does nothing

            // candidates from all previous blocks but nothing cleared
            batch = stateManager.GetCandidatesToProcess(2001);
            Assert.Equal(1, batch.ClearedRegions.Count);
            Assert.Equal(1000, batch.ClearedRegions[0].EndPosition);
            stateManager.DoneProcessing(batch);  // clear first block, but keep it's candidates

            batch = stateManager.GetCandidatesToProcess(3001);
            Assert.Equal(1, batch.ClearedRegions.Count);
            Assert.Equal(2000, batch.ClearedRegions[0].EndPosition);
            VerifyBatchCandidates(batch, allCandidates.Where(c => c.ReferencePosition == 500 || c.ReferencePosition == 1500 || c.ReferencePosition == 3000).ToList());
            stateManager.DoneProcessing(batch); // clear second block, purge block 1 candidates, but keep block 2 candidates

            batch = stateManager.GetCandidatesToProcess(4500);
            Assert.Equal(1, batch.ClearedRegions.Count);
            Assert.Equal(3000, batch.ClearedRegions[0].EndPosition);
            VerifyBatchCandidates(batch, allCandidates.Where(c => c.ReferencePosition == 1500 || c.ReferencePosition == 3000 || c.ReferencePosition == 4000).ToList());
        }
    }
}
