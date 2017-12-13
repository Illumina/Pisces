using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class RegionMapperTests
    {
        private readonly string _sequence = string.Concat(Enumerable.Repeat("ACGTATGGA", 10));
        private const string _chr = "chrNew";
        private ChrReference _chrReference;

        public RegionMapperTests()
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
            var intervals = new List<Region>
            {
                new Region(4, 10),
                new Region(15, 17),
                new Region(25, 39),
                new Region(50, 55),
                new Region(60, 70),
                new Region(80, 80)
            };

            var mapper = new RegionMapper(_chrReference, new ChrIntervalSet(intervals, "chr1"), 20);

            // ------------------------------------
            // coverage starts after interval start - make sure beginning positions arent skipped
            // ------------------------------------
            var expectedAlleles = new List<CalledAllele>();
            AddReferenceNoCallsByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(6, 10)
            });
            ExecuteTest(mapper, 6, 11, expectedAlleles);

            // ------------------------------------
            // skip second interval, fully covers third interval and partially the fourth
            // ------------------------------------            
            expectedAlleles.Clear();
            AddReferenceNoCallsByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(25, 39),
                new Tuple<int, int>(50, 52),
            });
            ExecuteTest(mapper, 20, 52, expectedAlleles);

            // ------------------------------------
            // empty region - no interval overlap
            // ------------------------------------
            expectedAlleles.Clear();
            ExecuteTest(mapper, 56, 59, expectedAlleles);

            // ------------------------------------
            // all the rest 
            // ------------------------------------
            expectedAlleles.Clear();
            AddReferenceNoCallsByRange(expectedAlleles, new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(80, 80)
            });
            ExecuteTest(mapper, 71, null, expectedAlleles, true);
        }

        private void AddReferenceNoCallsByRange(List<CalledAllele> references, List<Tuple<int, int>> ranges)
        {
            foreach (var range in ranges)
            {
                for (var position = range.Item1; position <= range.Item2; position++)
                {
                    var referenceAllele = GetReferenceAllele(position);
                    var calledRef = new CalledAllele()
                    {
                        Chromosome = _chr,
                        ReferencePosition = position,
                        ReferenceAllele = referenceAllele,
                        AlternateAllele = referenceAllele,
                        Genotype = Genotype.RefLikeNoCall
                    };

                    calledRef.Filters.Add(FilterType.LowDepth);
                    references.Add(calledRef);
                }
            }
        }

        private void ExecuteTest(RegionMapper mapper, int startPosition, int? endPosition,
            List<CalledAllele> expectedAlleles, bool mapAll = false)
        {
            var noCalls = new List<CalledAllele>();

            CalledAllele noCall;

            while((noCall = mapper.GetNextEmptyCall(startPosition, endPosition)) != null)
            {
                noCalls.Add(noCall);
            }

            Assert.Equal(expectedAlleles.Count, noCalls.Count());

            foreach (var noCallResult in noCalls)
            {
                Assert.True(expectedAlleles.Any(e => e.ReferencePosition == noCallResult.ReferencePosition &&
                    e.ReferenceAllele == noCallResult.ReferenceAllele &&
                    e.AlternateAllele == noCallResult.AlternateAllele &&
                    e.Chromosome == noCallResult.Chromosome &&
                    e.Genotype == noCallResult.Genotype));

                Assert.True(noCallResult.Filters.Contains(FilterType.LowDepth));
                Assert.Equal(1, noCallResult.Filters.Count);
            }
        }
    }
}
