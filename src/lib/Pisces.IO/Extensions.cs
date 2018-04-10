using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.IO
{
    public static class Extensions
    {
        public static Dictionary<string, List<CandidateAllele>> GetVariantsByChromosome(this VcfReader reader,
            bool variantsOnly = false, bool flagIsKnown = false, List<AlleleCategory> typeFilter = null, Func<CandidateAllele, bool> doSkipCandidate = null)
        {
            var lookup = new Dictionary<string, List<CandidateAllele>>();

            var calledVariants = VcfVariantUtilities.Convert(reader.GetVariants());
            foreach (var calledVariant in calledVariants)
            {
                var candidate = BackToCandiate(calledVariant);

                if (candidate.Type != AlleleCategory.Unsupported)
                {
                    if (variantsOnly && candidate.Type == AlleleCategory.Reference)
                        continue;

                    if (typeFilter != null && !typeFilter.Contains(candidate.Type))
                        continue;

                    if (doSkipCandidate != null && doSkipCandidate(candidate))
                        continue;

                    if (flagIsKnown)
                        candidate.IsKnown = true;

                    if (!lookup.ContainsKey(candidate.Chromosome))
                        lookup[candidate.Chromosome] = new List<CandidateAllele>();

                    lookup[candidate.Chromosome].Add(candidate);
                }
            }
            return lookup;
        }

        private static CandidateAllele BackToCandiate(CalledAllele allele)
        {
            return new CandidateAllele(allele.Chromosome, allele.ReferencePosition,
                allele.ReferenceAllele, allele.AlternateAllele, allele.Type);
        }
    }
}