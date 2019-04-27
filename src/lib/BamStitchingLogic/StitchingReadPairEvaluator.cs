using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.Logic;

namespace BamStitchingLogic
{
    public class StitchingReadPairEvaluator : ReadPairEvaluator
    {
        private readonly bool _treatImproperPairAsIncomplete;
        private readonly bool _treatNonOverlappingAsIncomplete;
        private readonly bool _treatHalfAnchoredAsUnanchored;

        public StitchingReadPairEvaluator(bool treatImproperPairAsIncomplete, bool treatNonOverlappingAsIncomplete, bool treatHalfAnchoredAsUnanchored)
        {
            _treatImproperPairAsIncomplete = treatImproperPairAsIncomplete;
            _treatNonOverlappingAsIncomplete = treatNonOverlappingAsIncomplete;
            _treatHalfAnchoredAsUnanchored = treatHalfAnchoredAsUnanchored;
        }

        private bool AnchoredRegionContainsUnanchoredEnds(BamAlignment read1, BamAlignment read2)
        {
            var read1UnanchoredStart = read1.Position - (read1.CigarData.GetPrefixClip());
            var read1UnanchoredEnd = read1.GetLastBasePosition() + (read1.CigarData.GetSuffixClip());

            return read2.ContainsPosition(read1UnanchoredStart, read1.RefID) ||
                   read2.ContainsPosition(read1UnanchoredEnd, read1.RefID);
        }

        private bool ReadsDoNotOverlap(BamAlignment read1, BamAlignment read2)
        {
            var overlaps = read1.OverlapsAlignment(read2);
            if (overlaps) return false;

            if (_treatHalfAnchoredAsUnanchored)
            {
                return true;
            }

            // Check for S/M overlap, if half-anchoring is allowed
            var read1ContainsUnanchoredRead2 = AnchoredRegionContainsUnanchoredEnds(read1, read2);
            var read2ContainsUnanchoredRead1 = AnchoredRegionContainsUnanchoredEnds(read2, read1);
            return !(read1ContainsUnanchoredRead2 || read2ContainsUnanchoredRead1);
        }

        private bool CheckAndSetReadsDoNotOverlap(ReadPair readPair)
        {
            var dontOverlap = ReadsDoNotOverlap(readPair.Read1, readPair.Read2);
            readPair.DontOverlap = dontOverlap;
            return dontOverlap;
        }
        public override bool TreatReadPairAsIncomplete(ReadPair readPair)
        {
            return (_treatNonOverlappingAsIncomplete && CheckAndSetReadsDoNotOverlap(readPair)) 
                   || (_treatImproperPairAsIncomplete && readPair.IsImproper);
        }
    }
}