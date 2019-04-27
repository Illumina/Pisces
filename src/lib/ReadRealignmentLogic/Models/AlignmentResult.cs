using System.Collections.Generic;
using System.Security.Cryptography;
using Pisces.Domain.Types;

namespace ReadRealignmentLogic.Models
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
        public bool IsRepeat;
        public string RepeatUnit;
        public bool IsDuplication;
        public string StringRepresentation;
        public bool IsUntrustworthyInRepeatRegion;
        public string RefPrefix;
        public string RefSuffix;
        public int NumBasesInReferenceSuffixBeforeUnique;
        public int NumRepeatsNearby;
        public int NumApproxDupsLeft;
        public int NumApproxDupsRight;
        public bool HardToCall
        {
            get { return Type == AlleleCategory.Insertion && Length > 5 || IsDuplication; }
        }
    }

    public class RealignmentResult : AlignmentSummary
    {
        public int Position { get; set; }
        public bool FailedForLeftAnchor { get; set; }
        public bool FailedForRightAnchor { get; set; }
        public string Indels { get; set; }
        public List<int> NifiedAt { get; set; }
        public List<int> IndelsAddedAt { get; set; }
        public int Attempts { get; set; }
        public List<int> AcceptedIndels { get; set; }
        public List<int> AcceptedIndelsInSubList { get; set; }
        public List<HashableIndel> AcceptedHashableIndels { get; set; }
        public bool IsSketchy { get; set; }


    }
}
