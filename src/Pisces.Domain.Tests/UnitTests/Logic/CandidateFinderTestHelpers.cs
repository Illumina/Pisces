using System.Collections.Generic;
using System.Linq;
using SequencingFiles;
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

        public static AlignmentSet CreateAlignmentSet(string cigarString, string bases, byte[] readQualities, int readStartPos)
        {
            var read = DomainTestHelper.CreateRead("chr1", bases, readStartPos, new CigarAlignment(cigarString), readQualities);

            var alignmentSet = new AlignmentSet(read, null)
            {
                ReadsForProcessing = new List<Read> { read }
            };

            return alignmentSet;
        }

    }
}