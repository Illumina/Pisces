using System.IO;
using System.Collections.Generic;

namespace VariantQualityRecalibration
{
    public class VariantListReader
    {

        
        public static Dictionary<string, List<int>> ReadVariantListFile(string file)
        {
            //lines in file look like this:
            //chr10   7577500   C   G
            //chr10   7577501   C   G
            //...

            var variantLookup = new Dictionary<string, List<int>>();

            using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
            {
                string line;

                while (true)
                {
                    line = sr.ReadLine();

                    if (line == "")
                        continue;

                    if (line == null)
                        break;

                    string[] splat = line.Split();

                    if (splat.Length < 2)
                        continue;

                    var chr = splat[0];
                    var pos = int.Parse(splat[1]);

                    if (!variantLookup.ContainsKey(chr))
                        variantLookup.Add(chr, new List<int>() { pos});
                    else
                        variantLookup[chr].Add(pos);
                }
            }

            return variantLookup;
        }

    }
}
