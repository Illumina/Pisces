using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;

namespace Gemini.CandidateIndelSelection
{
    public static class OverlappingIndelHelpers
    {
        public static void SoftclipAfterIndel(BamAlignment alignment, bool reverse, int firstCollision)
        {
            // TODO Test this intensely
            var cigarOps = alignment.CigarData;
            var hitIndel = false;
            var hitAnyMatch = false;
            if (reverse)
            {
                var endPosition = alignment.EndPosition;
                var positionAdjustment = 0;
                for (int i = 0; i < cigarOps.Count; i++)
                {
                    var currentIndex = cigarOps.Count - 1 - i;
                    var op = cigarOps[currentIndex];
                    var type = op.Type;
                    var length = op.Length;
                    if ((type == 'D' || type == 'I') &&
                        endPosition - (op.IsReferenceSpan() ? op.Length : 1) <= firstCollision)
                    {
                        hitIndel = true;
                    }
                    else if (type == 'M' && !hitIndel)
                    {
                        hitAnyMatch = true;
                    }

                    if (hitIndel && hitAnyMatch)
                    {
                        if (type == 'S' || type == 'H')
                        {
                            continue;
                        }

                        if (cigarOps[currentIndex].IsReferenceSpan())
                        {
                            positionAdjustment += (int) length;
                        }

                        cigarOps[currentIndex] = new CigarOp('S', type == 'D' ? 0 : length);
                    }

                    if (op.IsReferenceSpan())
                        endPosition -= (int) op.Length;

                }

                alignment.Position = alignment.Position + positionAdjustment;
            }
            else
            {
                int startIndexInReference = alignment.Position;

                for (int i = 0; i < cigarOps.Count; i++)
                {
                    var operation = cigarOps[i];
                    var type = cigarOps[i].Type;
                    var length = cigarOps[i].Length;
                    if ((type == 'D' || type == 'I') && startIndexInReference >= firstCollision)
                    {
                        hitIndel = true;
                    }
                    else if (type == 'M' && !hitIndel)
                    {
                        hitAnyMatch = true;
                    }


                    if (hitIndel && hitAnyMatch)
                    {
                        if (type != 'S' && type != 'H')
                        {
                            cigarOps[i] = new CigarOp('S', type == 'D' ? 0 : length);
                        }
                    }


                    if (operation.IsReferenceSpan())
                        startIndexInReference += (int) operation.Length;

                }
            }

            // TODO could just do this in place
            cigarOps.Compress();

        }

        public static List<BamAlignment> IndelsDisagreeWithStrongMate(BamAlignment read1,
            BamAlignment read2, out bool disagree, int mismatchesAllowed = 1, bool softclipWeakOne = true, int? r1Nm = null, int? r2Nm = null)
        {
            disagree = false;

            var r1Positions =
                GetIndelPositions(read1, out int totalIndelBasesR1);
            var r2Positions =
                GetIndelPositions(read2, out int totalIndelBasesR2);

            var result = IndelsDisagreeWithStrongMate(r1Positions, r2Positions, read1,
                read2, out disagree, mismatchesAllowed, r1IndelAdjustment: totalIndelBasesR1,
                r2IndelAdjustment: totalIndelBasesR2, softclipWeakOne: softclipWeakOne, r1Nm: r1Nm, r2Nm: r2Nm);

            return result;
        }


        private static List<BamAlignment> IndelsDisagreeWithStrongMate(List<IndelSite> r1IndelPositions,
            List<IndelSite> r2IndelPositions, BamAlignment read1,
            BamAlignment read2, out bool disagree, int mismatchesAllowed = 1, int r1IndelAdjustment = 0,
            int r2IndelAdjustment = 0, bool softclipWeakOne = true, int? r1Nm = null, int? r2Nm = null)
        {
            var checkBoth = true;
            // TODO maybe also check if one of the reads has ins AND del
            // TODO if we've grabbed this info here, propagate it out so we don't do it twice
            // TODO indel adjustment should only actually remove insertions, no?? 
            var read1Nm = r1Nm ?? read1.GetIntTag("NM");
            var read2Nm = r2Nm ?? read2.GetIntTag("NM");
            var read1AdjustedNm = read1Nm - r1IndelAdjustment;
            var read2AdjustedNm = read2Nm - r2IndelAdjustment;

            disagree = false;

            var r1IndelPositionsUnique = r1IndelPositions != null && r2IndelPositions != null ? GetUniqueIndelSites(r1IndelPositions, r2IndelPositions) : r1IndelPositions;
            var r2IndelPositionsUnique = r1IndelPositions != null && r2IndelPositions != null ? GetUniqueIndelSites(r2IndelPositions, r1IndelPositions) : r2IndelPositions;

            // No sense doing further checks if there's nothing to disagree over...
            if (r1IndelPositionsUnique.Any() || r2IndelPositionsUnique.Any())
            {
                var r1AdjustedClean = read1AdjustedNm <= mismatchesAllowed;
                var r2AdjustedClean = read2AdjustedNm <= mismatchesAllowed;
                var r1Clean = read1Nm <= mismatchesAllowed;
                var r2Clean = read2Nm <= mismatchesAllowed;
                var r1NumIndels = r1IndelPositions?.Count;
                var r2NumIndels = r2IndelPositions?.Count;
                var r1IsGood = r1AdjustedClean && (r1Clean || r1NumIndels <= 1);
                var r2IsGood = r2AdjustedClean && (r2Clean || r2NumIndels <= 1);

                if ((read1Nm != null && read2Nm != null) && (r1IsGood || r2IsGood))
                {
                    if (r1IsGood)
                    {
                        var disagreeingPos = AnyIndelCoveredInMate(r2IndelPositionsUnique, read1, read2);

                        if (disagreeingPos != null)
                        {
                            disagree = true;
                            if (softclipWeakOne && !r2IsGood)
                            {
                                SoftclipAfterIndel(read2, read2.IsReverseStrand(), disagreeingPos.Value);
                            }
                        }
                        else
                        {
                            if (checkBoth)
                            {
                                disagreeingPos = AnyIndelCoveredInMate(r1IndelPositionsUnique, read2, read1);
                                if (disagreeingPos != null)
                                {
                                    disagree = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        var disagreeingPos = AnyIndelCoveredInMate(r1IndelPositionsUnique, read2, read1);
                        if (disagreeingPos != null)
                        {
                            disagree = true;
                            if (softclipWeakOne && !r1IsGood)
                            {
                                SoftclipAfterIndel(read1, read1.IsReverseStrand(), disagreeingPos.Value);
                            }
                        }
                        else
                        {
                            if (checkBoth)
                            {
                                disagreeingPos = AnyIndelCoveredInMate(r2IndelPositionsUnique, read1, read2);
                                if (disagreeingPos != null)
                                {
                                    disagree = true;
                                }
                            }
                        }
                    }
                }
            }

            // If both are good, and they disagree, should still say they disagree?

            return new List<BamAlignment>() {read1, read2};

        }

        private static List<IndelSite> GetUniqueIndelSites(List<IndelSite> queryReadIndelSites, List<IndelSite> otherReadIndelSites)
        {
            var queryReadIndelSitesUnique = new List<IndelSite>();
            foreach (var item in queryReadIndelSites)
            {
                bool hasMatch = false;
                var matchInR2 = otherReadIndelSites.Where(x =>
                    x.PreviousMappedPosition == item.PreviousMappedPosition &&
                    x.NextMappedPosition == item.NextMappedPosition && x.IndelType == item.IndelType);

                if (!matchInR2.Any())
                {
                    queryReadIndelSitesUnique.Add(item);
                    continue;
                }

                if (item.IndelType == 'I')
                {
                    var match = matchInR2.First();
                    if (match.Length == item.Length || (match.Length < item.Length && match.IsTerminal) ||
                        (item.Length < match.Length && item.IsTerminal))
                    {
                        // Has matching indel in other read
                    }
                    else
                    {
                        // There's something at the same position but it's a different variant.
                        queryReadIndelSitesUnique.Add(item);
                    }
                }
            }

            return queryReadIndelSitesUnique;
        }

        public static int? AnyIndelCoveredInMate(IEnumerable<IndelSite> readIndelPositions,
            BamAlignment readWithoutIndels, BamAlignment readWithIndels, int anchorSize = 0)
        {
            if (readIndelPositions == null || !readIndelPositions.Any())
            {
                return null;
            }

            if (readWithIndels.IsReverseStrand())
            {
                readIndelPositions = readIndelPositions.Reverse();
            }

            foreach (var indelPosition in readIndelPositions)
            {
                var coveredInR1 =
                    readWithoutIndels.ContainsPosition(indelPosition.PreviousMappedPosition - anchorSize, readWithIndels.RefID) &&
                    readWithoutIndels.ContainsPosition(indelPosition.NextMappedPosition + anchorSize, readWithIndels.RefID);
                if (coveredInR1)
                {
                    return indelPosition.PreviousMappedPosition;
                }
            }

            return null;
        }


        public static List<IndelSite> GetIndelPositions(BamAlignment read, out int totalIndelBases)
        {
            totalIndelBases = 0;
            int startIndexInRead = 0;
            int startIndexInReference = read.Position;
            var positions = new List<IndelSite>();

            var numOperations = read.CigarData.Count;
            for (var cigarOpIndex = 0; cigarOpIndex < numOperations; cigarOpIndex++)
            {
                var operation = read.CigarData[cigarOpIndex];
                switch (operation.Type)
                {
                    case 'I':
                        positions.Add(new IndelSite(startIndexInReference - 1, startIndexInReference, operation.Type, (int)operation.Length, cigarOpIndex == 0 || cigarOpIndex == numOperations - 1));
                        totalIndelBases += (int) operation.Length;
                        break;
                    case 'D':
                        positions.Add(new IndelSite(startIndexInReference - 1,
                            startIndexInReference + (int) operation.Length, operation.Type, (int)operation.Length * -1, cigarOpIndex == 0 || cigarOpIndex == numOperations - 1));
                        totalIndelBases += (int) operation.Length;
                        break;
                }

                if (operation.IsReadSpan())
                    startIndexInRead += (int) operation.Length;

                if (operation.IsReferenceSpan())
                    startIndexInReference += (int) operation.Length;
            }

            return positions;
        }

        public static bool ReadContainsIndels(BamAlignment alignment)
        {
            if (alignment == null)
            {
                return false;
            }

            return alignment.CigarData.HasIndels;
        }


    }

    public struct IndelSite
    {
        public readonly int PreviousMappedPosition;
        public readonly int NextMappedPosition;
        public readonly char IndelType;
        public readonly int Length;
        public readonly bool IsTerminal;

        public IndelSite(int previousMappedPosition, int nextMappedPosition, char indeltype, int length, bool isTerminal)
        {
            PreviousMappedPosition = previousMappedPosition;
            NextMappedPosition = nextMappedPosition;
            IndelType = indeltype;
            Length = length;
            IsTerminal = isTerminal;
        }
    }
}