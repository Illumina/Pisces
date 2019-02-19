using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemini.Interfaces;
using Gemini.Types;

namespace Gemini.Logic
{
    public class CategorizedBamAndIndelEvidenceSourceFromIntermediateDir : ICategorizedBamAndIndelEvidenceSource
    {
        private readonly string _intermediateDir;
        private readonly string _indelsCsvName;

        public CategorizedBamAndIndelEvidenceSourceFromIntermediateDir(string intermediateDir, string indelsCsvName)
        {
            _intermediateDir = intermediateDir;
            _indelsCsvName = indelsCsvName;
        }
        public Dictionary<string, Dictionary<PairClassification, List<string>>> GetCategorizedAlignments()
        {
            return GetCategorizedBamMapping(_intermediateDir);
        }

        private static Dictionary<string, Dictionary<PairClassification, List<string>>> GetCategorizedBamMapping(string indermediateDirValue)
        {
            // TODO add the other chromosomes besides those being realigned so we can successfully merge at end <-- GB check if this comment is still relevant

            var categorizedAlignments = new Dictionary<string, Dictionary<PairClassification, List<string>>>();

            var bams = Directory.GetFiles(indermediateDirValue, "*bam*chr*")
                .Where(x => !x.EndsWith("realigned") && !x.EndsWith("merged.bam")).ToList();
            foreach (var bam in bams)
            {
                var splitBam = bam.Split("_");
                var bamChrom = splitBam[splitBam.Length - 2];
                var bamType = splitBam[splitBam.Length - 3];
                PairClassification classification;
                var canParse = PairClassification.TryParse(bamType, out classification);
                if (!canParse)
                {
                    throw new Exception("Bam isn't what I expected: " + bam);
                }

                if (!categorizedAlignments.ContainsKey(bamChrom))
                {
                    categorizedAlignments.Add(bamChrom, new Dictionary<PairClassification, List<string>>());
                }

                if (!categorizedAlignments[bamChrom].ContainsKey(classification))
                {
                    categorizedAlignments[bamChrom].Add(classification, new List<string>());
                }

                categorizedAlignments[bamChrom][classification].Add(bam);
            }

            return categorizedAlignments;
        }

        public Dictionary<string, int[]> GetIndelStringLookup()
        {
            // TODO this is kind of silly but leaving as-is for now as I'm just moving code around
            var indelStringLookup = new Dictionary<string, int[]>();
            GetIndelStringLookupFromFile(_intermediateDir, _indelsCsvName, indelStringLookup);
            return indelStringLookup;
        }

        private static void GetIndelStringLookupFromFile(string intermedPath, string indelsCsvName,
            Dictionary<string, int[]> indelStringLookup)
        {
            using (var reader = new StreamReader(new FileStream(Path.Combine(intermedPath, indelsCsvName), FileMode.Open)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var split = line.Split(",");
                    indelStringLookup[split[0]] = new int[9];
                    indelStringLookup[split[0]][0] = int.Parse(split[1]);
                    indelStringLookup[split[0]][1] = int.Parse(split[2]);
                    indelStringLookup[split[0]][2] = int.Parse(split[3]);
                    indelStringLookup[split[0]][3] = int.Parse(split[4]);
                    indelStringLookup[split[0]][4] = int.Parse(split[5]);
                    indelStringLookup[split[0]][5] = int.Parse(split[6]);
                    indelStringLookup[split[0]][6] = int.Parse(split[7]);
                    indelStringLookup[split[0]][7] = int.Parse(split[8]);
                    indelStringLookup[split[0]][8] = int.Parse(split[9]);
                }
            }
        }

        public void CollectAndCategorize(IGeminiDataSourceFactory dataSourceFactory, IGeminiDataOutputFactory dataOutputFactory)
        {
            // TODO do some validation here maybe? or do the actual logic here instead of in those other methods. 
        }
    }
}