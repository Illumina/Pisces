using System;
using System.Collections.Generic;

namespace Pisces.Domain.Models
{
    public class BiasResultsAcrossAmplicons
    {
        public bool BiasDetected;
        public string AmpliconWithCandidateArtifact;
        public Dictionary<string, AmpliconBiasResult> ResultsByAmpliconName;
    }

    public class AmpliconBiasResult
    {
        public double Frequency;
        public string Name;
        public bool BiasDetected;
        public int ConfidenceQScore;
        public double ChanceItsReal;
        public double Coverage;
        public double ObservedSupport;
        public double ExpectedSupport;
    }

    /// <summary>
    /// just because this is more readable than a tuple
    /// </summary>
    public struct AmpliconCountsIndexes
    {
        public int IndexForAmplicon;
        public int NextOpenSlot;
    }

    public struct AmpliconCounts
    {
        public int[] CountsForAmplicon;
        public string[] AmpliconNames;
        
        /// <summary>
        /// Given an amplicon name, this utilitiy method looks in the AmpliconNames array, and returns (a) the
        /// index where the amplicon is found in the array, and (b) is the first empty available index. if a is found, the algorithm returns with only a.
        /// If values for a or b do not exist, -1 is returned in the relevant spot.
        /// For easy of use (a,b) are named valuesin the AmpliconCountsIndexes struct instread of a tuple.
        /// </summary>
        /// <param name="ampliconName"></param>
        /// <param name="namesArrayAtPos"></param>
        /// <returns></returns>
        public static AmpliconCountsIndexes GetAmpliconNameIndex(string ampliconName, string[] namesArrayAtPos)
        {
            int indexOfFirstEmptySpot = -1;

            for (int i = 0; i < namesArrayAtPos.Length; i++)
            {
                var nameAtIndex = namesArrayAtPos[i];
                if (nameAtIndex == ampliconName)
                {
                    //found it!
                    return new AmpliconCountsIndexes() { IndexForAmplicon = i, NextOpenSlot = -1 };
                }
                if ((nameAtIndex == null) && (indexOfFirstEmptySpot == -1))
                    indexOfFirstEmptySpot = i;

            }

            return new AmpliconCountsIndexes() { IndexForAmplicon = -1, NextOpenSlot = indexOfFirstEmptySpot };
        }

        public AmpliconCountsIndexes GetAmpliconNameIndex(string ampliconName)
        {
            return GetAmpliconNameIndex(ampliconName, AmpliconNames);
        }

        public int GetCountsForAmplicon(string ampliconName)
        {
            int index = GetAmpliconNameIndex(ampliconName, AmpliconNames).IndexForAmplicon;
            if (index > -1)
                return CountsForAmplicon[index];
            else
                return 0;
        }

        
        public static AmpliconCounts GetEmptyAmpliconCounts()
        {
            var names = new string[Constants.MaxNumOverlappingAmplicons];
            var counts = new int[Constants.MaxNumOverlappingAmplicons];
            return new AmpliconCounts() { AmpliconNames = names, CountsForAmplicon = counts };
        }

        public AmpliconCounts Copy()
        {

            if (AmpliconNames == null)
                return new AmpliconCounts();

            var names = new string[Constants.MaxNumOverlappingAmplicons];
            var counts = new int[Constants.MaxNumOverlappingAmplicons];

            Array.Copy(AmpliconNames, names, AmpliconNames.Length);
            Array.Copy(CountsForAmplicon, counts, CountsForAmplicon.Length);

            return new AmpliconCounts() { AmpliconNames = names, CountsForAmplicon = counts };
        }
    }
}