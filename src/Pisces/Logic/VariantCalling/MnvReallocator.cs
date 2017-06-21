using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic.VariantCalling
{
    public static class MnvReallocator
    {
        public static IEnumerable<CalledAllele> ReallocateFailedMnvs(List<CalledAllele> failedMnvs, List<CalledAllele> callableAlleles, int? blockMaxPos= null)
        {
            var overlaps = new List<string>();
            var outsideThisBlock = new List<CalledAllele>();

            foreach (var remainderAlleles in failedMnvs.OrderBy(a=>a.ReferencePosition).ThenByDescending(a => a.AlternateAllele.Length).ThenByDescending(a => a.AlleleSupport).ThenBy(a => a.AlternateAllele).ThenBy(a => a.ReferenceAllele).Select(failedMnv => new List<CalledAllele>() { failedMnv }))
            {
                while (remainderAlleles.Count > 0)
                {
                    var alleleToReassign = remainderAlleles.First();

                    var potentialOverlaps = callableAlleles.Where(a => IsPotentialOverlap(a, alleleToReassign));

                    //Prioritize longest sub-mnvs, use allele support for tiebreaker
                    var orderedOverlaps = potentialOverlaps.OrderByDescending(a => a.AlternateAllele.Length).ThenByDescending(a => a.AlleleSupport).ThenBy(a => a.AlternateAllele).ThenBy(a=> a.ReferenceAllele).ToList();
                    var reallocated = false;

                    var matchingOverlaps = orderedOverlaps.Where(o => OverlapMatches(o, alleleToReassign));

                    if (blockMaxPos.HasValue)
                    {
                        var distanceIntoNextBlock = (int)(alleleToReassign.ReferencePosition + (alleleToReassign.AlternateAllele.Length - 1) - blockMaxPos);

                        if (distanceIntoNextBlock > 0 && !matchingOverlaps.Any(o => o.AlternateAllele.Length > 1))
                        {
                            if (alleleToReassign.ReferencePosition <= blockMaxPos)
                            {
                                //Peel off into next block
                                var coordinate = (int) blockMaxPos + 1;
                                var originalAlleleLength = alleleToReassign.ReferenceAllele.Length;

                                var nextBlockVariant = CreateVariant(alleleToReassign.Chromosome, coordinate, 0,
                                    alleleToReassign.AlternateAllele.Substring(originalAlleleLength - distanceIntoNextBlock,
                                        distanceIntoNextBlock),
                                    alleleToReassign.ReferenceAllele.Substring(originalAlleleLength - distanceIntoNextBlock,
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
                                if (singleNuc.ReferencePosition <= blockMaxPos)
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
                if (overlap.ReferencePosition > blockMaxPos)
                {
                    remainderAlleles.Remove(overlap);
                    outsideThisBlock.Add(overlap);
                }

                foreach (var remainder in remainders)
                {
                    if (remainder.ReferencePosition <= blockMaxPos)
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
            for (var i = 0; i < alleleToReassign.AlternateAllele.Length; i++)
            {
                var alternate = alleleToReassign.AlternateAllele.Substring(i, 1);
                var reference = alleleToReassign.ReferenceAllele.Substring(i, 1);

                var singleNucCall = CreateVariant(alleleToReassign.Chromosome, alleleToReassign.ReferencePosition + i,
                    alleleToReassign.AlleleSupport,
                    alternate, reference, alleleToReassign.SupportByDirection);

                if (!(singleNucCall.Type == AlleleCategory.Reference)) singleNucCalls.Add(singleNucCall);
            }
            return singleNucCalls;
        }

        private static CalledAllele CreateVariant(string chromosome, int coordinate, int alleleSupport,
            string alternate, string reference, int[] supportByDirection = null)
        {
            var calledAllele = alternate.Equals(reference, StringComparison.CurrentCultureIgnoreCase)
             ? new CalledAllele()
             : new CalledAllele(alternate.Length > 1 ? AlleleCategory.Mnv : AlleleCategory.Snv);

            calledAllele.Chromosome = chromosome;
            calledAllele.ReferencePosition = coordinate;
            calledAllele.AlleleSupport = alleleSupport;
            calledAllele.AlternateAllele = alternate;
            calledAllele.ReferenceAllele = reference;
            if (supportByDirection!=null) Array.Copy(supportByDirection, calledAllele.SupportByDirection,supportByDirection.Length);


            return calledAllele;

        }

        private static IEnumerable<CalledAllele> CreateAllelesFromRemainder(CalledAllele overlap, CalledAllele alleleToReassign)
        {
            var remainders = new List<CalledAllele>();
            var overlapIndexInFailedMnv = overlap.ReferencePosition - alleleToReassign.ReferencePosition;
            var overlapAlleleLength = overlap.AlternateAllele.Length;
            var rightSideOverlap = overlapIndexInFailedMnv + overlapAlleleLength;

            if (alleleToReassign.AlternateAllele.Length - rightSideOverlap > 0 && rightSideOverlap <= alleleToReassign.ReferencePosition + alleleToReassign.AlternateAllele.Length)
            {
                var rightRemainder = CreateVariant(alleleToReassign.Chromosome,
                    alleleToReassign.ReferencePosition + rightSideOverlap, alleleToReassign.AlleleSupport,
                    alleleToReassign.AlternateAllele.Substring(rightSideOverlap,
                    alleleToReassign.AlternateAllele.Length - rightSideOverlap),
                    alleleToReassign.ReferenceAllele.Substring(rightSideOverlap,
                        alleleToReassign.AlternateAllele.Length - rightSideOverlap), alleleToReassign.SupportByDirection);

                if (!(rightRemainder.Type == AlleleCategory.Reference)) remainders.Add(rightRemainder);
            }

            if (overlapIndexInFailedMnv > 0)
            {
                var leftRemainder = CreateVariant(alleleToReassign.Chromosome, alleleToReassign.ReferencePosition,
                    alleleToReassign.AlleleSupport,
                    alleleToReassign.AlternateAllele.Substring(0, overlapIndexInFailedMnv),
                    alleleToReassign.ReferenceAllele.Substring(0, overlapIndexInFailedMnv),
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

            for (var i = 0; i < allele.ReferenceAllele.Length; i++)
            {
                if (allele.ReferenceAllele[i] != allele.AlternateAllele[i]) break;
                leftAdjust++;
            }
            for (var i = 0; i < allele.ReferenceAllele.Length; i++)
            {
                var indexInAllele = allele.ReferenceAllele.Length - 1 - i;
                if (allele.ReferenceAllele[indexInAllele] != allele.AlternateAllele[indexInAllele]) break;
                rightAdjust++;
            }
            
            var restOfMnv = CreateVariant(allele.Chromosome, allele.ReferencePosition + leftAdjust, allele.AlleleSupport,
                allele.AlternateAllele.Substring(leftAdjust, allele.AlternateAllele.Length - (leftAdjust + rightAdjust)),
                allele.ReferenceAllele.Substring(leftAdjust, allele.ReferenceAllele.Length - (leftAdjust + rightAdjust)),
                allele.SupportByDirection);

            alleles.Add(restOfMnv);
            return alleles;                
        } 

        private static bool OverlapMatches(CalledAllele overlap, CalledAllele alleleToReassign)
        {
            var overlapIndexInFailedMnv = overlap.ReferencePosition - alleleToReassign.ReferencePosition;
            var overlapAlleleLength = overlap.AlternateAllele.Length;
            return overlap.AlternateAllele.Equals(alleleToReassign.AlternateAllele.Substring(overlapIndexInFailedMnv, overlapAlleleLength));
        }

        private static bool IsPotentialOverlap(CalledAllele callableAllele, CalledAllele failedMnv)
        {
            return callableAllele.ReferencePosition >= failedMnv.ReferencePosition
                   && callableAllele.Chromosome == failedMnv.Chromosome
                   && callableAllele.ReferencePosition <= (failedMnv.ReferencePosition + failedMnv.AlternateAllele.Length)
                   && callableAllele.AlternateAllele.Length <= failedMnv.AlternateAllele.Length
                   && callableAllele.ReferencePosition + callableAllele.AlternateAllele.Length <= (failedMnv.ReferencePosition + failedMnv.AlternateAllele.Length)
                   && (callableAllele.Type == AlleleCategory.Mnv
                   || callableAllele.Type == AlleleCategory.Snv
                   || callableAllele.Type == AlleleCategory.Reference);
        }
    }
}