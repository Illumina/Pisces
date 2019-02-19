using System;
using System.Collections.Generic;

namespace Pisces.Domain.Models.Alleles
{
    public class ChrCompare : IComparer<string>
    {
        
        //default order
        private readonly List<string> _forcedChrOrder = new List<string>() { "chr1", "chr2", "chr3", "chr4", "chr5, chr6", "chr7", "chr8", "chr9", "chr10",
                                                            "chr11", "chr12", "chr13", "chr14", "chr15, chr16", "chr17", "chr18", "chr19", "chr20",
                                                            "chr21", "chr22", "chrX", "chrY","chrM" };
   
        public ChrCompare(List<string> forcedChrOrder = null)
        {
            if (forcedChrOrder != null)
            {
                _forcedChrOrder = forcedChrOrder;
            }
        }

        public int Compare(string x, string y)
        {
            if (x == y)
                return 0;

            //check to see if either of these are in the forced-order list, and if so, use that.
            var chrXIndex = _forcedChrOrder.IndexOf(x);
            var chrYIndex = _forcedChrOrder.IndexOf(y);

            if ((chrXIndex > -1) && (chrYIndex > -1))
            {
                if (chrXIndex < chrYIndex)
                    return -1;
                else
                    return 1;
            }
            
            return String.Compare(x, y);  //we dont want cultural or OS-dependant default sort order.
        }

        public static List<string> GetChrListFromVcfHeader(List<string> vcfHeaderStrings)
        {
            List<string> foundContigs = new List<string>() { };

            foreach (var line in vcfHeaderStrings)
            {
                if (line.Contains("##contig=<ID="))
                {
                    var trimmedPrefix = line.Split("ID=");
                    var trimmedSuffix = trimmedPrefix[1].Split(",");

                    if (!foundContigs.Contains(trimmedSuffix[0]))
                        foundContigs.Add(trimmedSuffix[0]);
                }
            }

            return foundContigs;
        }

    }
}