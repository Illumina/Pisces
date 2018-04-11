using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Pisces.Domain.Options;
using CommandLine.IO;
using CommandLine.NDesk.Options;

namespace CommandLine.Options
{
    public class VcfWritingOptionsParser
    {
        public static Dictionary<string, OptionSet> GetVcfWritingOptionsMethods(VcfWritingParameters options)
        {
            var vcfWriterOps = new OptionSet
            {
                {
                    "gvcf=",
                    OptionTypes.BOOL +  " Output gVCF files, 'true' or 'false'",
                    value=>options.OutputGvcfFile = bool.Parse(value)
                },
                {
                    "crushvcf=",
                    OptionTypes.BOOL + " To crush vcf output to one line per loci",
                    value=>options.ForceCrush = bool.Parse(value)
                },
                {
                    "reportnocalls=",
                    OptionTypes.BOOL + " 'true' or 'false'. default, false",
                    value=>options.ReportNoCalls = bool.Parse(value)
                },
                {
                    "reportrccounts=",
                    OptionTypes.BOOL + " Report collapsed read count, When BAM files contain X1 and X2 tags, output read counts for duplex-stitched, duplex-nonstitched, simplex-stitched, and simplex-nonstitched.  'true' or 'false'. default, false",
                    value=>options.ReportRcCounts = bool.Parse(value)
                },
                {
                    "reporttscounts=",
                    OptionTypes.BOOL + " Report collapsed read count by different template strands, Conditional on ReportRcCounts, output read counts for duplex-stitched, duplex-nonstitched, simplex-forward-stitched, simplex-forward-nonstitched, simplex-reverse-stitched, simplex-reverse-nonstitched.  'true' or 'false'. default, false",
                    value=>options.ReportTsCounts = bool.Parse(value)
                }
            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                         {OptionSetNames.VcfWriting,vcfWriterOps },
            };




            return optionDict;
        }

        public static void AddVcfWritingArgumentParsing(Dictionary<string, OptionSet> parsingMethods, VcfWritingParameters options)
        {
            var vcfWritingOptionDict = GetVcfWritingOptionsMethods(options);


            foreach (var key in vcfWritingOptionDict.Keys)
            {
                foreach (var optSet in vcfWritingOptionDict[key])
                {
                    if (!parsingMethods.ContainsKey(key))
                    {
                        parsingMethods.Add(key, new OptionSet());
                    }

                    parsingMethods[key].Add(optSet);
                }
            }
        }

    }
}
