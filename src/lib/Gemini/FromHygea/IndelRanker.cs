using System;
using System.Collections.Generic;
using Gemini.Interfaces;
using Gemini.Models;

namespace Gemini.FromHygea
{
    public class IndelRanker : IIndelRanker, IComparer<PreIndel>
    {
        public void Rank(List<PreIndel> candidateIndels)
        {
            candidateIndels.Sort(this);
        }

        public int Compare(PreIndel first, PreIndel second)
        {
            // return known one first
            if (first.IsKnown && !second.IsKnown) return -1;
            if (!first.IsKnown && second.IsKnown) return 1;

            // try the larger one first
            if (first.Length != second.Length)
                return first.Length.CompareTo(second.Length) * -1;

            // if both the same size, go with more frequency
            if (Math.Abs(first.Frequency - second.Frequency) > 0.0001)
                return first.Frequency.CompareTo(second.Frequency) * -1;

            // if both the same frequency, just go with left one
            return first.ReferencePosition.CompareTo(second.ReferencePosition);
        }
    }
}