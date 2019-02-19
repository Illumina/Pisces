using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Types;

namespace Gemini.ClassificationAndEvidenceCollection
{
    public class ReadPairClassifierAndExtractor : IReadPairClassifierAndExtractor
    {
        const int NumMismatchesToBeConsideredMessy = 2;
        const int NumMismatchesToBeConsideredUnusableIfSplit = 20;
        private readonly int _minMapQuality;
        private readonly bool _trustSoftclips;
        private readonly bool _deferStitchIndelReads;
        private readonly bool _skipStitch;
        private readonly bool _alwaysTryStitch;

        public ReadPairClassifierAndExtractor(bool trustSoftclips, bool deferStitchIndelReads, int minMapQuality = 10, bool skipStitch = false, bool alwaysTryStitch = false)
        {
            _trustSoftclips = trustSoftclips;
            _deferStitchIndelReads = deferStitchIndelReads;
            _skipStitch = skipStitch;
            _alwaysTryStitch = alwaysTryStitch;
            _minMapQuality = minMapQuality;
        }

        // TODO less yucky outs
        public List<BamAlignment> GetBamAlignmentsAndClassification(ReadPair readPair, IReadPairHandler pairHandler, 
            out PairClassification classification, out bool hasIndels, out int numMismatchesInSingleton, out bool isSplit)
        {
            classification = PairClassification.Unknown;
            List<BamAlignment> bamAlignmentList = null;

            var r1HasIndels = OverlappingIndelHelpers.ReadContainsIndels(readPair.Read1);
            var r2HasIndels = OverlappingIndelHelpers.ReadContainsIndels(readPair.Read2);
            hasIndels = r1HasIndels || r2HasIndels;
            numMismatchesInSingleton = 0;
            int? numMismatchesInR1 = 0;
            int? numMismatchesInR2 = 0;

            if (readPair.IsComplete(false) && readPair.PairStatus == PairStatus.Paired)
            {
                if (readPair.Read1.MapQuality >= _minMapQuality && readPair.Read2.MapQuality >= _minMapQuality)
                {

                    var read1 = readPair.Read1;
                    var read2 = readPair.Read2;

                    var tryStitch = true;

                    if (!_alwaysTryStitch)
                    {
                        if (hasIndels)
                        {
                            tryStitch = false;

                            if (read1.EndPosition >= read2.Position)
                            {
                                bamAlignmentList = OverlappingIndelHelpers.IndelsDisagreeWithStrongMate(
                                    read1, read2, out bool disagree, 3, false);

                                // TODO allow to stitch if they don't disagree, as they may not necessarily get the chance later (either user is not using realigner, or there are no indels strong enough to realign against)
                                // Alternatively, if there are no indels to realign against, still stitch stuff if we can! (handle this in the realigner)
                                // For the cases where we want to skip realignment, either tell it to stitch here (configurable), or have it still go through realigner but not realign?
                                if (disagree)
                                {
                                    classification = PairClassification.Disagree;
                                }
                                else
                                {
                                    // try stitch?
                                    tryStitch = !_deferStitchIndelReads;
                                    classification = PairClassification.UnstitchIndel;
                                }
                            }
                            else
                            {
                                bamAlignmentList = readPair.GetAlignments().ToList();
                                classification = PairClassification.UnstitchIndel;
                            }
                        }
                        else
                        {
                            // TODO if not realigning anything (or not realigning imperfects), go ahead and stitch immediately
                            if (!_trustSoftclips && (ReadContainsImperfections(readPair.Read1, _trustSoftclips) ||
                                                     ReadContainsImperfections(readPair.Read2, _trustSoftclips)))
                            {
                                tryStitch = false;
                                bamAlignmentList = readPair.GetAlignments().ToList();
                                classification = PairClassification.UnstitchImperfect;
                            }
                        }
                    }

                    if (_skipStitch)
                    {
                        tryStitch = false;
                    }

                    if (tryStitch)
                    {
                        bamAlignmentList = pairHandler.ExtractReads(readPair);

                        if (bamAlignmentList.Count == 1)
                        {
                            classification = PairClassification.PerfectStitched;

                            var stitchedResult = bamAlignmentList[0];

                            int? nm = 0;
                            

                            //TODO handle this if it is a hit on performance. Making it simple for now because the previous logic where we were lazy evaluating was a bit skewed
                            var containsImperfections = ReadContainsImperfections(stitchedResult, _trustSoftclips);
                            //nm = stitchedResult.GetIntTag("NM"); // TODO reinstate this if stitched read has proper NM
                            numMismatchesInR1 = readPair.Read1.GetIntTag("NM");
                            numMismatchesInR2 = readPair.Read2.GetIntTag("NM");
                            if (containsImperfections ||
                                (nm > 0 || numMismatchesInR1 > 0 || numMismatchesInR2 > 0))
                            {
                                classification = PairClassification.ImperfectStitched;

                                if (nm >= NumMismatchesToBeConsideredMessy || numMismatchesInR1 >= NumMismatchesToBeConsideredMessy || numMismatchesInR2 >= NumMismatchesToBeConsideredMessy)
                                {
                                    classification = PairClassification.MessyStitched;
                                }
                            }
                        }
                        else
                        {
                            classification = PairClassification.FailStitch;
                        }
                    }
                    else
                    {
                        if (bamAlignmentList == null)
                        {
                            bamAlignmentList = readPair.GetAlignments().ToList();
                            classification = PairClassification.Unstitchable;
                        }
                    }
                }
                else if (readPair.Read1.MapQuality >= _minMapQuality || readPair.Read2.MapQuality >= _minMapQuality)
                {
                    classification = PairClassification.Split;
                    bamAlignmentList = readPair.GetAlignments().ToList();
                }
                else
                {
                    classification = PairClassification.Unusable;
                    bamAlignmentList = readPair.GetAlignments().ToList();
                }

            }
            else
            {
                classification = PairClassification.Unstitchable;

                if (hasIndels)
                {
                    classification = PairClassification.UnstitchIndel;
                }
                bamAlignmentList = readPair.GetAlignments().ToList();
            }


            isSplit = bamAlignmentList?.Count > 0 &&
                      bamAlignmentList?.Select(x => x.RefID).Distinct().Count() > 1;
            if (isSplit || classification == PairClassification.Split || readPair.PairStatus == PairStatus.SplitChromosomes || readPair.PairStatus == PairStatus.MateNotFound || readPair.PairStatus == PairStatus.MateUnmapped)
            {
                if (bamAlignmentList == null || !bamAlignmentList.Any())
                {
                    bamAlignmentList = readPair.GetAlignments().ToList();
                }
                if (!bamAlignmentList.Any())
                {
                    classification = PairClassification.Unusable;
                }
                else
                {
                    classification = PairClassification.Split;
                    if (bamAlignmentList.Count == 1 && bamAlignmentList[0].MapQuality < _minMapQuality)
                    {
                        classification = PairClassification.UnusableSplit;
                    }
                    else
                    {
                        var nms = bamAlignmentList.Select(b => b.GetIntTag("NM") ?? 0);
                        numMismatchesInSingleton = nms.Max();
                        if (numMismatchesInSingleton > NumMismatchesToBeConsideredUnusableIfSplit)
                        {
                            // TODO perhaps make these unmapped? Or just adjust the mapq? Or eventually softclip? It's just inconvenient to do the softclipping here because we don't have the reference sequence. Perhaps we could just softclip the last N bases of the read though?
                            // Making them unusable and skipping them ended up hurting recall, as one might imagine. Not by a ton, and you do see precision go up by a similar amount, but need to think about this.
                            // TODO look into what TPs were lost between commit a8b and ce3.
                            classification = PairClassification.UnusableSplit;
                        }
                        else if (hasIndels || numMismatchesInSingleton > NumMismatchesToBeConsideredMessy)
                        {
                            classification = PairClassification.MessySplit;
                        }
                    }
                }
            }

            if (readPair.PairStatus == PairStatus.Duplicate)
            {
                // TODO make this contingent on skip and remove duplicates flag!!!
                classification = PairClassification.Unusable;
            }

            return bamAlignmentList;
        }

        private bool ReadContainsImperfections(BamAlignment alignment, bool trustSoftClips)
        {
            if (alignment == null)
            {
                return false;
            }
            foreach (CigarOp op in alignment.CigarData)
            {
                if (op.Type == 'I' || op.Type == 'D' || (!trustSoftClips && op.Type == 'S'))
                {
                    return true;
                }
            }

            return false;

        }
    }
}