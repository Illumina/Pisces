using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Common.IO.Utility;
using StitchingLogic.Models;

namespace StitchingLogic
{
    public class ReadMerger
    {
        private const int MaxReadLength = 200;
	    private const int MaxBaseQuality = 93;
        private bool _useSoftclippedBases;
        private bool _allowRescuedInsertionBaseDisagreement;
        private ReadStatusCounter _statusCounter;
        private bool _debug;
        private readonly bool _ignoreProbeSoftclips;
        private bool _nifyDisagreements;
        private int _minBasecallQuality;

        // Allocate these once for performance
        private readonly List<char> _stitchedBases = new List<char>(MaxReadLength);
        private readonly List<byte> _stitchedQualities = new List<byte>(MaxReadLength);

        public ReadMerger(int minBasecallQuality, bool allowRescuedInsertionBaseDisagreement, bool useSoftclippedBases, bool nifyDisagreements, ReadStatusCounter statusCounter, bool debug, bool ignoreProbeSoftclips)
        {
            _minBasecallQuality = minBasecallQuality;
            _allowRescuedInsertionBaseDisagreement = allowRescuedInsertionBaseDisagreement;
            _useSoftclippedBases = useSoftclippedBases;
            _nifyDisagreements = nifyDisagreements;
            _statusCounter = statusCounter;
            _debug = debug;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
        }

        public Read GenerateNifiedMergedRead(AlignmentSet set, bool useSoftclippedBases)
        {
            var read1InsertionAdjustedEnd = set.PartnerRead1.ClipAdjustedEndPosition +
                                            set.PartnerRead1.CigarData.GetSuffixInsertionLength();
            var read2InsertionAdjustedEnd = set.PartnerRead2.ClipAdjustedEndPosition +
                                            set.PartnerRead2.CigarData.GetSuffixInsertionLength();

            var read1LongerThanRead2 = read2InsertionAdjustedEnd < read1InsertionAdjustedEnd;

            var furthestRight = read1LongerThanRead2 ? read1InsertionAdjustedEnd : read2InsertionAdjustedEnd;

            var nifiedStitchedLength = furthestRight + 1 - set.PartnerRead1.ClipAdjustedPosition;

            var prefixClip = set.PartnerRead1.CigarData.GetPrefixClip();
            var suffixClip = read1LongerThanRead2
                ? set.PartnerRead1.CigarData.GetSuffixClip()
                : set.PartnerRead2.CigarData.GetSuffixClip();
            if (read2InsertionAdjustedEnd == read1InsertionAdjustedEnd)
                suffixClip = Math.Min(set.PartnerRead1.CigarData.GetSuffixClip(),
                    set.PartnerRead2.CigarData.GetSuffixClip());

            var nifiedStitchedCigar = new CigarAlignment((prefixClip > 0 ? string.Format("{0}S", prefixClip) : "")
                                                         + string.Format("{0}M", nifiedStitchedLength - prefixClip - suffixClip)
                                                         + (suffixClip > 0 ? string.Format("{0}S", suffixClip) : ""));
            var beforeOverlap = (useSoftclippedBases ? set.PartnerRead2.ClipAdjustedPosition : set.PartnerRead2.Position) -
                                set.PartnerRead1.ClipAdjustedPosition;
            var afterOverlap = read1LongerThanRead2
                ? (read1InsertionAdjustedEnd - read2InsertionAdjustedEnd)
                : (read2InsertionAdjustedEnd - read1InsertionAdjustedEnd);
            var r1Forward = set.PartnerRead1.SequencedBaseDirectionMap.First() == DirectionType.Forward;
            var beforeOverlapDirection = r1Forward ? "F" : "R";
            var afterOverlapDirection = read1LongerThanRead2 ? (r1Forward ? "F" : "R") : (r1Forward ? "R" : "F");

            var nifiedStitchedDirections = (beforeOverlap > 0
                ? string.Format("{0}{1}", beforeOverlap, beforeOverlapDirection)
                : "")
                                           + string.Format("{0}S", nifiedStitchedLength - beforeOverlap - afterOverlap)
                                           +
                                           (afterOverlap > 0 ? string.Format("{0}{1}", afterOverlap, afterOverlapDirection) : "");

            var mergedRead = new Read(set.PartnerRead1.Chromosome, new BamAlignment
            {
                Name = set.PartnerRead1.Name,
                Bases = new string('N', nifiedStitchedLength),
                Position = Math.Min(set.PartnerRead1.Position - 1, set.PartnerRead2.Position - 1),
                Qualities = Enumerable.Repeat((byte)0, nifiedStitchedLength).ToArray(),
                CigarData = nifiedStitchedCigar
            })
            {
                StitchedCigar = nifiedStitchedCigar,
                CigarDirections = new CigarDirection(nifiedStitchedDirections)
            };
            return mergedRead;
        }

        public Read GenerateConsensusRead(Read read1, Read read2, StitchingInfo stitchingInfo, bool isOutie)
        {
            var mergedRead = new Read(read1.Chromosome, new BamAlignment
            {
                Name = read1.Name,
                Bases = string.Join("", stitchingInfo.StitchedBases),
                Position = Math.Min(read1.Position - 1, read2.Position - 1),
                Qualities = stitchingInfo.StitchedQualities.ToArray(),
                CigarData = stitchingInfo.StitchedCigar
            })
            {
                StitchedCigar = stitchingInfo.StitchedCigar
            };

            return mergedRead;
        }

    }
}