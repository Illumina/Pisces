//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using RealignIndels.Interfaces;
//using RealignIndels.Models;

//namespace RealignIndels.Logic.TargetCalling
//{
//    public class IndelRanker : IIndelRanker, IComparer<CandidateIndel>
//    {
//        public void Rank(List<CandidateIndel> candidateIndels)
//        {
//            candidateIndels.Sort(this);
//        }

//        public int Compare(CandidateIndel first, CandidateIndel second)
//        {
//            // return known one first
//            if (first.IsKnown && !second.IsKnown) return -1;
//            if (!first.IsKnown && second.IsKnown) return 1;

//            // try the one with higher frequency first
//            if (Math.Abs(first.Frequency - second.Frequency) > 0.0001)
//                return first.Frequency.CompareTo(second.Frequency) * -1;

//            // if both the same frequency, go with the larger one
//            if (first.Length != second.Length)
//                return first.Length.CompareTo(second.Length) * -1;

//            // if both the same size, just go with left one
//            return first.ReferencePosition.CompareTo(second.ReferencePosition);
//        }
//    }
//}
