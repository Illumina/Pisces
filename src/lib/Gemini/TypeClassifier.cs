using System.Collections.Generic;
using Gemini.Types;

namespace Gemini
{
    public static class TypeClassifier
    {
        public static readonly List<PairClassification> MessyTypes = new List<PairClassification>()
        {
            //PairClassification.Disagree,
            PairClassification.FailStitch,
            PairClassification.MessyStitched,
            PairClassification.MessySplit,
            PairClassification.UnstitchMessy,
            PairClassification.UnstitchForwardMessy,
            PairClassification.UnstitchReverseMessy,
            PairClassification.UnstitchMessySuspiciousRead,
            PairClassification.UnstitchMessyIndelSuspiciousRead,
            PairClassification.UnstitchForwardMessyIndel,
            PairClassification.UnstitchReverseMessyIndel,
            PairClassification.UnstitchMessySuspiciousMd

        };


        public static bool ClassificationIsStitchable(PairClassification classification)
        {
            return classification == PairClassification.Disagree ||
                   classification == PairClassification.FailStitch ||
                   classification == PairClassification.UnstitchIndel ||
                   classification == PairClassification.UnstitchImperfect ||
                   classification == PairClassification.UnstitchPerfect ||
                   classification == PairClassification.LongFragment ||
                   classification == PairClassification.UnstitchMessy ||
                   classification == PairClassification.UnstitchMessyIndel ||
                   classification == PairClassification.UnstitchMessySuspiciousRead ||
                   classification == PairClassification.UnstitchMessyIndelSuspiciousRead ||
                   classification == PairClassification.Unstitchable ||
                   classification == PairClassification.UnstitchSingleMismatch ||
                   classification == PairClassification.UnstitchReverseMessy ||
                   classification == PairClassification.UnstitchForwardMessy ||
                   classification == PairClassification.UnstitchForwardMessyIndel ||
                   classification == PairClassification.UnstitchReverseMessyIndel;
        }



        public static readonly List<PairClassification> _indelTypes = new List<PairClassification>()
        {
            PairClassification.UnstitchIndel,
            PairClassification.Disagree,
            PairClassification.IndelUnstitchable,
            PairClassification.IndelSingleton,
            PairClassification.UnstitchMessyIndel,
            PairClassification.UnstitchMessyIndelSuspiciousRead,
            PairClassification.UnstitchForwardMessyIndel,
            PairClassification.UnstitchReverseMessyIndel
        };

    }
}