using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Models;
using CallSomaticVariants.Tests.Utilities;
using SequencingFiles;

namespace CallSomaticVariants.Tests.UnitTests.VariantCalling
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
            var read = TestHelper.CreateRead("chr1", bases, readStartPos, new CigarAlignment(cigarString), readQualities);

            var alignmentSet = new AlignmentSet(read, null)
            {
                ReadsForProcessing = new List<Read> { read }
            };

            return alignmentSet;
        }

    }
}