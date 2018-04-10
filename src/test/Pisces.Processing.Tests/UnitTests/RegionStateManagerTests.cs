using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Pisces.IO.Sequencing;
using TestUtilities;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Processing.Interfaces;
using Pisces.Processing.RegionState;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{

    public class BlockStateManagerTests
    {
        private ChrReference _chrReference;

        public BlockStateManagerTests()
        {
            _chrReference = new ChrReference() { Name = "chr1", Sequence = string.Concat(Enumerable.Repeat("ACGT", 1000)) };
        }

        [Fact]
        public void AddAndGetCandidates()
        {
            ExecuteAddAndGetCandidates(true);
            ExecuteAddAndGetCandidates(false);
        }

        private void ExecuteAddAndGetCandidates(bool trackOpenEnded)
        {
            var stateManager = new RegionStateManager(false, trackOpenEnded: trackOpenEnded);
            var candidates = new List<CandidateAllele>();

            // block 1-1000
            candidates.Add(new CandidateAllele("chr1", 1, "A", "T", AlleleCategory.Snv));
            candidates.Add(new CandidateAllele("chr1", 998, "AT", "A", AlleleCategory.Deletion));
            candidates.Add(new CandidateAllele("chr1", 1000, "A", "T", AlleleCategory.Snv));
            // block 1001-2000
            candidates.Add(new CandidateAllele("chr1", 1001, "A", "T", AlleleCategory.Snv));
            candidates.Add(new CandidateAllele("chr1", 1001, "A", "G", AlleleCategory.Snv));
            // block 2001-3000
            candidates.Add(new CandidateAllele("chr1", 3000, "ATCC", "GAGG", AlleleCategory.Mnv)); // spans blocks
            // block 3001-4000
            candidates.Add(new CandidateAllele("chr1", 3001, "AT", "A", AlleleCategory.Deletion)); // not collapsable
            candidates.Add(new CandidateAllele("chr1", 3002, "T", "A", AlleleCategory.Snv));  // collapsable competing target
            candidates.Add(new CandidateAllele("chr1", 3003, "G", "C", AlleleCategory.Snv) { OpenOnRight = true }); // not collapsable
            candidates.Add(new CandidateAllele("chr1", 3002, "TA", "GA", AlleleCategory.Mnv)); // collapsable competing target
            candidates.Add(new CandidateAllele("chr1", 3001, "TACGG", "GAGAA", AlleleCategory.Mnv)); // not competing target (past max)
            //block 5001-6000
            candidates.Add(new CandidateAllele("chr1", 5005, "AC", "TT", AlleleCategory.Mnv));

            stateManager.AddCandidates(candidates);

            // make sure we only get candidates from cleared blocks (plus any collapsable ones)
            // make sure we only get a batch at all after upToPosition has transitioned blocks

            // first time we always get a batch
            var batch = stateManager.GetCandidatesToProcess(1);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);

            // no batch expected - haven't transitioned to next block
            batch = stateManager.GetCandidatesToProcess(1000);
            Assert.Equal(null, batch);

            // onto next block - should get all candidates in block 1
            batch = stateManager.GetCandidatesToProcess(1002);
            VerifyBatchContents(batch, candidates.Where(c => c.ReferencePosition <= 1000).ToList());
            Assert.Equal(1, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(1000, batch.ClearedRegions[0].EndPosition);
            stateManager.DoneProcessing(batch);

            // make sure "done" batch is cleared from state
            // expect second block only
            batch = stateManager.GetCandidatesToProcess(2001);  // next block
            VerifyBatchContents(batch, candidates.Where(c => c.ReferencePosition == 1001).ToList());
            Assert.Equal(1001, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(2000, batch.ClearedRegions[0].EndPosition);
            stateManager.DoneProcessing(batch);

            // make sure we fetch collapsable from future blocks
            batch = stateManager.GetCandidatesToProcess(3003);
            VerifyBatchContents(batch, candidates.Where(c => c.ReferencePosition > 1001 &&
                (c.ReferencePosition <= 3000 || // properly from batch
                (trackOpenEnded &&
                ((c.Type == AlleleCategory.Mnv || c.Type == AlleleCategory.Snv) // OR collapsable from future batch (if collapsing is on)
                    && c.ReferencePosition + c.AlternateAllele.Length - 1 <= 3003
                    && !c.OpenOnRight))
                    )).ToList());

            Assert.Equal(2001, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(3000, batch.ClearedRegions[0].EndPosition);

            // repeat - blocks weren't cleared from previous batch
            // expect all candidates in cleared batches minus the collapsable ones we extracted (those were removed from batch)
            batch = stateManager.GetCandidatesToProcess(4001);
            VerifyBatchContents(batch, candidates.Where(c => c.ReferencePosition > 1001 && c.ReferencePosition < 4000 && c.ReferencePosition != 3002 || (!trackOpenEnded &&
                ((c.Type == AlleleCategory.Mnv || c.Type == AlleleCategory.Snv)
                    && c.ReferencePosition + c.AlternateAllele.Length - 1 <= 3003
                    && !c.OpenOnRight && c.ReferencePosition == 3002))
                     ).ToList());

            Assert.Equal(2001, batch.ClearedRegions[0].StartPosition);
            Assert.Equal(3000, batch.ClearedRegions[0].EndPosition);
            Assert.Equal(3001, batch.ClearedRegions[1].StartPosition);
            Assert.Equal(4000, batch.ClearedRegions[1].EndPosition);

            // make sure can fetch all - upToPosition is null
            batch = stateManager.GetCandidatesToProcess(null);
            VerifyBatchContents(batch, candidates.Where(c => c.ReferencePosition > 1001 && (!trackOpenEnded || c.ReferencePosition != 3002)).ToList());

            Assert.Equal(3, batch.ClearedRegions.Count);
            stateManager.DoneProcessing(batch);

            // make sure empty state is ok
            batch = stateManager.GetCandidatesToProcess(null);
            Assert.Equal(0, batch.GetCandidates().Count);
            Assert.Equal(null, batch.ClearedRegions);
        }

        private void VerifyBatchContents(ICandidateBatch batch, List<CandidateAllele> expectedCandidates)
        {
            var candidates = batch.GetCandidates();
            Assert.Equal(expectedCandidates.Count(), candidates.Count);
            foreach (var expected in expectedCandidates)
            {
                Assert.True(candidates.Contains(expected));
            }
        }

        [Fact]
        public void AddAndGetGappedMnvRefCount()
        {
            var stateManager = new RegionStateManager(false);

            // -----------------------------------------------
            // happy path - add a couple of counts in different blocks, 
            // getting them should return the same
            // -----------------------------------------------

            Dictionary<int, int> refCounts = new Dictionary<int, int>();
            refCounts.Add(25, 10);
            refCounts.Add(250, 15);
            refCounts.Add(25000, 20);

            stateManager.AddGappedMnvRefCount(refCounts);
            Assert.Equal(10, stateManager.GetGappedMnvRefCount(25));
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


        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetCollapsedCountHappyPath(ReadCollapsedType type)
        {
            int minQuality = 25;

            var stateManager = new CollapsedRegionStateManager(false, minQuality);
            var readpair = ReadTestHelper.CreateProperReadPair("test", 6, type, pos:10, matePos:15, minBaseQuality: 30);
            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(1, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(2, stateManager.GetCollapsedReadCount(15, type));  // overlapping
            Assert.Equal(1, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9,type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(21, type));

            // test SimplexStitched which is not a primative type.
            if (type == ReadCollapsedType.SimplexForwardStitched || type == ReadCollapsedType.SimplexReverseStitched)
            {
                Assert.Equal(1, stateManager.GetCollapsedReadCount(10, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(11, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(12, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(13, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(14, ReadCollapsedType.SimplexStitched));
                Assert.Equal(2, stateManager.GetCollapsedReadCount(15, ReadCollapsedType.SimplexStitched));  // overlapping
                Assert.Equal(1, stateManager.GetCollapsedReadCount(16, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(17, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(18, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(19, ReadCollapsedType.SimplexStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(20, ReadCollapsedType.SimplexStitched));
                Assert.Equal(0, stateManager.GetCollapsedReadCount(9, ReadCollapsedType.SimplexStitched));
                Assert.Equal(0, stateManager.GetCollapsedReadCount(21, ReadCollapsedType.SimplexStitched));
            }
            // test SimplexNonStitched which is also not a primative type.
            if (type == ReadCollapsedType.SimplexForwardNonStitched || type == ReadCollapsedType.SimplexReverseNonStitched)
            {
                Assert.Equal(1, stateManager.GetCollapsedReadCount(10, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(11, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(12, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(13, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(14, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(2, stateManager.GetCollapsedReadCount(15, ReadCollapsedType.SimplexNonStitched));  // overlapping
                Assert.Equal(1, stateManager.GetCollapsedReadCount(16, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(17, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(18, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(19, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(1, stateManager.GetCollapsedReadCount(20, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(0, stateManager.GetCollapsedReadCount(9, ReadCollapsedType.SimplexNonStitched));
                Assert.Equal(0, stateManager.GetCollapsedReadCount(21, ReadCollapsedType.SimplexNonStitched));
            }
        }

        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetCollapsedCount_minQuality(ReadCollapsedType type)
        {
            int minQuality = 35;
            var stateManager = new CollapsedRegionStateManager(false, minQuality);
            var readpair = ReadTestHelper.CreateProperReadPair("test", 6, type, pos: 10, matePos: 15, minBaseQuality: 30);
            // all base quality less than min quality, no count 
            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(0, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(15, type)); // overlapping
            Assert.Equal(0, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9, type));
        }

        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetCollapsedCount_NonCollapsedBAM(ReadCollapsedType type)
        {
            int minQuality = 20;

            // non collapsed BAM (no reco @PG line in BAM header)
            var stateManager = new RegionStateManager(false, minQuality,expectStitchedReads: true);
            var readpair = ReadTestHelper.CreateProperReadPair("test", 6, type, pos: 10, matePos: 15);
            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(0, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(15, type)); // overlapping
            Assert.Equal(0, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9, type));
        }

        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetCollapsedCount_AlleleN(ReadCollapsedType type)
        {
            int minQuality = 20;
            var stateManager = new CollapsedRegionStateManager(false, minQuality);
            var readpair = ReadTestHelper.CreateProperReadPair("test", 6, type, pos: 10,
                matePos: 15,minBaseQuality:30, candidateBases:"N"); // Generate read pairs only contain "N"
            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(0, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(15, type)); // overlapping
            Assert.Equal(0, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9, type));
        }

        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetCollapsedCount_Exceptions(ReadCollapsedType type)
        {
            int minQuality = 20;
            // collapsed and stitched BAM
            var stateManager = new CollapsedRegionStateManager(false, minQuality);
            var noncollapsedRead = ReadTestHelper.CreateRead("test", ReadTestHelper.RandomBases(6,"ACGTN"), 10, matePosition: 15);
            Assert.Throws<Exception>(()=>stateManager.AddAlleleCounts(noncollapsedRead));
        }

        [Theory]
        [InlineData(ReadCollapsedType.SimplexForwardStitched)]
        [InlineData(ReadCollapsedType.SimplexForwardNonStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseStitched)]
        [InlineData(ReadCollapsedType.SimplexReverseNonStitched)]
        [InlineData(ReadCollapsedType.DuplexNonStitched)]
        [InlineData(ReadCollapsedType.DuplexStitched)]
        public void AddAndGetAlleleCounts_BlockReset(ReadCollapsedType type)
        {

            int minQuality = 25;
            var stateManager = new CollapsedRegionStateManager(false, minQuality);
            var readpair = ReadTestHelper.CreateProperReadPair("test", 6, type, pos: 10, matePos: 15, minBaseQuality: 30);
            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(1, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(2, stateManager.GetCollapsedReadCount(15, type));  // overlapping
            Assert.Equal(1, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(21, type));

            var batch = stateManager.GetCandidatesToProcess(2001);
            stateManager.DoneProcessing(batch); // trigger block reset

            stateManager.AddAlleleCounts(readpair.Item1);
            stateManager.AddAlleleCounts(readpair.Item2);
            Assert.Equal(1, stateManager.GetCollapsedReadCount(10, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(11, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(12, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(13, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(14, type));
            Assert.Equal(2, stateManager.GetCollapsedReadCount(15, type));  // overlapping
            Assert.Equal(1, stateManager.GetCollapsedReadCount(16, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(17, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(18, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(19, type));
            Assert.Equal(1, stateManager.GetCollapsedReadCount(20, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(9, type));
            Assert.Equal(0, stateManager.GetCollapsedReadCount(21, type));
        }

        [Fact]
        public void AddAndGetAlleleCounts()
        {
            int minQuality = 25;

            var stateManager = new RegionStateManager(false, minQuality);

            var reads = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "ACTGGCATC", 1001),
                ReadTestHelper.CreateRead("chr1", "TCTGCCACT", 1005), minQuality).ToList();
            var read1 = reads[0];
            var read2 = reads[1];

            for (var i = 0; i < read2.SequencedBaseDirectionMap.Length; i++)
                read2.SequencedBaseDirectionMap[i] = DirectionType.Reverse;
            read2.PositionMap[7] = -1;

            var reads2 = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "ACAC", 999), ReadTestHelper.CreateRead("chr1", "ACAC", 999), minQuality).ToList();
            var secondRead1 = reads2[0];
            var secondRead2 = reads2[1];

            for (var i = 0; i < secondRead1.SequencedBaseDirectionMap.Length; i++)
                secondRead1.SequencedBaseDirectionMap[i] = DirectionType.Stitched;

            foreach (var read in reads)
            {
                stateManager.AddAlleleCounts(read);
            }
            foreach (var read in reads2)
            {
                stateManager.AddAlleleCounts(read);
            }

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

            //allow some funky ref bases. just treat as N
            //var badAlignmentSet = TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "ACE", 999), minQuality);
            //Assert.Throws<ArgumentException>(() => stateManager.AddAlleleCounts(badAlignmentSet));

            // ---------------------------------------
            // no calls and low quality bases should map to allele type N
            // ---------------------------------------
            var noCallAlignment = TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "NNAC", 999), minQuality);
            noCallAlignment[0].Qualities[2] = (byte)(minQuality - 1);
            noCallAlignment[0].Qualities[3] = (byte)(minQuality - 1);

            foreach (var read in noCallAlignment)
            {
                stateManager.AddAlleleCounts(read);
            }

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
        public void AddAndGetAlleleCounts_PoorQualDeletions()
        {
            // make sure deletions get padded
            int minQualityThreshold = 25;
            int highQualityForRead = 30;
            int lowQualityForRead = 20;

            var stateManager = new RegionStateManager(false, minQualityThreshold);

            var alignmentSet = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "TTTTTTTTT", 1001, new CigarAlignment("5M4D4M")), highQualityForRead, // forward counts for 1001 - 1013
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("1M2D8M")), lowQualityForRead); // reverse counts for 1005 - 1115

            var read1 = alignmentSet[0];
            var read2 = alignmentSet[1];

            // T T T T T D D D D T  T  T  T  x  x   <fwd, q 30>
            // x x x x A D D A A A  A  A  A  A  A   <rvs, q 15>
            // 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15

            for (var i = 0; i < read2.SequencedBaseDirectionMap.Length; i++)
                read2.SequencedBaseDirectionMap[i] = DirectionType.Reverse;

            foreach (var read in alignmentSet)
            {
                stateManager.AddAlleleCounts(read);
            }


            // check fwd read counts (T's and D's)
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            for (int i = 1001; i <= 1013; i++)
            {
                var alleleType = i >= 1006 && i <= 1009 ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.T, DirectionType.Forward), 0);

            // check rev counts (A's and D's). Should be zero b/c under Q
            Assert.Equal(stateManager.GetAlleleCount(1004, AlleleType.A, DirectionType.Reverse), 0);
            for (int i = 1005; i <= 1015; i++)
            {
                var alleleType = i >= 1006 && i <= 1007 ? AlleleType.Deletion : AlleleType.A;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Reverse), 0);
            }
            Assert.Equal(stateManager.GetAlleleCount(1116, AlleleType.A, DirectionType.Reverse), 0);

            // ---------------------------------------
            // Read beginning with deletion
            // ---------------------------------------
            var alignmentSet_frontEdge = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "NNNNNTTTT", 1001, new CigarAlignment("5S2D4M")), lowQualityForRead,
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("9M")), highQualityForRead);

            stateManager = new RegionStateManager(false, minQualityThreshold);
            var frontRead1 = alignmentSet_frontEdge[0];
            var frontRead2 = alignmentSet_frontEdge[1];
            foreach (var read in alignmentSet_frontEdge)
            {
                stateManager.AddAlleleCounts(read);
            }

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            var lengthRef = ((Read)frontRead1).CigarData.GetReferenceSpan();
            for (int i = 1001; i < 1001 + lengthRef - 1; i++)
            {
                var alleleType = i >= 1001 && i <= 1002 ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 0); //dont add low Q ones
            }
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.T, DirectionType.Forward), 0);

            // ---------------------------------------
            // Terminal deletions
            // ---------------------------------------
            var alignmentSet_tailEdge = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "TTTTNNNNN", 1001, new CigarAlignment("4M2D5S")), highQualityForRead,
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1015, new CigarAlignment("9M2D")), lowQualityForRead);//dont add low Q ones

            var tailRead1 = alignmentSet_tailEdge[0];
            var tailRead2 = alignmentSet_tailEdge[1];

            stateManager = new RegionStateManager(false, minQualityThreshold);

            for (var i = 0; i < tailRead2.SequencedBaseDirectionMap.Length; i++)
                tailRead2.SequencedBaseDirectionMap[i] = DirectionType.Reverse;

            foreach (var read in alignmentSet_tailEdge)
            {
                stateManager.AddAlleleCounts(read);
            }

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            lengthRef = ((Read)tailRead1).CigarData.GetReferenceSpan();
            var lastPos = 1001 + lengthRef - 1;
            for (int i = 1001; i <= lastPos; i++)
            {
                var alleleType = i >= 1005 && i <= lastPos ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount((int)lastPos + 1, AlleleType.Deletion, DirectionType.Forward), 0);

            // check A counts
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.A, DirectionType.Reverse), 0);
            lengthRef = ((Read)tailRead2).CigarData.GetReferenceSpan();
            lastPos = 1015 + lengthRef - 1;
            for (int i = 1015; i <= lastPos; i++)
            {
                var alleleType = i >= 1024 && i <= lastPos ? AlleleType.Deletion : AlleleType.A;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Reverse), 0);
            }
            Assert.Equal(stateManager.GetAlleleCount((int)lastPos + 1, AlleleType.Deletion, DirectionType.Reverse), 0);

        }



        [Fact]
        public void AddAndGetAlleleCounts_Deletions()
        {
            // make sure deletions get padded
            int minQuality = 25;

            var stateManager = new RegionStateManager(false, minQuality);

            var alignmentSet = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "TTTTTTTTT", 1001, new CigarAlignment("5M4D4M")),   // forward counts for 1001 - 1013
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("1M2D8M")), minQuality); // reverse counts for 1005 - 1115

            var read1 = alignmentSet[0];
            var read2 = alignmentSet[1];

            for (var i = 0; i < read2.SequencedBaseDirectionMap.Length; i++)
                read2.SequencedBaseDirectionMap[i] = DirectionType.Reverse;

            foreach (var read in alignmentSet)
            {
                stateManager.AddAlleleCounts(read);
            }

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            for (int i = 1001; i <= 1013; i++)
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
            var alignmentSet_frontEdge = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "NNNNNTTTT", 1001, new CigarAlignment("5S2D4M")),
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1005, new CigarAlignment("9M")), minQuality);

            stateManager = new RegionStateManager(false, minQuality);
            var frontRead1 = alignmentSet_frontEdge[0];
            var frontRead2 = alignmentSet_frontEdge[1];
            foreach (var read in alignmentSet_frontEdge)
            {
                stateManager.AddAlleleCounts(read);
            }

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            var lengthRef = ((Read)frontRead1).CigarData.GetReferenceSpan();
            for (int i = 1001; i < 1001 + lengthRef - 1; i++)
            {
                var alleleType = i >= 1001 && i <= 1002 ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.T, DirectionType.Forward), 0);

            // ---------------------------------------
            // Terminal deletions
            // ---------------------------------------
            var alignmentSet_tailEdge = TestHelper.CreateTestReads(
                ReadTestHelper.CreateRead("chr1", "TTTTNNNNN", 1001, new CigarAlignment("4M2D5S")),
                ReadTestHelper.CreateRead("chr1", "AAAAAAAAA", 1015, new CigarAlignment("9M2D")), minQuality);

            var tailRead1 = alignmentSet_tailEdge[0];
            var tailRead2 = alignmentSet_tailEdge[1];

            stateManager = new RegionStateManager(false, minQuality);

            for (var i = 0; i < tailRead2.SequencedBaseDirectionMap.Length; i++)
                tailRead2.SequencedBaseDirectionMap[i] = DirectionType.Reverse;

            foreach (var read in alignmentSet_tailEdge)
            {
                stateManager.AddAlleleCounts(read);
            }

            // check T counts
            Assert.Equal(stateManager.GetAlleleCount(1000, AlleleType.T, DirectionType.Forward), 0);
            lengthRef = ((Read)tailRead1).CigarData.GetReferenceSpan();
            var lastPos = 1001 + lengthRef - 1;
            for (int i = 1001; i <= lastPos; i++)
            {
                var alleleType = i >= 1005 && i <= lastPos ? AlleleType.Deletion : AlleleType.T;
                Assert.Equal(stateManager.GetAlleleCount(i, alleleType, DirectionType.Forward), 1);
            }
            Assert.Equal(stateManager.GetAlleleCount((int)lastPos + 1, AlleleType.Deletion, DirectionType.Forward), 0);

            // check A counts
            Assert.Equal(stateManager.GetAlleleCount(1014, AlleleType.A, DirectionType.Reverse), 0);
            lengthRef = ((Read)tailRead2).CigarData.GetReferenceSpan();
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
            var readLists = new List<List<Read>>
            {
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 1)), // 1-1000
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 1001)), // 1001-2000
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 2001)), // 2001-3000
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 3001)), // 2001-3000
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 5001)), // 5001-6000
                TestHelper.CreateTestReads(ReadTestHelper.CreateRead("chr1", "A", 7001)) // 7001-8000
            };

            foreach (var readList in readLists)
            {
                foreach (var read in readList)
                {
                    stateManager.AddAlleleCounts(read);
                }
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

            var batch = stateManager.GetCandidatesToProcess(1);
            stateManager.DoneProcessing(batch);
            Assert.Equal(1, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(1500);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(2001);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(2001, AlleleType.A, DirectionType.Forward));

            // blocks 1001-2000 and 2001-3000 should be cleared
            batch = stateManager.GetCandidatesToProcess(5500);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(1, AlleleType.A, DirectionType.Forward));
            Assert.Equal(0, stateManager.GetAlleleCount(2001, AlleleType.A, DirectionType.Forward));
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
            Assert.Equal(0, stateManager.GetAlleleCount(5001, AlleleType.A, DirectionType.Forward));
            Assert.Equal(1, stateManager.GetAlleleCount(7001, AlleleType.A, DirectionType.Forward));

            batch = stateManager.GetCandidatesToProcess(8001);
            stateManager.DoneProcessing(batch);
            Assert.Equal(0, stateManager.GetAlleleCount(7001, AlleleType.A, DirectionType.Forward));
        }
    }
}
