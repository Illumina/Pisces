using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Tests.Utilities;
using CallSomaticVariants.Types;
using Moq;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests
{
    public class VariantToRegionMapperTests
    {
        private readonly string _sequence = string.Concat(Enumerable.Repeat("ACGTATGGA", 10));
        private const string _chr = "chrNew";
        private ChrReference _chrReference;

        public VariantToRegionMapperTests()
        {
            _chrReference = new ChrReference
            {
                Name = _chr,
                Sequence = _sequence
            };
        }

        private string GetReferenceAllele(int position)
        {
            return _sequence[position - 1].ToString();
        }

        /// <summary>
        /// Expect any interval positions outside of the batch cleared regions get padded 
        /// with reference calls
        /// </summary>
        [Fact]
        [Trait("ReqID", "SDS-38")]
        [Trait("ReqID", "SDS-46")]
        [Trait("ReqID", "SDS-37")]
        public void Map()
        {
            // set up test
            var intervals = new List<CallSomaticVariants.Logic.RegionState.Region>
            {
                new CallSomaticVariants.Logic.RegionState.Region(4, 10),
                new CallSomaticVariants.Logic.RegionState.Region(15, 17),
                new CallSomaticVariants.Logic.RegionState.Region(25, 39),
                new CallSomaticVariants.Logic.RegionState.Region(50, 55),
                new CallSomaticVariants.Logic.RegionState.Region(60, 70),
                new CallSomaticVariants.Logic.RegionState.Region(80, 80)
            };

            var mapper = new RegionPadder(_chrReference, new ChrIntervalSet(intervals, "chr1"));

            // ------------------------------------
            // first batch starts after interval start - make sure beginning positions arent skipped
            // ------------------------------------
            var batch = new CandidateBatch
            {
                ClearedRegions = new List<CallSomaticVariants.Logic.RegionState.Region>
                {
                    new CallSomaticVariants.Logic.RegionState.Region(5, 11)
                }
            };
            var expectedAlleles = new List<CandidateAllele>();
            AddReferenceCandidatesByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(4, 4)
            });
            ExecuteTest(mapper, batch, expectedAlleles);

            // ------------------------------------
            // next batch and starts after the second interval, fully covers third interval and partially the fourth
            // ------------------------------------
            batch = new CandidateBatch
            {
                ClearedRegions = new List<CallSomaticVariants.Logic.RegionState.Region>
                {
                    new CallSomaticVariants.Logic.RegionState.Region(20, 52),
                }
            };
            expectedAlleles.Clear();
            AddReferenceCandidatesByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(15, 17)
            });
            ExecuteTest(mapper, batch, expectedAlleles);

            // ------------------------------------
            // next batch contains multiple cleared regions
            // ------------------------------------
            batch = new CandidateBatch
            {
                ClearedRegions = new List<CallSomaticVariants.Logic.RegionState.Region>
                {
                    new CallSomaticVariants.Logic.RegionState.Region(58, 59),
                    new CallSomaticVariants.Logic.RegionState.Region(62, 68)
                }
            };
            expectedAlleles.Clear();
            AddReferenceCandidatesByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(53, 55),
                new Tuple<int, int>(60, 61)
            });
            ExecuteTest(mapper, batch, expectedAlleles);

            // ------------------------------------
            // empty batch
            // ------------------------------------
            batch = new CandidateBatch
            {
                ClearedRegions = null
            };
            expectedAlleles.Clear();
            ExecuteTest(mapper, batch, expectedAlleles);

            // ------------------------------------
            // all the rest
            // ------------------------------------
            batch = new CandidateBatch
            {
                ClearedRegions = new List<CallSomaticVariants.Logic.RegionState.Region>
                {
                    new CallSomaticVariants.Logic.RegionState.Region(69, 69)
                }
            };
            expectedAlleles.Clear();
            AddReferenceCandidatesByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(70, 70),
                new Tuple<int, int>(80, 80)
            });
            ExecuteTest(mapper, batch, expectedAlleles, true);
        }

        private void AddReferenceCandidatesByRange(List<CandidateAllele> candidates, List<Tuple<int, int>> ranges)
        {
            foreach (var range in ranges)
            {
                for (var position = range.Item1; position <= range.Item2; position++)
                {
                    var referenceAllele = GetReferenceAllele(position);
                    candidates.Add(new CandidateAllele(_chr, position, referenceAllele, referenceAllele,
                        AlleleCategory.Reference));
                }
            }
        }

        private void ExecuteTest(RegionPadder mapper, CandidateBatch batch,
            List<CandidateAllele> expectedAlleles, bool mapAll = false)
        {
            mapper.Pad(batch, mapAll);
            var candidates = batch.GetCandidates();
            Assert.Equal(expectedAlleles.Count, candidates.Count());

            foreach (var candidate in candidates)
                Assert.True(expectedAlleles.Contains(candidate));
        }
    }
}
