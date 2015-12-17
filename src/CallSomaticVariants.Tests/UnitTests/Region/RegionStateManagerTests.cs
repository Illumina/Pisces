using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Region
{
    public class BlockStateManagerTests
    {
        private ChrReference _chrReference;

        public BlockStateManagerTests()
        {
            _chrReference = new ChrReference() {Name = "chr1", Sequence = string.Concat(Enumerable.Repeat("ACGT", 1000))};
        }

        [Fact]
        public void AddAndGetCandidates()
        {
            var stateManager = new RegionStateManager();
            var candidates = new List<CandidateAllele>();

            // block 1-1000
            candidates.Add(new CandidateAllele("chr1", 1, "A", "T", AlleleCategory.Snv));
            candidates.Add(new CandidateAllele("chr1", 999, "AT", "A", AlleleCategory.Deletion));
            candidates.Add(new CandidateAllele("chr1", 1000, "A", "T", AlleleCategory.Snv));
            // block 1001-2000
            candidates.Add(new CandidateAllele("chr1", 1001, "A", "T", AlleleCategory.Snv));
            candidates.Add(new CandidateAllele("chr1", 1001, "A", "G", AlleleCategory.Snv));
            // block 2001-3000
            candidates.Add(new CandidateAllele("chr1", 3000, "AT", "A", AlleleCategory.Deletion));
            // block 3001-4000
            candidates.Add(new CandidateAllele("chr1", 3001, "AT", "A", AlleleCategory.Deletion));

            stateManager.AddCandidates(candidates);

            // make sure two block window is enforced 
            var batch = stateManager.GetCandidatesToProcess(1);

            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);

            batch = stateManager.GetCandidatesToProcess(1000);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);

            batch = stateManager.GetCandidatesToProcess(1500);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);

            batch = stateManager.GetCandidatesToProcess(2000);
            Assert.Equal(0, batch.GetCandidates().Count);

            batch = stateManager.GetCandidatesToProcess(2001);  // officially on another block
            VerifyBatchContents(batch, candidates.Where(c => c.Coordinate <= 1000));
            Assert.Equal(1, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(1000, batch.ClearedRegions[0].EndPosition);
            stateManager.DoneProcessing(batch);

            // make sure candidate already fetched isn't still in state
            batch = stateManager.GetCandidatesToProcess(2001);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);

            // make sure can fetch across multiple
            batch = stateManager.GetCandidatesToProcess(4000);
            VerifyBatchContents(batch, candidates.Where(c => c.Coordinate >= 1001 && c.Coordinate <= 3000));
            stateManager.DoneProcessing(batch);
            Assert.Equal(1001, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(2000, batch.ClearedRegions[0].EndPosition);
            Assert.Equal(2001, batch.ClearedRegions[1].StartPosition);
            Assert.Equal(3000, batch.ClearedRegions[1].EndPosition);

            stateManager.AddCandidates(new List<CandidateAllele>
            {
                new CandidateAllele("chr1", 2001, "A", "ATG", AlleleCategory.Insertion)
            });

            // make sure can fetch remaining
            batch = stateManager.GetCandidatesToProcess(null);
            Assert.Equal(2, batch.GetCandidates().Count);
            stateManager.DoneProcessing(batch);
            Assert.Equal(2001, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(3000, batch.ClearedRegions[0].EndPosition);
            Assert.Equal(3001, batch.ClearedRegions[1].StartPosition);
            Assert.Equal(4000, batch.ClearedRegions[1].EndPosition);

            // make sure empty state is ok
            batch = stateManager.GetCandidatesToProcess(null);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);
        }

        private void VerifyBatchContents(ICandidateBatch batch, IEnumerable<CandidateAllele> expectedCandidates)
        {
            var candidates = batch.GetCandidates();
            Assert.Equal(candidates.Count, expectedCandidates.Count());
            foreach (var expected in expectedCandidates)
            {
                Assert.True(candidates.Contains(expected));
            }
        }

        [Fact]
        public void AddAndGetGappedMnvRefCount()
        {
            var stateManager = new RegionStateManager();

            // -----------------------------------------------
            // happy path - add a couple of counts in different blocks, 
            // getting them should return the same
            // -----------------------------------------------

            Dictionary<int, int> refCounts = new Dictionary<int, int>();
            refCounts.Add(25, 10);
            refCounts.Add(250, 15);
            refCounts.Add(25000, 20);

            stateManager.AddGappedMnvRefCount(refCounts);
            Assert.Equal(10,stateManager.GetGappedMnvRefCount(25));
            Assert.Equal(15, stateManager.GetGappedMnvRefCount(250));
            Assert.Equal(20, stateManager.GetGappedMnvRefCount(25000));

            // -----------------------------------------------
            // adding to existing values - unmentioned/zeroed ones should remain the same 
            // -----------------------------------------------
            Dictionary<int, int> moreRefCounts = new Dictionary<int, int>();
            moreRefCounts.Add(25, 3);
            moreRefCounts.Add(25000, 0);
            moreRefCounts.Add(500, 10);

            stateManager.AddGappedMnvRefCount(moreRefCounts);
            Assert.Equal(13, stateManager.GetGappedMnvRefCount(25));
            Assert.Equal(15, stateManager.GetGappedMnvRefCount(250));
            Assert.Equal(20, stateManager.GetGappedMnvRefCount(25000));
            Assert.Equal(10, stateManager.GetGappedMnvRefCount(500));

        }


        [Fact]
        public void AddAndGetAlleleCounts()
        {
            int minQuality = 25;

            var stateManager = new RegionStateManager(false, minQuality);

            var alignmentSet = TestHelper.CreateTestSet(
                TestHelper.CreateRead("chr1", "ACTGGCATC", 1001),
                TestHelper.CreateRead("chr1", "TCTGCCACT", 1005), minQuality);

            for (var i = 0; i < alignmentSet.PartnerRead2.DirectionMap.Length; i ++)
                alignmentSet.PartnerRead2.DirectionMap[i] = DirectionType.Reverse;
            alignmentSet.PartnerRead2.PositionMap[7] = -1;

            var alignmentSet2 = TestHelper.CreateTestSet(
                TestHelper.CreateRead("chr1", "ACAC", 999), TestHelper.CreateRead("chr1", "ACAC", 999), minQuality);

            for (var i = 0; i < alignmentSet2.PartnerRead1.DirectionMap.Length; i++)
                alignmentSet2.PartnerRead1.DirectionMap[i] = DirectionType.Stitched;

            stateManager.AddAlleleCounts(alignmentSet);
            stateManager.AddAlleleCounts(alignmentSet2);

            Assert.Equal(stateManager.GetAlleleCount(1004, AlleleType.G, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1005, AlleleType.G, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1005, AlleleType.T, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1006, AlleleType.C, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1006, AlleleType.C, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1007, AlleleType.A, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1007, AlleleType.T, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1008, AlleleType.T, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1008, AlleleType.G, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1009, AlleleType.C, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1009, AlleleType.C, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1010, AlleleType.C, DirectionType.Reverse), 1);
            Assert.Equal(stateManager.GetAlleleCount(1012, AlleleType.C, DirectionType.Reverse), 0); // not mapped to reference

            Assert.Equal(stateManager.GetAlleleCount(999, AlleleType.A, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.C, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1002, AlleleType.C, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward), 2);
            Assert.Equal(stateManager.GetAlleleCount(1002, AlleleType.C, DirectionType.Forward), 2);

            // error conditions
            Assert.Throws<ArgumentException>(() => stateManager.GetAlleleCount(0, AlleleType.A, DirectionType.Forward));

            //tjd + allow some funky ref bases. just treat as N
            //var badAlignmentSet = TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "ACE", 999), minQuality);
            //Assert.Throws<ArgumentException>(() => stateManager.AddAlleleCounts(badAlignmentSet));
            //tjd-

            // ---------------------------------------
            // no calls and low quality bases should map to allele type N
            // ---------------------------------------
            var noCallAlignment = TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "NNAC", 999), minQuality);
            noCallAlignment.PartnerRead1.Qualities[2] = (byte)(minQuality - 1);
            noCallAlignment.PartnerRead1.Qualities[3] = (byte)(minQuality - 1);

            stateManager.AddAlleleCounts(noCallAlignment);

            // make sure no calls logged
            Assert.Equal(stateManager.GetAlleleCount(999, AlleleType.N, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.N, DirectionType.Forward), 1);
            Assert.Equal(stateManager.GetAlleleCount(1001, AlleleType.N, DirectionType.Forward), 1);
            // make sure remaining didnt change
            Assert.Equal(stateManager.GetAlleleCount(999, AlleleType.A, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.C, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1002, AlleleType.C, DirectionType.Stitched), 1);
            Assert.Equal(stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward), 2);
            Assert.Equal(stateManager.GetAlleleCount(1002, AlleleType.C, DirectionType.Forward), 2);
        }

        [Fact]
        public void AddAndGetAlleleCounts_Deletions()
        {
            // make sure deletions get padded
            int minQuality = 25;

            var stateManager = new RegionStateManager(false, minQuality);

            var alignmentSet = TestHelper.CreateTestSet(
                TestHelper.CreateRead("chr1", "TTTTTTTTT", 1001, new CigarAlignment("5M4D4M")),   // forward counts for 1001 - 1013
                TestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("1M2D8M")), minQuality); // reverse counts for 1005 - 1115

            for (var i = 0; i < alignmentSet.PartnerRead2.DirectionMap.Length; i++)
                alignmentSet.PartnerRead2.DirectionMap[i] = DirectionType.Reverse;

            stateManager.AddAlleleCounts(alignmentSet);

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            for (int i = 1001; i <= 1013; i ++)
            {
                var alleleType = i >= 1006 && i <= 1009 ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.T, DirectionType.Forward), 0);

            // check A counts
            Assert.Equal(stateManager.GetAlleleCount(1004, AlleleType.A, DirectionType.Reverse), 0);
            for (int i = 1005; i <= 1015; i++)
            {
                var alleleType = i >= 1006 && i <= 1007 ? AlleleType.Deletion : AlleleType.A;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Reverse), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount(1116, AlleleType.A, DirectionType.Reverse), 0);

            // ---------------------------------------
            // Read beginning with deletion
            // ---------------------------------------
            var alignmentSet_frontEdge = TestHelper.CreateTestSet(
                TestHelper.CreateRead("chr1", "NNNNNTTTT", 1001, new CigarAlignment("5S2D4M")),
                TestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("9M")), minQuality);
       
            stateManager = new RegionStateManager(false, minQuality);

            stateManager.AddAlleleCounts(alignmentSet_frontEdge);

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            var lengthRef = alignmentSet_frontEdge.PartnerRead1.CigarData.GetReferenceSpan();
            for (int i = 1001; i < 1001 + lengthRef -1; i++)
            {
                var alleleType = i >= 1001 && i <= 1002 ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.T, DirectionType.Forward), 0);

            // ---------------------------------------
            // Terminal deletions
            // ---------------------------------------
            var alignmentSet_tailEdge = TestHelper.CreateTestSet(
                TestHelper.CreateRead("chr1", "TTTTNNNNN", 1001, new CigarAlignment("4M2D5S")),   
                TestHelper.CreateRead("chr1", "AAAAAAAAA", 1015, new CigarAlignment("9M2D")), minQuality);

            stateManager = new RegionStateManager(false, minQuality);

            for (var i = 0; i < alignmentSet_tailEdge.PartnerRead2.DirectionMap.Length; i++)
                alignmentSet_tailEdge.PartnerRead2.DirectionMap[i] = DirectionType.Reverse;

            stateManager.AddAlleleCounts(alignmentSet_tailEdge);

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            lengthRef = alignmentSet_tailEdge.PartnerRead1.CigarData.GetReferenceSpan();
            var lastPos = 1001 + lengthRef - 1;
            for (int i = 1001; i <= lastPos; i++)
            {
                var alleleType = i >= 1005 && i <= lastPos ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount((int)lastPos + 1, AlleleType.Deletion, DirectionType.Forward), 0);

            // check A counts
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.A, DirectionType.Reverse), 0);
            lengthRef = alignmentSet_tailEdge.PartnerRead2.CigarData.GetReferenceSpan();
            lastPos = 1015 + lengthRef - 1;
            for (int i = 1015; i <= lastPos; i++)
            {
                var alleleType = i >= 1024 && i <= lastPos ? AlleleType.Deletion : AlleleType.A;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Reverse), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount((int)lastPos + 1, AlleleType.Deletion, DirectionType.Reverse), 0);

        }

        [Fact]
        public void DoneProcessing()
        {
            var stateManager = new RegionStateManager();
            var alignments = new List<AlignmentSet>
            {
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 1)), // 1-1000
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 1001)), // 1001-2000
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 2001)), // 2001-3000
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 3001)), // 2001-3000
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 5001)), // 5001-6000
                TestHelper.CreateTestSet(TestHelper.CreateRead("chr1", "A", 7001)) // 7001-8000
            };

            foreach (var alignment in alignments)
            {
                stateManager.AddAlleleCounts(alignment);
            }

            // blocks should all be in memory
            Assert.Equal(1, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(2001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(3001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(7001, AlleleType.A, DirectionType.Forward));

            stateManager.AddCandidates(new List<CandidateAllele>()
            {
                new CandidateAllele("chr1", 1, "T", "A", AlleleCategory.Snv),
                new CandidateAllele("chr1", 3001, new string('A', 3500), "A", AlleleCategory.Deletion)
            });

            // still in memory
            var batch = stateManager.GetCandidatesToProcess(1);
            stateManager.DoneProcessing(batch);
            Assert.Equal(1, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(1500);
            stateManager.DoneProcessing(batch);
            Assert.Equal(1, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));

            // block 1-1000 is the last accessed block so is still in memory, block 1001-2000 still in memory
            batch = stateManager.GetCandidatesToProcess(2001);
            stateManager.DoneProcessing(batch);
            //Assert.Equal(0, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward)); 
            Assert.Equal(1, stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward));

            // blocks 1001-2000 and 2001-3000 should be cleared
            batch = stateManager.GetCandidatesToProcess(5500);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward)); 
            //Assert.Equal(0, stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward)); // last accessed block so this is stored in memory
            Assert.Equal(0, stateManager.GetAlleleCount(2001, AlleleType.A, DirectionType.Forward));

            Console.WriteLine("Check");
            //3001 block shouldn't be cleared yet because it holds a variant that extends into a further block
            Assert.Equal(1, stateManager.GetAlleleCount(3001, AlleleType.A, DirectionType.Forward));
            //5001 block shouldn't be cleared yet because it comes later than a held-up block
            Assert.Equal(1, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(6500);
            stateManager.DoneProcessing(batch);
            //3001 block shouldn't be cleared yet because it holds a variant that extends into a further block
            Assert.Equal(1, stateManager.GetAlleleCount(3001, AlleleType.A, DirectionType.Forward));
            //5001 block shouldn't be cleared yet because it comes later than a held-up block
            Assert.Equal(1, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(7001);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(3001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward)); //Still here because it's the last accessed block
            Assert.Equal(1, stateManager.GetAlleleCount(7001, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(7002);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(3001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(0, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));

            // rest should be cleared - Not anymore!!!
            //stateManager.DoneProcessing();
            //Assert.Equal(0, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));
            //Assert.Equal(0, stateManager.GetAlleleCount(7001, AlleleType.A, DirectionType.Forward));
        }
    }
}
