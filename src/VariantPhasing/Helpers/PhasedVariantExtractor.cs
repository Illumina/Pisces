using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Calculators;
using Common.IO.Utility;
using VariantPhasing.Models;

namespace VariantPhasing.Logic
{
    public static class PhasedVariantExtractor
    {

        /// <summary>
        /// Warning #1. This algorithm has an inherent assumption: 
        /// the VS must be in order of their true position (first base of difference).
        /// Thats not always how they appeared in the vcf.
        /// Warning #2. Variants are typically reported in the VCF on their first base of difference 
        /// from the reference genome (or in the case of indels, one base before).
        /// However, in germline (crushed) formatting, Scylla reports all variants in a nbhd
        /// at the same (anchored) position. This is because there can only ever be two alleles
        /// given the diploid assumption. So, you cant report 5 different alleles at 5 spots withn the neighborhood.
        /// IE, somatic genotyping/reporting is loci-specific.
        /// but diploid genotyping/reporting is is forced to be consistent through the whole neighborhood.
        /// </summary>
        /// <param name="allele"> allele we are going to create from the cluster</param>
        /// <param name="clusterVariantSites">the variant site results for the cluster</param>
        /// <param name="referenceSequence">the reference seqeunce, so we can populate inbetween the MNVs</param>
        /// <param name="neighborhoodDepthAtSites">depths needed to populate the new allele</param>
        /// <param name="clusterCountsAtSites">call counts needed to populate the new allele</param>
        /// <param name="chromosome">chr needed to populate the new allele</param>
        /// <param name="qNoiselevel">NL needed to populate the new allele</param>
        /// <param name="maxQscore">Q max needed to determine Q score of the new allele</param>
        /// <param name="anchorPosition">if we are forcing the allele to be at a given position, instead of the poisition it would naturally be at in the VCF file</param>
        /// <returns></returns>
        public static Dictionary<int,int> Extract(out CalledAllele allele,
            VariantSite[] clusterVariantSites, string referenceSequence, int[] neighborhoodDepthAtSites, int[] neighborhoodNoCallsAtSites, int[] clusterCountsAtSites, 
            string chromosome, int qNoiselevel, int maxQscore, int anchorPosition=-1)
        {
            if (clusterVariantSites.Length != neighborhoodDepthAtSites.Length || neighborhoodDepthAtSites.Length != clusterCountsAtSites.Length)
            {
                throw new InvalidDataException("Variant sites, depths, and counts arrays are different lengths.");
            }

            var referenceRemoval = new Dictionary<int, int>();

            // Initialize items we'll eventually use to build a variant.
            var alleleReference = "";
            var alleleAlternate = "";
            var totalCoverage = 0;
            var varCount = 0;
            var noCallCount = 0;

            // Initialize trackers
            var referenceCallsSuckedIntoMnv = new List<int>();
            var nocallsInsideMnv = new List<int>();
            var depthsInsideMnv = new List<int>();
            var countsInsideMnv = new List<int>();

            var lastRefBaseSitePosition = clusterVariantSites[0].VcfReferencePosition;
            var firstVariantSitePosition = clusterVariantSites[0].VcfReferencePosition;
            var differenceStarted = false;
            var lengthOfRefAllele = -1;
            var lengthOfAltAllele = -1;

            bool usingAnchor = (anchorPosition != -1);

            if (usingAnchor)
                lastRefBaseSitePosition = anchorPosition - 1;

            // Walk through the cluster's variant sites and build up ref/alt strings, average support, and average coverage
            for (var siteIndex = 0; siteIndex < clusterVariantSites.Length; siteIndex++)
            {
                var consensusSite = clusterVariantSites[siteIndex];

                var refAlleleToAdd = consensusSite.TrueRefAllele;
                var altAlleleToAdd = consensusSite.TrueAltAllele;
                var currentPosition = consensusSite.TrueFirstBaseOfDiff;
                var diff = lastRefBaseSitePosition - currentPosition;

                // no variant here...
                if (refAlleleToAdd == altAlleleToAdd)
                    continue;

                if (differenceStarted && (diff >= 0))
                {
                    //We have a problem. the last site we added overlaps with the current site we want to add.
                    //The probably are not in conflict. But we will had to do some kind of sub string to get this right..
                   
                    var lengthToTrimFromStart = diff + 1 ;

                    if ((lengthToTrimFromStart < consensusSite.TrueAltAllele.Length) &&
                        (lengthToTrimFromStart < consensusSite.TrueRefAllele.Length))
                    {
                        refAlleleToAdd = consensusSite.TrueRefAllele.Substring(lengthToTrimFromStart);
                        altAlleleToAdd = consensusSite.TrueAltAllele.Substring(lengthToTrimFromStart);
                        currentPosition = consensusSite.TrueFirstBaseOfDiff + lengthToTrimFromStart;
                    }
                    else
                        continue; //if the last variant site entirely covered this one, just dont worry about it.
                }


                if (differenceStarted || usingAnchor)
                {
                    var gapLength = currentPosition - lastRefBaseSitePosition - 1;
                    var suckedUpRefPositions = new List<int>();
                    for (var i = 0; i < gapLength; i++)
                    {
                        var refPosition = lastRefBaseSitePosition + i + 1;
                        suckedUpRefPositions.Add(refPosition);
                    }
                    referenceCallsSuckedIntoMnv.AddRange(suckedUpRefPositions);

                    var gapFiller = FillGapWithReferenceData(referenceSequence,
                        clusterVariantSites[0], suckedUpRefPositions);

                    alleleReference += gapFiller;
                    alleleAlternate += gapFiller;
                }

                if (!differenceStarted)
                    firstVariantSitePosition = currentPosition;
                   
                differenceStarted = true;
                depthsInsideMnv.Add(neighborhoodDepthAtSites[siteIndex]);
                countsInsideMnv.Add(clusterCountsAtSites[siteIndex]);
                nocallsInsideMnv.Add(neighborhoodNoCallsAtSites[siteIndex]);

                //this takes into account taking deletions out of the ref allele.
                lastRefBaseSitePosition = currentPosition + refAlleleToAdd.Length - 1;

                alleleReference += refAlleleToAdd;
                alleleAlternate += altAlleleToAdd;

                lengthOfRefAllele = alleleReference.Length;
                lengthOfAltAllele = alleleAlternate.Length;
            }

            //if we are not anchored, we trim off preceding bases of agreement, and move up the cooridnate to
            //the first base of difference.
            var precedingBasesOfAgreement = usingAnchor? 0 : GetNumPrecedingAgreement(alleleReference, alleleAlternate);

            if (differenceStarted)
            {
                alleleReference = alleleReference.Substring(precedingBasesOfAgreement,
                    lengthOfRefAllele - precedingBasesOfAgreement);
                alleleAlternate = alleleAlternate.Substring(precedingBasesOfAgreement,
                    lengthOfAltAllele - precedingBasesOfAgreement);
            }

            if (!differenceStarted || (alleleReference.Length == 0) && (alleleAlternate.Length == 0))
            {
                //taking out the preceding bases, the phased variant compacted to nothing!
                allele = Create(chromosome, -1, alleleReference, alleleAlternate, varCount, noCallCount, totalCoverage, AlleleCategory.Reference, qNoiselevel, maxQscore);
                return referenceRemoval;
            }

            // take average counts and depth through MNV
            // the only "holes" that lower these counts are Ns
            totalCoverage = depthsInsideMnv.Any() ? (int)depthsInsideMnv.Average() : 0;
            varCount = countsInsideMnv.Any() ? (int)countsInsideMnv.Average() : 0;
            noCallCount = nocallsInsideMnv.Any() ? (int)nocallsInsideMnv.Average() : 0;

            var trueStartPosition = usingAnchor? anchorPosition : firstVariantSitePosition + precedingBasesOfAgreement;

            var indexIntoRef = (trueStartPosition - 1) - clusterVariantSites[0].VcfReferencePosition;
            var prependableBase = "R";
            if ((indexIntoRef >= 0) && (indexIntoRef < referenceSequence.Length))
                prependableBase = referenceSequence[indexIntoRef].ToString();

            //compacted to an insertion
            if ((alleleReference.Length == 0) && (alleleAlternate.Length != 0))
                allele = Create(chromosome, trueStartPosition - 1, prependableBase + alleleReference, prependableBase + alleleAlternate,
                    varCount, noCallCount, totalCoverage, AlleleCategory.Insertion, qNoiselevel, maxQscore);
            //compacted to an insertion
            else if ((alleleReference.Length != 0) && (alleleAlternate.Length == 0))
                allele = Create(chromosome, trueStartPosition - 1, prependableBase + alleleReference, prependableBase + alleleAlternate,
                    varCount, noCallCount, totalCoverage, AlleleCategory.Deletion, qNoiselevel, maxQscore);
            else  //MNV,pretty much what we were expecting. (and every time we are using an anchor)
            {
                allele = Create(chromosome, trueStartPosition, alleleReference, alleleAlternate,
                    varCount, noCallCount, totalCoverage, AlleleCategory.Mnv, qNoiselevel, maxQscore);
            }


            if (varCount==0)
                allele = Create(chromosome, trueStartPosition, alleleReference, ".",
                   varCount, noCallCount, totalCoverage, AlleleCategory.Reference, qNoiselevel, maxQscore);

            foreach (var suckedupRefPos in referenceCallsSuckedIntoMnv)
            {
                if ((usingAnchor) || (suckedupRefPos > trueStartPosition))
                    referenceRemoval.Add(suckedupRefPos, varCount);
            }

            return referenceRemoval;
        }


        /// <summary>
        /// Warning. This algorithm has an inherent assumption: 
        /// the VS must be in order of their true position (first base of difference).
        /// Thats not always how they appeared in the vcf.
        /// </summary>
        /// <param name="allele"></param>
        /// <param name="clusterVariantSites"></param>
        /// <param name="referenceSequence"></param>
        /// <param name="neighborhoodDepthAtSites"></param>
        /// <param name="clusterCountsAtSites"></param>
        /// <param name="chromosome"></param>
        /// <returns></returns>

        public static CalledAllele Create(string chromosome, int alleleCoordinate, string alleleReference,
            string alleleAlternate, int varCount, int noCallCount, int totalCoverage, AlleleCategory category, int qNoiselevel, int maxQscore)
        {
            if (totalCoverage < varCount)  //sometimes the orignal vcf and the bam dont agree...
                totalCoverage = varCount;

            int refSpt = totalCoverage - varCount;

            if (category == AlleleCategory.Reference)
            {
                refSpt = varCount;
            }

            var allele = new CalledAllele(category)
                {
                    Chromosome = chromosome,
                    ReferencePosition = alleleCoordinate,
                    ReferenceAllele = alleleReference,
                    AlternateAllele = alleleAlternate,
                    TotalCoverage = totalCoverage,
                    Type = category,
                    AlleleSupport = varCount,
                    ReferenceSupport = refSpt,
                    NumNoCalls = noCallCount,
                    VariantQscore = VariantQualityCalculator.AssignPoissonQScore(varCount, totalCoverage, qNoiselevel, maxQscore)
                };

            allele.SetFractionNoCalls();
            return allele;
        }

        private static string FillGapWithReferenceData(string reference,
            VariantSite variantSite, IEnumerable<int> suckedUpReferenceCalls)
        {
            var gapFiller = "";
            foreach (var refPosition in suckedUpReferenceCalls)
            {
                var indexIntoRef = refPosition - variantSite.VcfReferencePosition;

                if (reference.Length == 0)
                {
                    gapFiller += "R";
                }
                else if ((indexIntoRef >= 0) && (indexIntoRef < reference.Length))
                {
                    gapFiller += reference[indexIntoRef];
                }
                else
                {
                    Logger.WriteToLog("Reference issue:");
                    Logger.WriteToLog("Reference:" + reference);
                    Logger.WriteToLog("Index:" + indexIntoRef);
                    Logger.WriteToLog("Start of nbhd:" + variantSite.VcfReferencePosition);
                    //Logger.WriteToLog("LastVSPosition:" + lastRefBaseSitePosition);
                    gapFiller += "R";
                }

            }
            return gapFiller;
        }

        private static int GetNumPrecedingAgreement(string alleleReference, string alleleAlternate)
        {
            var numBasesAgree = 0;

            while (true)
            {
                if (alleleReference.Length == numBasesAgree)
                    break;

                if (alleleAlternate.Length == numBasesAgree)
                    break;

                if (alleleReference[numBasesAgree] == alleleAlternate[numBasesAgree])
                    numBasesAgree++;
                else
                    break;
            }

            return numBasesAgree;
        }

    }
}
