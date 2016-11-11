using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Calculators;
using Pisces.Processing.Utility;
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
        /// fromt he reference genome (or in the case of indels, one base before).
        /// However, in germline (crushed) formatting, Scyllca reports all varaints in a nbhd
        /// at the same (anchored) position. This is becasue there can only ever be two alleles
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
            VariantSite[] clusterVariantSites, string referenceSequence, List<int> neighborhoodDepthAtSites, int[] clusterCountsAtSites, 
            string chromosome, int qNoiselevel, int maxQscore, int anchorPosition=-1)
        {
            if (clusterVariantSites.Length != neighborhoodDepthAtSites.Count() || neighborhoodDepthAtSites.Count() != clusterCountsAtSites.Length)
            {
                throw new Exception("Variant sites, depths, and counts arrays are different lengths.");
            }

            var referenceRemoval = new Dictionary<int, int>();

            // Initialize items we'll eventually use to build a variant.
            var alleleReference = "";
            var alleleAlternate = "";
            var totalCoverage = 0;
            var varCount = 0;

            // Initialize trackers
            var referenceCallsSuckedIntoMnv = new List<int>();
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

                // no variant here...
                if (refAlleleToAdd == altAlleleToAdd)
                    continue;

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
                allele = Create(chromosome, -1, alleleReference, alleleAlternate, varCount, totalCoverage, AlleleCategory.Reference, qNoiselevel, maxQscore);
                return referenceRemoval;
            }

            // take average counts and depth through MNV
            // the only "holes" that lower these counts are Ns
            totalCoverage = depthsInsideMnv.Any() ? (int)depthsInsideMnv.Average() : 0;
            varCount = countsInsideMnv.Any() ? (int)countsInsideMnv.Average() : 0;

            var trueStartPosition = usingAnchor? anchorPosition : firstVariantSitePosition + precedingBasesOfAgreement;

            var indexIntoRef = (trueStartPosition - 1) - clusterVariantSites[0].VcfReferencePosition;
            var prependableBase = "R";
            if ((indexIntoRef >= 0) && (indexIntoRef < referenceSequence.Length))
                prependableBase = referenceSequence[indexIntoRef].ToString();

            //compacted to an insertion
            if ((alleleReference.Length == 0) && (alleleAlternate.Length != 0))
                allele = Create(chromosome, trueStartPosition - 1, prependableBase + alleleReference, prependableBase + alleleAlternate,
                    varCount, totalCoverage, AlleleCategory.Insertion, qNoiselevel, maxQscore);
            //compacted to an insertion
            else if ((alleleReference.Length != 0) && (alleleAlternate.Length == 0))
                allele = Create(chromosome, trueStartPosition - 1, prependableBase + alleleReference, prependableBase + alleleAlternate,
                    varCount, totalCoverage, AlleleCategory.Deletion, qNoiselevel, maxQscore);
            else  //MNV,pretty much what we were expecting. (and every time we are using an anchor)
            {
                allele = Create(chromosome, trueStartPosition, alleleReference, alleleAlternate,
                    varCount, totalCoverage, AlleleCategory.Mnv, qNoiselevel, maxQscore);
            }


            if (varCount==0)
                allele = Create(chromosome, trueStartPosition, alleleReference, ".",
                   varCount, totalCoverage, AlleleCategory.Reference, qNoiselevel, maxQscore);

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
            string alleleAlternate, int varCount, int totalCoverage, AlleleCategory category, int qNoiselevel, int maxQscore)
        {
            if (totalCoverage < varCount)  //sometimes the orignal vcf and the bam dont agree...
                totalCoverage = varCount;

            if (category == AlleleCategory.Reference)
            {
                return new CalledAllele()
                {
                    Chromosome = chromosome,
                    Coordinate = alleleCoordinate,
                    Reference = alleleReference,
                    Alternate = alleleAlternate,
                    TotalCoverage = totalCoverage,
                    AlleleSupport = varCount,
                    ReferenceSupport = varCount,
                    VariantQscore = VariantQualityCalculator.AssignPoissonQScore(varCount, totalCoverage, qNoiselevel, maxQscore)
                };

            }

            return new CalledAllele(category)
            {
                Chromosome = chromosome,
                Coordinate = alleleCoordinate,
                Reference = alleleReference,
                Alternate = alleleAlternate,
                TotalCoverage = totalCoverage,
                AlleleSupport = varCount,
                ReferenceSupport = totalCoverage - varCount,
                VariantQscore = VariantQualityCalculator.AssignPoissonQScore(varCount, totalCoverage, qNoiselevel, maxQscore)
            };
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
