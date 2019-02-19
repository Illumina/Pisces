using System;
using System.Collections.Generic;
using Common.IO.Utility;
using Gemini.Models;
using Gemini.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.IO;

namespace Gemini.CandidateIndelSelection
{
    public interface IHashableIndelSource
    {
        List<HashableIndel> GetFinalIndelsForChromosome(string chromosome, List<PreIndel> indelsForChrom);
    }

    public class HashableIndelSource : IHashableIndelSource
    {
        private readonly string _genomePath;

        public HashableIndelSource(string genomePath)
        {
            _genomePath = genomePath;
        }

        /// <summary>
        /// Given a list of raw (non-genome-contextualized) indels to realign around, returns a list of hashable, contextualized indels.
        /// </summary>
        /// <param name="chrom"></param>
        /// <param name="genomePath"></param>
        /// <param name="indelsForChrom"></param>
        /// <returns></returns>
        public List<HashableIndel> GetFinalIndelsForChromosome(string chrom, List<PreIndel> indelsForChrom)
        {
            Logger.WriteToLog($"Getting chromosome {chrom} from genome {_genomePath}");
            var genome = new Genome(_genomePath, new List<string> { chrom });
            var chrReference = genome.GetChrReference(chrom);
            return GetFinalIndelsForChromosome(indelsForChrom, chrReference);
        }

        private static HashableIndel GetHashableIndel(GenomeSnippet snippet, PreIndel preIndel, int contextStart)
        {
            var actualReferenceAllele = snippet.Sequence.Substring(
                preIndel.ReferencePosition - 1 - contextStart, preIndel.ReferenceAllele.Length);

            var actualAltAllele =
                actualReferenceAllele.Length == 1
                    ? actualReferenceAllele +
                      preIndel.AlternateAllele.Substring(1)
                    : actualReferenceAllele[0].ToString();

            var indelIdentifier = new HashableIndel
            {
                Chromosome = preIndel.Chromosome,
                ReferencePosition = preIndel.ReferencePosition,
                ReferenceAllele = actualReferenceAllele,
                AlternateAllele = actualAltAllele,
                Type = actualReferenceAllele.Length > actualAltAllele.Length
                    ? AlleleCategory.Deletion
                    : AlleleCategory.Insertion,
                Length = Math.Abs(actualReferenceAllele.Length - actualAltAllele.Length),
                Score = preIndel.Score,
                InMulti = preIndel.InMulti,
                OtherIndel = preIndel.OtherIndel
            };
            return indelIdentifier;
        }


        private static List<HashableIndel> GetFinalIndelsForChromosome(List<PreIndel> indelsForChrom, ChrReference chrReference)
        {
            var indelsdict = new Dictionary<HashableIndel, List<PreIndel>>();
            var chromIndelContexts = new List<HashableIndel>();

            var snippet = new GenomeSnippet
            {
                Chromosome = chrReference.Name,
                Sequence = chrReference.Sequence,
                StartPosition = 0
            };
            var contextStart = 0;

            foreach (var candidateIndel in indelsForChrom)
            {
                var indelIdentifier = GetHashableIndel(snippet, candidateIndel, contextStart);

                if (!indelsdict.ContainsKey(indelIdentifier))
                {
                    indelsdict.Add(indelIdentifier, new List<PreIndel>());
                }

                indelsdict[indelIdentifier].Add(candidateIndel);

                chromIndelContexts.Add(indelIdentifier);
            }

            return chromIndelContexts;
        }

    }
}