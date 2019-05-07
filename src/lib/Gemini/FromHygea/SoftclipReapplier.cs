using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.Domain.Sequencing;
using Gemini.Types;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using ReadRealignmentLogic.Utlity;
using Helper = Gemini.Utility.Helper;

namespace Gemini.FromHygea
{
    public class SoftclipReapplier
    {
        private bool _remaskSoftclips;
        private bool _maskNsOnly;
        private bool _keepProbeSoftclips;
        private bool _keepBothSideSoftclips;
        private readonly bool _trackActualMismatches;
        private readonly bool _checkSoftclipsForMismatches;

        public SoftclipReapplier(bool remaskSoftclips, bool maskNsOnly, bool keepProbeSoftclips, bool keepBothSideSoftclips, bool trackActualMismatches, bool checkSoftclipsForMismatches)
        {
            _remaskSoftclips = remaskSoftclips;
            _maskNsOnly = maskNsOnly;
            _keepProbeSoftclips = keepProbeSoftclips;
            _keepBothSideSoftclips = keepBothSideSoftclips;
            _trackActualMismatches = trackActualMismatches;
            _checkSoftclipsForMismatches = checkSoftclipsForMismatches;
        }

        public void ReapplySoftclips(Read read, int nPrefixLength, int nSuffixLength, PositionMap positionMapWithoutTerminalNs,
            RealignmentResult result, GenomeSnippet context, uint prefixSoftclip, uint suffixSoftclip,
            CigarAlignment freshCigarWithoutTerminalNs)
        {
            // Re-append the N-prefix
            var nPrefixPositionMap = Enumerable.Repeat(-1, nPrefixLength);
            var nSuffixPositionMap = Enumerable.Repeat(-1, nSuffixLength);
            // TODO maybe have a function for combining pos maps instead
            var finalPositionMap = new PositionMap(nPrefixPositionMap.Concat(positionMapWithoutTerminalNs.Map).Concat(nSuffixPositionMap).ToArray());


            var finalCigar = new CigarAlignment { new CigarOp('S', (uint)nPrefixLength) };
            foreach (CigarOp op in result.Cigar)
            {
                finalCigar.Add(op);
            }

            finalCigar.Add(new CigarOp('S', (uint)nSuffixLength));
            finalCigar.Compress();
            result.Cigar = finalCigar;




            // In case realignment introduced a bunch of mismatch-Ms where there was previously softclipping, optionally re-mask them.
            if (result != null && _remaskSoftclips)
            {
                var mismatchMap =
                    Helper.GetMismatchMap(read.Sequence, finalPositionMap, context.Sequence, context.StartPosition);

                var softclipAdjustedCigar = Helper.SoftclipCigar(result.Cigar, mismatchMap, prefixSoftclip, suffixSoftclip,
                    maskNsOnly: _maskNsOnly, prefixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', false),
                    suffixNs: Helper.GetCharacterBookendLength(read.Sequence, 'N', true), softclipEvenIfMatch: _keepProbeSoftclips || _keepBothSideSoftclips, softclipRepresentsMess: (!(_keepBothSideSoftclips || _keepProbeSoftclips)));

                // Update position map to account for any softclipping added
                var adjustedPrefixClip = softclipAdjustedCigar.GetPrefixClip();
                for (var i = 0; i < adjustedPrefixClip; i++)
                {
                    finalPositionMap.UpdatePositionAtIndex(i, -2, true);
                }

                var adjustedSuffixClip = softclipAdjustedCigar.GetSuffixClip();
                for (var i = 0; i < adjustedSuffixClip; i++)
                {
                    finalPositionMap.UpdatePositionAtIndex(finalPositionMap.Length - 1 - i, -2, true);
                }

                var editDistance =
                    Helper.GetNumMismatches(read.Sequence, finalPositionMap, context.Sequence, context.StartPosition);
                if (editDistance == null)
                {
                    // This shouldn't happen at this point - we already have a successful result
                    throw new InvalidDataException("Edit distance is null for :" + read.Name + " with position map " +
                                                   string.Join(",", finalPositionMap) + " and CIGAR " + softclipAdjustedCigar);
                }

                // TODO PERF - See how much this really helps analytically. I'm thinking maybe kill this altogether and remove from eval
                var sumOfMismatching = Helper.GetSumOfMismatchQualities(mismatchMap, read.Qualities);

                var readHasPosition = finalPositionMap.HasAnyMappableBases();
                if (!readHasPosition)
                {
                    throw new InvalidDataException(string.Format(
                        "Read does not have any alignable bases. ({2} --> {0} --> {3}, {1})", freshCigarWithoutTerminalNs,
                        string.Join(",", finalPositionMap), read.CigarData, softclipAdjustedCigar));
                }

                result.Position = finalPositionMap.FirstMappableBase(); // TODO this used to be >= 0 but changed to > 0. Confirm correct.
                result.Cigar = softclipAdjustedCigar;
                result.NumMismatches = editDistance.Value;

                var addedAtFinal = new List<int>();
                foreach (var i in result.IndelsAddedAt)
                {
                    addedAtFinal.Add(i + nPrefixLength);
                }
                result.IndelsAddedAt = addedAtFinal;
                var nifiedAtFinal = new List<int>();
                foreach (var i in result.NifiedAt)
                {
                    nifiedAtFinal.Add(i + nPrefixLength);
                }
                result.NifiedAt = nifiedAtFinal;

                var newSummary = Extensions.GetAlignmentSummary(result.Position - 1 - context.StartPosition, result.Cigar,
                    context.Sequence,
                    read.Sequence, _trackActualMismatches, _checkSoftclipsForMismatches);

                result.NumNonNMismatches = newSummary.NumNonNMismatches;
                result.NumNonNSoftclips = newSummary.NumNonNSoftclips;
                result.NumSoftclips = newSummary.NumSoftclips;
                result.NumInsertedBases = newSummary.NumInsertedBases;
                result.NumMismatchesIncludeSoftclip = newSummary.NumMismatchesIncludeSoftclip;
                //result.MismatchesIncludeSoftclip = newSummary.MismatchesIncludeSoftclip;
                result.SumOfMismatchingQualities = sumOfMismatching;
                result.AnchorLength = newSummary.AnchorLength;
            }
        }
    }
}