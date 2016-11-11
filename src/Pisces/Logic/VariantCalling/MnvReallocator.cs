using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic.VariantCalling
{
    public static class MnvReallocator
    {
        public static IEnumerable<CalledAllele> ReallocateFailedMnvs(List<CalledAllele> failedMnvs, List<CalledAllele> callableAlleles, int? blockMaxPos= null)
        {
            var outsideThisBlock = new List<CalledAllele>();
            foreach (var remainderAlleles in failedMnvs.Select(failedMnv => new List<CalledAllele>() { failedMnv }))
            {
                while (remainderAlleles.Count > 0)
                {
                    var alleleToReassign = remainderAlleles.First();

                    var potentialOverlaps = callableAlleles.Where(a => IsPotentialOverlap(a, alleleToReassign));

                    //Prioritize longest sub-mnvs, use allele support for tiebreaker
                    var orderedOverlaps = potentialOverlaps.OrderByDescending(a => a.Alternate.Length).ThenByDescending(a => a.AlleleSupport).ToList();
                    var reallocated = false;

                    var matchingOverlaps = orderedOverlaps.Where(o => OverlapMatches(o, alleleToReassign));

                    if (blockMaxPos.HasValue)
                    {
                        var distanceIntoNextBlock = (int)(alleleToReassign.Coordinate + (alleleToReassign.Alternate.Length - 1) - blockMaxPos);

                        if (distanceIntoNextBlock > 0 && !matchingOverlaps.Any(o => o.Alternate.Length > 1))
                        {
                            if (alleleToReassign.Coordinate <= blockMaxPos)
                            {
                                //Peel off into next block
                                var coordinate = (int) blockMaxPos + 1;
                                var originalAlleleLength = alleleToReassign.Reference.Length;

                                var nextBlockVariant = CreateVariant(alleleToReassign.Chromosome, coordinate, 0,
                                    alleleToReassign.Alternate.Substring(originalAlleleLength - distanceIntoNextBlock,
                                        distanceIntoNextBlock),
                                    alleleToReassign.Reference.Substring(originalAlleleLength - distanceIntoNextBlock,
                                        distanceIntoNextBlock));

                                var nextBlockVariants = BreakOffEdgeReferences(nextBlockVariant);

                                ProcessOverlap(blockMaxPos, nextBlockVariants.First(),
                                    alleleToReassign, remainderAlleles, outsideThisBlock);
                            }
                            else
                            {   
                                // fully in next block, no need to peel
                                remainderAlleles.Remove(alleleToReassign);
                                outsideThisBlock.Add(alleleToReassign);
                            }

                            reallocated = true;
                        }
                    }

                    if (!reallocated && matchingOverlaps.Any())
                    {
                        // We got a callableAllele match. Increment support and push the remaining Mnvs (if any) through the process
                        ProcessOverlap(blockMaxPos, matchingOverlaps.First(), alleleToReassign, remainderAlleles, outsideThisBlock);
                        reallocated = true;
                    }

                    if (!reallocated)
                    {
                        var singleNucs = BreakDownToSingleNucCalls(alleleToReassign);
                        if (blockMaxPos.HasValue)
                        {
                            foreach (var singleNuc in singleNucs)
                            {
                                if (singleNuc.Coordinate <= blockMaxPos)
                                {
                                    callableAlleles.Add(singleNuc);
                                }
                                else
                                {
                                    outsideThisBlock.Add(singleNuc);
                                }
                            }
                        }
                        else
                        {
                            callableAlleles.AddRange(singleNucs);                            
                        }
                        remainderAlleles.Remove(alleleToReassign);
                    }
                }
            }
            return outsideThisBlock;
        }

        private static void ProcessOverlap(int? blockMaxPos, CalledAllele overlap,
            CalledAllele alleleToReassign, List<CalledAllele> remainderAlleles, List<CalledAllele> outsideThisBlock)
        {
            overlap.AlleleSupport += alleleToReassign.AlleleSupport;

            for (int i = 0; i < alleleToReassign.SupportByDirection.Length; i++)
            {
                overlap.SupportByDirection[i] += alleleToReassign.SupportByDirection[i];
            }

            remainderAlleles.Remove(alleleToReassign);

            var remainders = CreateAllelesFromRemainder(overlap, alleleToReassign);
            if (blockMaxPos.HasValue)
            {
                if (overlap.Coordinate > blockMaxPos)
                {
                    remainderAlleles.Remove(overlap);
                    outsideThisBlock.Add(overlap);
                }

                foreach (var remainder in remainders)
                {
                    if (remainder.Coordinate <= blockMaxPos)
                    {
                        remainderAlleles.Add(remainder);
                    }
                    else
                    {
                        outsideThisBlock.Add(remainder);
                    }
                }
            }
            else
            {
                remainderAlleles.AddRange(remainders);
            }
        }

        private static IEnumerable<CalledAllele> BreakDownToSingleNucCalls(CalledAllele alleleToReassign)
        {
            var singleNucCalls = new List<CalledAllele>();
            for (var i = 0; i < alleleToReassign.Alternate.Length; i++)
            {
                var alternate = alleleToReassign.Alternate.Substring(i, 1);
                var reference = alleleToReassign.Reference.Substring(i, 1);

                var singleNucCall = CreateVariant(alleleToReassign.Chromosome, alleleToReassign.Coordinate + i,
                    alleleToReassign.AlleleSupport,
                    alternate, reference, alleleToReassign.SupportByDirection);

                if (!(singleNucCall.Type == AlleleCategory.Reference)) singleNucCalls.Add(singleNucCall);
            }
            return singleNucCalls;
        }

        private static CalledAllele CreateVariant(string chromosome, int coordinate, int alleleSupport,
            string alternate, string reference, int[] supportByDirection = null)
        {
            var calledAllele = alternate.Equals(reference, StringComparison.InvariantCultureIgnoreCase)
             ? new CalledAllele()
             : new CalledAllele(alternate.Length > 1 ? AlleleCategory.Mnv : AlleleCategory.Snv);

            calledAllele.Chromosome = chromosome;
            calledAllele.Coordinate = coordinate;
            calledAllele.AlleleSupport = alleleSupport;
            calledAllele.Alternate = alternate;
            calledAllele.Reference = reference;
            if (supportByDirection!=null) Array.Copy(supportByDirection, calledAllele.SupportByDirection,supportByDirection.Length);


            return calledAllele;

        }

        private static IEnumerable<CalledAllele> CreateAllelesFromRemainder(CalledAllele overlap, CalledAllele alleleToReassign)
        {
            var remainders = new List<CalledAllele>();
            var overlapIndexInFailedMnv = overlap.Coordinate - alleleToReassign.Coordinate;
            var overlapAlleleLength = overlap.Alternate.Length;
            var rightSideOverlap = overlapIndexInFailedMnv + overlapAlleleLength;

            if (alleleToReassign.Alternate.Length - rightSideOverlap > 0 && rightSideOverlap <= alleleToReassign.Coordinate + alleleToReassign.Alternate.Length)
            {
                var rightRemainder = CreateVariant(alleleToReassign.Chromosome,
                    alleleToReassign.Coordinate + rightSideOverlap, alleleToReassign.AlleleSupport,
                    alleleToReassign.Alternate.Substring(rightSideOverlap,
                    alleleToReassign.Alternate.Length - rightSideOverlap),
                    alleleToReassign.Reference.Substring(rightSideOverlap,
                        alleleToReassign.Alternate.Length - rightSideOverlap), alleleToReassign.SupportByDirection);

                if (!(rightRemainder.Type == AlleleCategory.Reference)) remainders.Add(rightRemainder);
            }

            if (overlapIndexInFailedMnv > 0)
            {
                var leftRemainder = CreateVariant(alleleToReassign.Chromosome, alleleToReassign.Coordinate,
                    alleleToReassign.AlleleSupport,
                    alleleToReassign.Alternate.Substring(0, overlapIndexInFailedMnv),
                    alleleToReassign.Reference.Substring(0, overlapIndexInFailedMnv),
                    alleleToReassign.SupportByDirection
                    );
                if (!(leftRemainder.Type == AlleleCategory.Reference)) remainders.Add(leftRemainder);
            }

            //if any remainders begin with ref, split those out into separate remainders.

            var remaindersWithRefsBrokenOut = new List<CalledAllele>();
            foreach (var remainder in remainders)
            {
                remaindersWithRefsBrokenOut.AddRange(BreakOffEdgeReferences(remainder));
            }
            return remaindersWithRefsBrokenOut;
        }

        public static IEnumerable<CalledAllele> BreakOffEdgeReferences(CalledAllele allele)
        {
            var alleles = new List<CalledAllele>();
            if (allele.Type != AlleleCategory.Mnv)
            {
                alleles.Add(allele);
                return alleles;
            }

            var leftAdjust = 0;
            var rightAdjust = 0;

            for (var i = 0; i < allele.Reference.Length; i++)
            {
                if (allele.Reference[i] != allele.Alternate[i]) break;
                leftAdjust++;
            }
            for (var i = 0; i < allele.Reference.Length; i++)
            {
                var indexInAllele = allele.Reference.Length - 1 - i;
                if (allele.Reference[indexInAllele] != allele.Alternate[indexInAllele]) break;
                rightAdjust++;
            }
            
            var restOfMnv = CreateVariant(allele.Chromosome, allele.Coordinate + leftAdjust, allele.AlleleSupport,
                allele.Alternate.Substring(leftAdjust, allele.Alternate.Length - (leftAdjust + rightAdjust)),
                allele.Reference.Substring(leftAdjust, allele.Reference.Length - (leftAdjust + rightAdjust)),
                allele.SupportByDirection);

            alleles.Add(restOfMnv);
            return alleles;                
        } 

        private static bool OverlapMatches(CalledAllele overlap, CalledAllele alleleToReassign)
        {
            var overlapIndexInFailedMnv = overlap.Coordinate - alleleToReassign.Coordinate;
            var overlapAlleleLength = overlap.Alternate.Length;
            return overlap.Alternate.Equals(alleleToReassign.Alternate.Substring(overlapIndexInFailedMnv, overlapAlleleLength));
        }

        private static bool IsPotentialOverlap(CalledAllele callableAllele, CalledAllele failedMnv)
        {
            return callableAllele.Coordinate >= failedMnv.Coordinate
                   && callableAllele.Chromosome == failedMnv.Chromosome
                   && callableAllele.Coordinate <= (failedMnv.Coordinate + failedMnv.Alternate.Length)
                   && callableAllele.Alternate.Length <= failedMnv.Alternate.Length
                   && callableAllele.Coordinate + callableAllele.Alternate.Length <= (failedMnv.Coordinate + failedMnv.Alternate.Length)
                   && (callableAllele.Type == AlleleCategory.Mnv
                   || callableAllele.Type == AlleleCategory.Snv
                   || callableAllele.Type == AlleleCategory.Reference);
        }
    }
}