using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.Models;
using Gemini.Types;

namespace Gemini.ClassificationAndEvidenceCollection
{
    public class PairResult
    {
        public PairResult()
        {

        }
        public PairResult(IEnumerable<BamAlignment> alignments, ReadPair readPair, 
            PairClassification classification = PairClassification.Unknown, bool hasIndels = false, bool isSplit = false, int numMismatchesInSingleton = 0, int softclipLengthForIndelRead = 0)
        {
            NumMismatchesInSingleton = numMismatchesInSingleton;
            IsSplit = isSplit;
            HasIndels = hasIndels;
            Classification = classification;
            Alignments = alignments;
            ReadPair = readPair;
            SoftclipLengthForIndelRead = softclipLengthForIndelRead;
        }

        public IEnumerable<BamAlignment> Alignments { get; }
        public PairClassification Classification { get; set; }
        public bool HasIndels { get; }
        public bool IsSplit { get; }
        public int NumMismatchesInSingleton { get; }
        public int SoftclipLengthForIndelRead { get; }
        public bool TriedStitching { get; set; }
        public bool R1Confirmed { get; set; }
        public bool R2Confirmed { get; set; }
        public bool IsReputableIndelContaining { get; set; }
        public int R1Nm;
        public int R2Nm;
        public List<PreIndel> OriginalIndelsR1;
        public List<PreIndel> OriginalIndelsR2;
        // Temporary
        public ReadPair ReadPair;
        public MdCounts md1;
        public MdCounts md2;

    }
}