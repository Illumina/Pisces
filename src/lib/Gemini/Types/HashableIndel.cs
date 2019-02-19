using Pisces.Domain.Types;

namespace Gemini.Types
{
    public struct HashableIndel
    {
        public AlleleCategory Type;
        public int Length;
        public string Chromosome;
        public int ReferencePosition;
        public string ReferenceAllele;
        public string AlternateAllele;
        public int Score; // TODO this probably belongs elsewhere.
        public bool AllowMismatchingInsertions;
        public bool InMulti;
        public string OtherIndel;
    }
}