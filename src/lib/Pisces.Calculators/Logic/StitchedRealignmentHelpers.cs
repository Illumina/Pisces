using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Gemini.Logic
{
    public static class StitchedRealignmentHelpers
    {
        public static string GetUpdatedXdForRealignedStitchedRead(BamAlignment origRead, BamAlignment realignedRead)
        {
            var origXdString = origRead.GetStringTag("XD");
            if (origXdString == null)
            {
                // If we're properly only doing this on stuff that was already stitched, this should never happen anyway
                return null;
            }

            if (ReadContainsDeletions(origRead) || ReadContainsDeletions(realignedRead))
            {
                var origXd = new CigarDirection(origXdString);
                var newXd = RecalculateApproximateStitchedDirections(origXd, origRead.CigarData, realignedRead.CigarData);

                return newXd;

            }
            else
            {
                // We can keep the orig cigar directions - length hasn't changed
                return origXdString;
            }
        }

        private static string RecalculateApproximateStitchedDirections(CigarDirection cigarDirections, CigarAlignment cigarData, CigarAlignment newCigarData)
        {
            var cigarBaseDirectionMap = cigarDirections.Expand().ToArray();

            var cigarBaseAlleleMap = cigarData.Expand();
            var newCigarBaseAlleleMap = newCigarData.Expand();

            var sequencedBaseDirectionMap = new DirectionType[cigarData.GetReadSpan()];

            var directions = new List<DirectionOp>();

            var sequencedBaseIndex = 0;

            var cigarBaseIndex = 0;
            var newCigarBaseIndex = 0;
            while (true)
            {
                if (cigarBaseIndex >= cigarBaseAlleleMap.Count || newCigarBaseIndex >= newCigarBaseAlleleMap.Count)
                {
                    // If new is longer than old, fill out the rest with the last direction of the old cigar
                    if (newCigarBaseIndex < newCigarBaseAlleleMap.Count)
                    {
                        directions.Add(new DirectionOp(cigarBaseDirectionMap[cigarBaseIndex - 1], newCigarBaseAlleleMap.Count - newCigarBaseIndex));
                    }

                    break;
                }

                while (!cigarBaseAlleleMap[cigarBaseIndex].IsReadSpan())
                {
                    // Skip these
                    cigarBaseIndex++;

                    // TODO is it ever possible to go off the end here?
                }

                while (!newCigarBaseAlleleMap[newCigarBaseIndex].IsReadSpan())
                {
                    directions.Add(new DirectionOp(cigarBaseDirectionMap[cigarBaseIndex], 1)); // TODO perhaps something more nuanced here? unclear what the best solution is. For now, just be consistent: take the last one that we were on at this point in the old cigar
                    newCigarBaseIndex++;

                    // TODO is it ever possible to go off the end here?
                }

                sequencedBaseDirectionMap[sequencedBaseIndex] = cigarBaseDirectionMap[cigarBaseIndex];
                directions.Add(new DirectionOp(cigarBaseDirectionMap[cigarBaseIndex], 1));
                sequencedBaseIndex++;

                cigarBaseIndex++;
                newCigarBaseIndex++;
            }

            var compressedDirections = DirectionHelper.CompressDirections(directions);
            return new CigarDirection(compressedDirections).ToString();
        }

        private static bool ReadContainsDeletions(BamAlignment alignment)
        {
            foreach (CigarOp op in alignment.CigarData)
            {
                if (op.Type == 'D')
                {
                    return true;
                }
            }

            return false;

        }

    }
}