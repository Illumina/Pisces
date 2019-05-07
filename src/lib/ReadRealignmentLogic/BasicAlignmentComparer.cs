using System;
using System.Linq;
using ReadRealignmentLogic.Models;

namespace ReadRealignmentLogic
{
    public class BasicAlignmentComparer : AlignmentComparer
    {
        public const uint MinReductionInMismatch = 3;
        public const uint MaxTotalMismatch = 2;
        public const uint MaxMismatchCreatedByIndel = 1;
        // More strict with short indels
        public uint MaxTotalMismatchShortIndel = Math.Max(MaxTotalMismatch - 1, 0);
        public uint MaxMismatchCreatedByIndelShortIndel = Math.Max(MaxMismatchCreatedByIndel - 1, 0);
        // Less strict with preferred indels, i.e. long or high frequency
        public uint MinReductionInMismatchPreferred = Math.Max(MinReductionInMismatch - 1, 1);

        /// <summary>
        /// When comparing results:
        /// - Always minimize number of mismatches, regardless of number of indels
        /// - Given same number of mismatches, prefer fewer non-N softclips (0 better than 1, 1 better than 2)
        /// - Given same number of mismatches, prefer fewer indels (0 better than 1, 1 better than 2)
        /// 
        /// This maps to the following scenarios (written out to be explicit)
        /// indels =, mismatch =, 0
        /// indels =, mismatch <, 1
        /// indels =, mismatch >, -1
        /// indels <, mismatch =, 1
        /// indels <, mismatch <, 1
        /// indels <, mismatch >, -1
        /// indels >, mismatch =, -1
        /// indels >, mismatch <, 1
        /// indels >, mismatch >, -1
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override int CompareAlignments(AlignmentSummary original, AlignmentSummary other)
        {
            if (other == null) return 1;

            if (original.NumMismatches == 1 && original.NumIndels == 0 && other.NumIndels > 1) return 1;
            if (other.NumMismatches == 1 && other.NumIndels == 0 && original.NumIndels > 1) return -1;

            if (original.NumMismatches < other.NumMismatches) return 1;
            if (original.NumMismatches > other.NumMismatches) return -1;

            if (original.NumNonNSoftclips < other.NumNonNSoftclips) return 1;
            if (original.NumNonNSoftclips > other.NumNonNSoftclips) return -1;

            if (original.NumIndels < other.NumIndels) return 1;
            if (original.NumIndels > other.NumIndels) return -1;
            return 0;
        }

        public override int CompareAlignmentsWithOriginal(AlignmentSummary other, AlignmentSummary original, bool treatKindly = false)
        {

            if (original == null) return 1;
            
            // when realignment has zero mismatch
            if (other.NumMismatchesIncludeSoftclip == 0)
            {
                // special rule for one indel vs. one mismatch 
                if (other.NumIndels == 1 && original.NumMismatchesIncludeSoftclip == 1 && original.NumIndels == 0) return -1;

                if (original.NumIndels > 0) return 1;

                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= 1) return 1;

                else return -1;
            }

            // mismatches not changed by indel realignment
            var numSharedMismatch = original.MismatchesIncludeSoftclip != null ? original.MismatchesIncludeSoftclip.Intersect(other.MismatchesIncludeSoftclip).ToList().Count() : 0;

            // More strict with short indels
            if (other.NumIndelBases <= 3)
            {
                if (other.NumMismatchesIncludeSoftclip - numSharedMismatch <= MaxMismatchCreatedByIndelShortIndel && other.NumMismatchesIncludeSoftclip <= MaxTotalMismatchShortIndel && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= MinReductionInMismatch) return 1;
                else return -1;
            }
            
            // all other cases
            if (other.NumMismatchesIncludeSoftclip - numSharedMismatch <= MaxMismatchCreatedByIndel && other.NumMismatchesIncludeSoftclip <= MaxTotalMismatch)
            {
                if (original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= MinReductionInMismatch) return 1;

                // give preference to long indels
                if (other.NumIndelBases - original.NumIndelBases >= 9 && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= MinReductionInMismatchPreferred) return 1;

                // give preference to indels that have high frequency to start with
                if (other.HasHighFrequencyIndel && original.NumMismatchesIncludeSoftclip - other.NumMismatchesIncludeSoftclip >= MinReductionInMismatchPreferred) return 1;

            }

            return -1;
        }
    }
}