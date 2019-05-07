using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Calculators;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Logic.VariantCalling
{
    public class VariantCollapser : IVariantCollapser, IComparer<CandidateAllele>
    {
        public int TotalNumCollapsed { get; set; }
        private List<CandidateAllele> _knownVariants;
        private ICoverageCalculator _coverageCalculator;
        private float _freqThreshold;
        private float _freqRatioThreshold;
        private bool _excludeMNVs = true;

        public VariantCollapser(List<CandidateAllele> knownCollapsableVariants, bool excludeMNVs = false, ICoverageCalculator coverageCalculator = null, float freqThreshold = 0f, float freqRatioThreshold = 0f)
        {
            _knownVariants = knownCollapsableVariants;
            _coverageCalculator = coverageCalculator ?? new CoverageCalculator();
            _freqThreshold = freqThreshold;
            _freqRatioThreshold = freqRatioThreshold;
            _excludeMNVs = excludeMNVs;
        }

        public List<CandidateAllele> Collapse(List<CandidateAllele> candidates, IAlleleSource source, int? maxClearedPosition)
        {
            var targetVariants = _excludeMNVs ? candidates.Where(x => x.Type != AlleleCategory.Mnv).ToList() : candidates.ToList();

            AnnotateKnown(targetVariants);

            // Only try to collapse variants that are not fully closed
            // Start with largest ones first - builds evidence better,
            // then try to collapse those that are the most "open".
            // Tiebreaker on alleles to ensure we are deterministic.
            // Try to collapse to fully anchored variants
            var variantsToCollapse = targetVariants.Where(v => (v.OpenOnLeft || v.OpenOnRight)).OrderByDescending(c => c.Length)
                .ThenByDescending(c => c.OpenOnLeft && c.OpenOnRight)
                .ThenByDescending(c => c.OpenOnLeft || c.OpenOnRight)
                .ThenBy(c=> c.ReferenceAllele).ThenBy(c=>c.AlternateAllele)
                .ThenBy(c => c.Support).ThenBy(c => c.OpenOnRight).ThenBy(c => c.OpenOnLeft)
                .ToList();

            for (var i = 0; i < variantsToCollapse.Count; i ++)
            {
                var variantToCollapse = variantsToCollapse[i];

                var match = GetMatches(variantToCollapse, targetVariants, source);

                if (match != null)
                {              
                    TotalNumCollapsed ++;
                    Collapse(match, variantToCollapse);

                    targetVariants.RemoveAll(v => v == variantToCollapse); // remove from target list
                    candidates.RemoveAll(v => v == variantToCollapse); // remove from total list
                }
            }


            // remove candidates outside of cleared region that we couldnt collapse and add back to source
            if (maxClearedPosition.HasValue)
            {
                var notClearedVariants = candidates.Where(c => c.ReferencePosition > maxClearedPosition && c.Type!=AlleleCategory.Reference).ToList();
                foreach (var nonClearedVariant in notClearedVariants)
                {
                    candidates.Remove(nonClearedVariant);
                }
                source.AddCandidates(notClearedVariants);
            }

            // TODO Consider building out functionality for Pisces to combine support for insertions that are the same except for Ns
            //var insertions = candidates.Where(x => x.Type == AlleleCategory.Insertion);
            //var groupedInsertions = insertions.GroupBy(x => x.ReferencePosition + "_" + x.Length);

            //var dominantAlleleMultiplier = 2;
            //foreach (var groupedInsertion in groupedInsertions.Where(g=>g.Count() > 1))
            //{
            //    var orderedInsertions = groupedInsertion.OrderByDescending(x => x.Support).ToList();
            //    if (orderedInsertions[0].Support >= (orderedInsertions[1].Support * dominantAlleleMultiplier))
            //    {
            //        var insertionsToAdd = new List<CandidateAllele>();

            //        for (int i = 0; i < orderedInsertions.Count(); i++)
            //        {
            //            if (orderedInsertions[i].AlternateAllele.Contains("N"))
            //            {

            //            }
            //            else
            //            {

            //            }
            //        }
            //    }
            //    else
            //    {
            //        continue;
            //    }


            //    // Only one should be non-N containing


            //}

            return candidates;
        }

        private void Collapse(CandidateAllele collapseTo, CandidateAllele collapseFrom)
        {
            collapseTo.AddSupport(collapseFrom);
            collapseTo.OpenOnLeft = collapseTo.OpenOnLeft && collapseFrom.OpenOnLeft;
            collapseTo.OpenOnRight = collapseTo.OpenOnRight && collapseFrom.OpenOnRight;

            for (var i = 0; i < collapseFrom.ReadCollapsedCountsMut.Length; i ++)
                collapseTo.ReadCollapsedCountsMut[i] += collapseFrom.ReadCollapsedCountsMut[i];                
        }

        private bool CanCollapse(CandidateAllele toCollapse, CandidateAllele potentialMatch)
        {
            if ((toCollapse.Type == AlleleCategory.Insertion && potentialMatch.Type != AlleleCategory.Insertion) ||
                (toCollapse.Type != AlleleCategory.Insertion && potentialMatch.Type == AlleleCategory.Insertion) ||
                (toCollapse.Type == AlleleCategory.Deletion && potentialMatch.Type != AlleleCategory.Deletion) ||
                (toCollapse.Type != AlleleCategory.Deletion && potentialMatch.Type == AlleleCategory.Deletion) ||
                toCollapse.Length > potentialMatch.Length ||  // cannot collapse to something smaller
                (toCollapse.FullyAnchored && !potentialMatch.FullyAnchored)) // fully anchored cannot collapse to something that is open ended
                return false;

            var toCollapseBases = toCollapse.Type == AlleleCategory.Deletion ? toCollapse.ReferenceAllele : toCollapse.AlternateAllele;
            var potentialMatchBases = potentialMatch.Type == AlleleCategory.Deletion ? potentialMatch.ReferenceAllele : potentialMatch.AlternateAllele;

            // Note: to get to the situation of two fully anchored variants, there has to have been 2+ unanchored variants that had complementary anchors. Else toCollapse would never have been fully anchored.
            if (toCollapse.FullyAnchored && potentialMatch.FullyAnchored)
                return toCollapse.Equals(potentialMatch);  // fully anchored can only collapse to another fully anchored that's exactly the same

            if (toCollapse.Type == AlleleCategory.Deletion)
            {
                // no need to check that the deleted bases themselves match, but the left or right anchor position should
                if (toCollapse.OpenOnRight)
                    return potentialMatch.ReferencePosition + 1 == toCollapse.ReferencePosition + 1; // check left anchor

                return potentialMatch.ReferencePosition + potentialMatchBases.Length - 1 == toCollapse.ReferencePosition + toCollapseBases.Length - 1;  // check right anchor
            }

            if (toCollapse.OpenOnRight)
            {
                // anchored on left, alternate should match
                return potentialMatch.ReferencePosition == toCollapse.ReferencePosition &&
                       potentialMatchBases.Substring(0, toCollapseBases.Length) == toCollapseBases;
            }

            // insertion open on the left
            if (toCollapse.Type == AlleleCategory.Insertion)
            {
                // anchored on right (trailing position of insertion), alternate should match
                return potentialMatch.ReferencePosition + 1 == toCollapse.ReferencePosition + 1
                       &&
                       potentialMatchBases.Substring(potentialMatchBases.Length - toCollapseBases.Length + 1)
                       == toCollapseBases.Substring(1); // strip off reference base since we are open on left
            }

            // snv/mnv open on the left
            // potential matches are anchored on the right
            return potentialMatch.ReferencePosition + potentialMatch.AlternateAllele.Length - 1
                   == toCollapse.ReferencePosition + toCollapse.AlternateAllele.Length - 1
                   &&
                   potentialMatch.AlternateAllele.Substring(potentialMatch.AlternateAllele.Length - toCollapse.AlternateAllele.Length)
                   == toCollapse.AlternateAllele;
        }

        // mark any variant that matches a known variant - even if open ended on one or both sides
        private void AnnotateKnown(List<CandidateAllele> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (_knownVariants != null && _knownVariants.Contains(candidate))
                {
                    candidate.IsKnown = true;
                    candidate.OpenOnLeft = false;
                    candidate.OpenOnRight = false; // automatically anchor both sides
                }
            }
        }

        private CandidateAllele GetMatches(CandidateAllele toCollapse, IEnumerable<CandidateAllele> targets, IAlleleSource source)
        {
            var potentialMatches = targets.Where(c => CanCollapse(toCollapse, c)
                                                         && c != toCollapse).ToList();

            if (potentialMatches.Count == 0)
                return null;

            // reset frequency - could have changed from last time fetched
            foreach (var variant in potentialMatches)
            {
                var callableVariant = AlleleHelper.Map(variant);
                _coverageCalculator.Compute(callableVariant, source);
                variant.Frequency = callableVariant.Frequency;
            }

            // to collapse frequency
            var toCollapseCallableVariant = AlleleHelper.Map(toCollapse);
            _coverageCalculator.Compute(toCollapseCallableVariant, source);

            potentialMatches.Sort(this);

            // if there's an exact match to a fully anchored variant, take that first
            // otherwise take the most likely potential match
            var exactMatch = potentialMatches.FirstOrDefault(m => m.Equals(toCollapse) && !m.OpenOnLeft && !m.OpenOnRight);

            // if no exact match to fully anchored, take first potential match that meets threshold requirements
            return exactMatch ?? potentialMatches.FirstOrDefault(m => m.Frequency >= _freqThreshold && m.Frequency / toCollapseCallableVariant.Frequency > _freqRatioThreshold);
        }

        public int Compare(CandidateAllele first, CandidateAllele second)
        {
            // return known one first
            if (first.IsKnown && !second.IsKnown) return -1;
            if (!first.IsKnown && second.IsKnown) return 1;

            // if both known or unknown, prefer fully anchored 
            if (first.FullyAnchored && !second.FullyAnchored) return -1;
            if (!first.FullyAnchored && second.FullyAnchored) return 1;

            // if both open ended or fully anchored, try the larger one first
            if (first.Length != second.Length)
                return first.Length.CompareTo(second.Length) * -1;

            // if both the same size, go with more frequency (assuming floats are distinguishable)
            if (Math.Abs(first.Frequency - second.Frequency) > 0f)
                return first.Frequency.CompareTo(second.Frequency) * -1;

            // if both the same size, go with left one
            if (first.ReferencePosition != second.ReferencePosition)
                return first.ReferencePosition.CompareTo(second.ReferencePosition);

            // all else undistinguishable, just sort alphabetically by alternate for deterministic behavior
            return first.AlternateAllele.CompareTo(second.AlternateAllele);
        }
    }
}
