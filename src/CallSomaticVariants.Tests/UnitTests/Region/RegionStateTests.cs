using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests.Region
{
    public class RegionStateTests
    {
        [Fact]
        public void AddAndGetCandidates()
        {
            // -----------------------------------------------
            // simple test case adding candidate to empty region
            // -----------------------------------------------
            var testRegion = new RegionState(1000, 2000);
            var snv1 = new CandidateAllele("chr1", 1500, "A", "T", AlleleCategory.Snv) { SupportByDirection = new[] { 10, 0, 0 } };

            testRegion.AddCandidate(snv1);

            var allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 1);

            var fetchedCandidate = allCandidates[0];
            Assert.True(fetchedCandidate.Equals(snv1));

            // -----------------------------------------------
            // add same type of candidate but at different position
            // -----------------------------------------------
            var snv2 = new CandidateAllele("chr1", 1501, "A", "T", AlleleCategory.Snv) { SupportByDirection = new[] { 5, 0, 0 } };
            testRegion.AddCandidate(snv2);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 2);

            // make sure coverage is preserved
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 10);
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv2));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 5);

            // -----------------------------------------------
            // add a different type of candidate at same position
            // -----------------------------------------------
            var deletion1 = new CandidateAllele("chr1", 1500, "AT", "A", AlleleCategory.Deletion) { SupportByDirection = new[] { 15, 0, 0 } };
            testRegion.AddCandidate(deletion1);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 3);

            // make sure coverage is preserved
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 10);
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv2));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 5);
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(deletion1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 15);

            // -----------------------------------------------
            // add same variant, but more coverage
            // -----------------------------------------------
            var moreSnv1 = new CandidateAllele("chr1", 1500, "A", "T", AlleleCategory.Snv) { SupportByDirection = new[] { 2, 0, 0 } };
            testRegion.AddCandidate(moreSnv1);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 3);

            // make sure coverage is incremented
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 12);

            // make sure coverage is preserved
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv2));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 5);
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(deletion1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 15);

            var moreDeletion1 = new CandidateAllele("chr1", 1500, "AT", "A", AlleleCategory.Deletion) { SupportByDirection = new[] { 0, 18, 0 } };
            testRegion.AddCandidate(moreDeletion1);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 3);

            // make sure coverage is incremented
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(deletion1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 33);

            // make sure coverage is preserved
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv1));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 12);
            fetchedCandidate = allCandidates.FirstOrDefault(c => c.Equals(snv2));
            Assert.True(fetchedCandidate != null && fetchedCandidate.Support == 5);

            // -----------------------------------------------
            // add insertion that goes off block
            // -----------------------------------------------
            var edgeInsertion = new CandidateAllele("chr1", 2000, "A", "TCG", AlleleCategory.Insertion) { SupportByDirection = new[] { 5, 0, 0 } };
            testRegion.AddCandidate(edgeInsertion);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 4);
            Assert.Equal(2001, testRegion.MaxAlleleEndpoint);

            // -----------------------------------------------
            // add mnv that goes off block
            // -----------------------------------------------
            var edgeMnv = new CandidateAllele("chr1", 1999, "ATCC", "TCGG", AlleleCategory.Mnv) { SupportByDirection = new[] { 5, 0, 0 } };
            testRegion.AddCandidate(edgeMnv);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 5);
            Assert.Equal(2002, testRegion.MaxAlleleEndpoint);

            // -----------------------------------------------
            // add deletion that goes off block
            // -----------------------------------------------
            var edgeDeletion = new CandidateAllele("chr1", 1999, "A"+new string('T',25), "A", AlleleCategory.Deletion) { SupportByDirection = new[] { 5, 0, 0 } };
            testRegion.AddCandidate(edgeDeletion);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 6);
            Assert.Equal(2025, testRegion.MaxAlleleEndpoint);

            // -----------------------------------------------
            // add variant that goes off block but not as far as the deletion did - max endpoint should stay the same
            // -----------------------------------------------
            var edgeMnv2 = new CandidateAllele("chr1", 1999, "ATCGA", "TCGCT", AlleleCategory.Mnv) { SupportByDirection = new[] { 5, 0, 0 } };
            testRegion.AddCandidate(edgeMnv2);

            allCandidates = testRegion.GetAllCandidates(false, null);
            Assert.Equal(allCandidates.Count, 7);
            Assert.Equal(2025, testRegion.MaxAlleleEndpoint);

        }

        [Fact]
        public void AddAndGetCandidates_Errors()
        {
            var testRegion = new RegionState(1000, 2000);

            Assert.Throws<ArgumentException>(
                () => testRegion.AddCandidate(new CandidateAllele("chr1", 999, "A", "T", AlleleCategory.Snv)));

            Assert.Throws<ArgumentException>(
               () => testRegion.GetAlleleCount(2001, AlleleType.A, DirectionType.Forward));
        }


        [Fact]
        public void AddAndGetAlleleCounts()
        {
            var testRegion = new RegionState(1000, 2000);

            for (var i = 0; i < 5; i ++)
                testRegion.AddAlleleCount(1001, AlleleType.A, DirectionType.Forward);
            for (var i = 0; i < 2; i++)
                testRegion.AddAlleleCount(1001, AlleleType.C, DirectionType.Forward);
            for (var i = 0; i < 12; i++)
                testRegion.AddAlleleCount(1001, AlleleType.C, DirectionType.Reverse);
            for (var i = 0; i < 15; i++)
                testRegion.AddAlleleCount(2000, AlleleType.A, DirectionType.Stitched);

            Assert.Equal(testRegion.GetAlleleCount(1001, AlleleType.A, DirectionType.Forward), 5);
            Assert.Equal(testRegion.GetAlleleCount(1001, AlleleType.C, DirectionType.Forward), 2);
            Assert.Equal(testRegion.GetAlleleCount(1001, AlleleType.C, DirectionType.Reverse), 12);
            Assert.Equal(testRegion.GetAlleleCount(2000, AlleleType.A, DirectionType.Stitched), 15);
            Assert.Equal(testRegion.GetAlleleCount(1000, AlleleType.A, DirectionType.Stitched), 0);
            Assert.Equal(testRegion.GetAlleleCount(1500, AlleleType.A, DirectionType.Forward), 0);
        }

        [Fact]
        public void GetCandidates_WithReference_NoIntervals()
        {
            ExecuteTest_GetCandidates(true, false);
        }

        [Fact]
        public void GetCandidates_NoReference_NoIntervals()
        {
            ExecuteTest_GetCandidates(false, false);
        }

        [Fact]
        public void GetCandidates_WithReference_WithIntervals()
        {
            ExecuteTest_GetCandidates(true, true);
        }

        [Fact]
        public void GetCandidates_NoReference_WithIntervals()
        {
            ExecuteTest_GetCandidates(false, true);
        }

        [Fact]
        public void AddAndGetGappedMnvReferenceCounts()
        {
            var testRegion = new RegionState(50, 150);

            // -----------------------------------------------
            // happy path - add a couple of counts, 
            // getting them should return the same
            // -----------------------------------------------
            testRegion.AddGappedMnvRefCount(75, 1);
            testRegion.AddGappedMnvRefCount(76, 10);
            Assert.Equal(1,testRegion.GetGappedMnvRefCount(75));
            Assert.Equal(10, testRegion.GetGappedMnvRefCount(76));

            // -----------------------------------------------
            // happy path - add a couple of counts to existing ref, 
            // it should be incremented but others should be unchanged
            // -----------------------------------------------
            testRegion.AddGappedMnvRefCount(76, 3);
            Assert.Equal(1, testRegion.GetGappedMnvRefCount(75));
            Assert.Equal(13, testRegion.GetGappedMnvRefCount(76));
        }

        [Fact]
        public void AddAndGetGappedMnvReferenceCounts_Errors()
        {
            var testRegion = new RegionState(50, 150);

            testRegion.AddGappedMnvRefCount(150, 10);

            var refCountsBefore = GetAllRefCounts(testRegion);

            // -----------------------------------------------
            // adding at an invalid position should do nothing
            // -----------------------------------------------
            testRegion.AddGappedMnvRefCount(151, 1);
            var refCountsAfter = GetAllRefCounts(testRegion);
            Assert.True(refCountsAfter.SequenceEqual(refCountsBefore));

            // -----------------------------------------------
            // getting at an invalid position should throw exception
            // -----------------------------------------------
            Assert.Throws<ArgumentException>(() => testRegion.GetGappedMnvRefCount(151));

        }

        private List<int> GetAllRefCounts(RegionState testRegion)
        {
            var refCounts = new List<int>();

            for (int position = testRegion.StartPosition; position <= testRegion.EndPosition; position++)
            {
                refCounts.Add(testRegion.GetGappedMnvRefCount(position));    
            }

            return refCounts;
        }

        public void ExecuteTest_GetCandidates(bool withReference, bool withIntervals)
        {
            var testRegion = new RegionState(1, 50);
            var chrReference = new ChrReference()
            {
                Name = "chr1",
                Sequence = string.Concat(Enumerable.Repeat("A", 50))
            };
            var snv1 = new CandidateAllele("chr1", 5, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new []{ 10, 5, 0}
            };
            var snv2 = new CandidateAllele("chr1", 15, "A", "T", AlleleCategory.Snv)
            {
                SupportByDirection = new[] { 10, 5, 0 }
            };
            testRegion.AddCandidate(snv1);
            testRegion.AddCandidate(snv2);

            for (var i = 0; i < 5; i++)
            {
                testRegion.AddAlleleCount(5, AlleleType.A, DirectionType.Stitched);  // ref @ variant position
                testRegion.AddAlleleCount(6, AlleleType.A, DirectionType.Stitched); // ref by itself
                testRegion.AddAlleleCount(10, AlleleType.C, DirectionType.Stitched); // nonref by itself (no ref)
                testRegion.AddAlleleCount(15, AlleleType.A, DirectionType.Reverse); // ref (multiple directions) + nonref
                testRegion.AddAlleleCount(15, AlleleType.A, DirectionType.Forward); 
                testRegion.AddAlleleCount(15, AlleleType.T, DirectionType.Reverse);
            }

            ChrIntervalSet intervals = null;
            if (withIntervals)
            {
                intervals = new ChrIntervalSet(new List<CallSomaticVariants.Logic.RegionState.Region>()
                {
                 new CallSomaticVariants.Logic.RegionState.Region(3, 6),
                 new CallSomaticVariants.Logic.RegionState.Region(16, 16)   
                }, "chr1");
            }
            var expectedList = new List<CandidateAllele>();
            expectedList.Add(snv1);
            expectedList.Add(snv2);

            if (withReference)
            {
                expectedList.Add(new CandidateAllele("chr1", 5, "A", "A", AlleleCategory.Reference)
                {
                    SupportByDirection = new[] {0, 0, 5}
                });
                expectedList.Add(new CandidateAllele("chr1", 6, "A", "A", AlleleCategory.Reference)
                {
                    SupportByDirection = new[] {0, 0, 5}
                });
                expectedList.Add(new CandidateAllele("chr1", 10, "A", "A", AlleleCategory.Reference)
                {
                    SupportByDirection = new[] {0, 0, 0}
                });
                expectedList.Add(new CandidateAllele("chr1", 15, "A", "A", AlleleCategory.Reference)
                {
                    SupportByDirection = new[] {5, 5, 0}
                });
            }

            if (withIntervals)
            {
                expectedList = expectedList.Where(c => c.Coordinate == 5 || c.Coordinate == 6 || c.Type!=AlleleCategory.Reference).ToList();
                if (withReference)
                {
                    expectedList.Add(new CandidateAllele("chr1", 3, "A", "A", AlleleCategory.Reference)
                    {
                        SupportByDirection = new[] {0, 0, 0}
                    });
                    expectedList.Add(new CandidateAllele("chr1", 4, "A", "A", AlleleCategory.Reference)
                    {
                        SupportByDirection = new[] {0, 0, 0}
                    });
                    expectedList.Add(new CandidateAllele("chr1", 16, "A", "A", AlleleCategory.Reference)
                    {
                        SupportByDirection = new[] {0, 0, 0}
                    });
                }
            }
            var allCandidates = testRegion.GetAllCandidates(withReference, chrReference, intervals);

            VerifyCandidates(expectedList, allCandidates);
        }

        private void VerifyCandidates(List<CandidateAllele> expected, List<CandidateAllele> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var candidate in expected)
            {
                var matchingCandidate = actual.FirstOrDefault(a => a.Equals(candidate));
                Assert.True(matchingCandidate != null);
                for (var i = 0; i < Constants.NumDirectionTypes; i ++)
                    Assert.Equal(candidate.SupportByDirection[i], matchingCandidate.SupportByDirection[i]);
            }
        }
    }
}
