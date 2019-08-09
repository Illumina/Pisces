using System.Collections.Generic;
using Gemini.Types;

namespace Gemini.Utility
{
    public class RealignmentOptions
    {
        public bool PairAwareEverything { get; set; }
        public List<PairClassification> CategoriesForRealignment = 
            new List<PairClassification>{
            PairClassification.ImperfectStitched,
            PairClassification.FailStitch,
            PairClassification.UnstitchIndel,
            PairClassification.Unstitchable,
            PairClassification.Disagree,
            PairClassification.MessyStitched,
            PairClassification.MessySplit,
            PairClassification.UnstitchImperfect,
            PairClassification.LongFragment,
            PairClassification.UnstitchMessy,
                PairClassification.UnstitchForwardMessy,
                PairClassification.UnstitchReverseMessy,
                PairClassification.UnstitchForwardMessyIndel,
                PairClassification.UnstitchReverseMessyIndel,
                PairClassification.UnstitchMessySuspiciousRead,
                PairClassification.UnstitchMessyIndelSuspiciousRead,
                PairClassification.UnstitchMessySuspiciousMd,
                PairClassification.IndelImproper,
                PairClassification.IndelSingleton,
                PairClassification.IndelUnstitchable,
                PairClassification.UnstitchableAsSingleton

        };


        public List<PairClassification> CategoriesForSnowballing = new List<PairClassification>();
        public int NumSubSamplesForSnowballing = int.MaxValue;

        // From Hygea
        public int MaxIndelSize = 100;
        //public bool AllowRescoringOrigZero = true;
        public bool MaskPartialInsertion;
        public int MinimumUnanchoredInsertionLength;
        public bool RemaskMessySoftclips = false;
    }
}