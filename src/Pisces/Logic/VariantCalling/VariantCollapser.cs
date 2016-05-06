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

        public VariantCollapser(List<CandidateAllele> knownCollapsableVariants, ICoverageCalculator coverageCalculator = null, float freqThreshold = 0f, float freqRatioThreshold = 0f)
        {
            _knownVariants = knownCollapsableVariants;
            _coverageCalculator = coverageCalculator ?? new CoverageCalculator();
            _freqThreshold = freqThreshold;
            _freqRatioThreshold = freqRatioThreshold;
        }

        public List<CandidateAllele> Collapse(List<CandidateAllele> candidates, IAlleleSource source, int? maxClearedPosition)
        {
            var targetVariants = candidates.ToList();

            AnnotateKnown(targetVariants);

            // only try to collapse variants that are not fully closed
            // start with largest ones first - builds evidence better
            // try to collapse to fully anchored variants
            var variantsToCollapse = targetVariants.Where(v => v.OpenOnLeft || v.OpenOnRight).OrderByDescending(c => c.Length).ToList();

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
                var notClearedVariants = candidates.Where(c => c.Coordinate > maxClearedPosition && c.Type!=AlleleCategory.Reference).ToList();
                foreach (var nonClearedVariant in notClearedVariants)
                {
                    candidates.Remove(nonClearedVariant);
                }
                source.AddCandidates(notClearedVariants);
            }

            return candidates;
        }

        private void Collapse(CandidateAllele collapseTo, CandidateAllele collapseFrom)
        {
            collapseTo.AddSupport(collapseFrom);
            collapseTo.OpenOnLeft = collapseTo.OpenOnLeft && collapseFrom.OpenOnLeft;
            collapseTo.OpenOnRight = collapseTo.OpenOnRight && collapseFrom.OpenOnRight;

            for (var i = 0; i < collapseFrom.ReadCollapsedCounts.Length; i ++)
                collapseTo.ReadCollapsedCounts[i] += collapseFrom.ReadCollapsedCounts[i];                
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

            var toCollapseBases = toCollapse.Type == AlleleCategory.Deletion ? toCollapse.Reference : toCollapse.Alternate;
            var potentialMatchBases = potentialMatch.Type == AlleleCategory.Deletion ? potentialMatch.Reference : potentialMatch.Alternate;

            if (toCollapse.FullyAnchored && potentialMatch.FullyAnchored) 
                return toCollapseBases.Equals(potentialMatchBases);  // fully anchored can only collapse to another fully anchored that's exactly the same

            if (toCollapse.Type == AlleleCategory.Deletion)
            {
                // no need to check that the deleted bases themselves match, but the left or right anchor position should
                if (toCollapse.OpenOnRight)
                    return potentialMatch.Coordinate + 1 == toCollapse.Coordinate + 1; // check left anchor

                return potentialMatch.Coordinate + potentialMatchBases.Length - 1 == toCollapse.Coordinate + toCollapseBases.Length - 1;  // check right anchor
            }

            if (toCollapse.OpenOnRight)
            {
                // anchored on left, alternate should match
                return potentialMatch.Coordinate == toCollapse.Coordinate &&
                       potentialMatchBases.Substring(0, toCollapseBases.Length) == toCollapseBases;
            }

            // insertion open on the left
            if (toCollapse.Type == AlleleCategory.Insertion)
            {
                // anchored on right (trailing position of insertion), alternate should match
                return potentialMatch.Coordinate + 1 == toCollapse.Coordinate + 1
                       &&
                       potentialMatchBases.Substring(potentialMatchBases.Length - toCollapseBases.Length + 1)
                       == toCollapseBases.Substring(1); // strip off reference base since we are open on left
            }

            // snv/mnv open on the left
            // potential matches are anchored on the right
            return potentialMatch.Coordinate + potentialMatch.Alternate.Length - 1
                   == toCollapse.Coordinate + toCollapse.Alternate.Length - 1
                   &&
                   potentialMatch.Alternate.Substring(potentialMatch.Alternate.Length - toCollapse.Alternate.Length)
                   == toCollapse.Alternate;
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
            if (first.Coordinate != second.Coordinate)
                return first.Coordinate.CompareTo(second.Coordinate);

            // all else undistinguishable, just sort alphabetically by alternate for deterministic behavior
            return first.Alternate.CompareTo(second.Alternate);
        }
    }
}
