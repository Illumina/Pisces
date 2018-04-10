
using System.IO;
using System.Collections.Generic;
using Pisces.Domain.Models;


namespace Pisces.Domain.Utility
{
    public class IntervalFileToRegion
    {
        private const char _intervalFileDelimiter = '\t';
        public static void ParseIntervalFile(string intervalFilePath, Dictionary<string, List<Region>> regionsByChr)
        {
            using (var reader = new StreamReader(new FileStream(intervalFilePath, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var bits = line.Split(_intervalFileDelimiter);
                    if (bits.Length < 3 || bits[0].Length == 0 || bits[0][0] == '@')
                        continue; //header or invalid line

                    var chromosomeName = bits[0];
                    if (!regionsByChr.ContainsKey(chromosomeName))
                        regionsByChr[chromosomeName] = new List<Region>();

                    regionsByChr[chromosomeName].Add(new Region(int.Parse(bits[1]), int.Parse(bits[2])));
                }
            }

            // sort regions
            foreach (var chrRegions in regionsByChr.Values)
                chrRegions.Sort((r1, r2) => r1.StartPosition.CompareTo(r2.StartPosition));
        }
      
    }
}
