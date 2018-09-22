using System.Collections.Generic;
using Pisces.Domain.Options;
using Common.IO.Utility;
using CommandLine.NDesk.Options;

namespace CommandLine.Options
{
    public class BamFilterOptionsUtils
    {
        public static Dictionary<string, OptionSet> GetBamFilterParsingMethods(BamFilterParameters options)
        {
            var bamFilterOps = new OptionSet
            {
                {
                    "minbq|minbasecallquality=",
                    OptionTypes.INT +  " MinimumBaseCallQuality to use a base of the read",
                    value => options.MinimumBaseCallQuality = int.Parse(value)
                },
                {
                    "minmq|minmapquality=",
                    OptionTypes.INT  +  " MinimumMapQuality required to use a read",
                    value => options.MinimumMapQuality = int.Parse(value)
                },
                {
                    "filterduplicates|duplicatereadfilter=",
                    OptionTypes.BOOL  +  " To filter reads marked as duplicates",
                    value => options.RemoveDuplicates = bool.Parse(value)
                },
                {
                    "pp|onlyuseproperpairs=",
                    OptionTypes.BOOL +  " Only use proper pairs, 'true' or 'false",
                    value => options.OnlyUseProperPairs = bool.Parse(value)
                }

            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                    {OptionSetNames.BamFiltering,bamFilterOps },
            };




            return optionDict;
        }

        public static void AddBamFilterArgumentParsing(Dictionary<string, OptionSet> parsingMethods, BamFilterParameters options)
        {
            var bamfilterOptionDict = GetBamFilterParsingMethods(options);


            foreach (var key in bamfilterOptionDict.Keys)
            {
                foreach (var optSet in bamfilterOptionDict[key])
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
