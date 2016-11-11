using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;

namespace Pisces.Domain.Tests.UnitTests.Logic
{
    public class CandidateFinderTestHelpers
    {
        public static byte[] QualitiesArray(string cigarString, int primaryQuality, int? substituteQuality = null, int[] substituteSites = null)
        {
            byte[] baseQualities = Enumerable.Repeat((byte)primaryQuality, (int)(new CigarAlignment(cigarString).GetReadSpan())).ToArray();
            if (substituteQuality != null && substituteSites != null)
            {
                foreach (var siteIndex in substituteSites)
                {
                    baseQualities[siteIndex] = (byte)substituteQuality;
                }
            }
            return baseQualities;
        }

        public static Read CreateRead(string cigarString, string bases, byte[] readQualities, int readStartPos)
        {
            var read = DomainTestHelper.CreateRead("chr1", bases, readStartPos, new CigarAlignment(cigarString), readQualities);

            return read;
        }

    }
}