using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SequencingFiles;
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

            var variants = reader.GetVariants();
            foreach (var variant in variants)
            {
                var candidate = Map(variant);
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

        private static CandidateAllele Map(VcfVariant vcfVariant)
        {
            var alternateAllele = vcfVariant.VariantAlleles[0];
            var type = AlleleCategory.Unsupported;

            if (!String.IsNullOrEmpty(vcfVariant.ReferenceAllele)
                && !String.IsNullOrEmpty(alternateAllele))
            {
                if (vcfVariant.ReferenceAllele == alternateAllele)
                    type = AlleleCategory.Reference;

                if (vcfVariant.ReferenceAllele.Length == alternateAllele.Length)
                {
                    type = alternateAllele.Length == 1 ? AlleleCategory.Snv : AlleleCategory.Mnv;
                }
                else
                {
                    if (vcfVariant.ReferenceAllele.Length == 1)
                        type = AlleleCategory.Insertion;
                    else if (alternateAllele.Length == 1)
                        type = AlleleCategory.Deletion;
                }
            }

            return new CandidateAllele(vcfVariant.ReferenceName, vcfVariant.ReferencePosition,
                vcfVariant.ReferenceAllele, alternateAllele, type);
        }

        public static int OrderVariants(VcfVariant a, VcfVariant b, bool mFirst)
        {
            //return -1 if A comes first

            if ((a == null) && (b == null))
            {
                throw new ApplicationException("Backlog is empty. Cannot order variants.");
            }

            if (a == null)
                return 1;
            if (b == null)
                return -1;



            if (a.ReferenceName != b.ReferenceName)
            {
                if (!(a.ReferenceName.Contains("chr")) || !(b.ReferenceName.Contains("chr")))
                    throw new ApplicationException("Chromosome name in .vcf not supported.  Cannot order variants.");

                try
                {
                    int chrNumA;
                    int chrNumB;

                    var aisInt = Int32.TryParse(a.ReferenceName.Replace("chr", ""), out chrNumA);
                    var bIsInt = Int32.TryParse(b.ReferenceName.Replace("chr", ""), out chrNumB);
                    var aIsChrM = a.ReferenceName.ToLower() == "chrm";
                    var bIsChrM = b.ReferenceName.ToLower() == "chrm";

                    //for simple chr[1,2,3...] numbered, just order numerically 
                    if (aisInt && bIsInt)
                    {
                        if (chrNumA < chrNumB) return -1;
                        if (chrNumA > chrNumB) return 1;
                    }

                    if (mFirst)
                    {
                        if (aIsChrM && bIsChrM) return 0; //equal
                        if (aIsChrM) return -1; //A goes first
                        if (bIsChrM) return 1;  //B goes first
                    }

                    //order chr1 before chrX,Y,M
                    if (aisInt && !bIsInt) return -1; //A goes first
                    if (!aisInt && bIsInt) return 1;  //B goes first

                    //these chrs are alphanumeric.  Order should be X,Y,M .
                    //And lets try not to crash on alien dna like chrW and chrFromMars
                    if (!aisInt)
                    {
                        //we only go down this path if M is not first.
                        if (aIsChrM && bIsChrM) return 0; //equal
                        if (aIsChrM) return 1; //B goes first
                        if (bIsChrM) return -1;  //A goes first

                        //order remaining stuff {x,y,y2,HEDGeHOG } alphanumerically.
                        return (String.Compare(a.ReferenceName, b.ReferenceName));
                    }
                }
                catch
                {
                    throw new ApplicationException(String.Format("Cannot order variants with chr names {0} and {1}.", a.ReferenceName, b.ReferenceName));
                }
            }

            if (a.ReferencePosition < b.ReferencePosition) return -1;
            return a.ReferencePosition > b.ReferencePosition ? 1 : 0;
        }

        public static VcfVariant DeepCopy(VcfVariant originalVariant)
        {
            var newVariant = new VcfVariant
            {
                ReferenceName = originalVariant.ReferenceName,
                ReferencePosition = originalVariant.ReferencePosition,
                ReferenceAllele = originalVariant.ReferenceAllele,
                VariantAlleles = new[] { originalVariant.VariantAlleles[0] },
                Filters = originalVariant.Filters,
                Identifier = originalVariant.Identifier,
                GenotypeTagOrder = new string[originalVariant.GenotypeTagOrder.Length],
                InfoTagOrder = new string[originalVariant.InfoTagOrder.Length],
                Genotypes = new List<Dictionary<string, string>> { new Dictionary<string, string>() },
                Quality = originalVariant.Quality
            };

            for (var i = 0; i < originalVariant.GenotypeTagOrder.Length; i++)
            {
                var tag = originalVariant.GenotypeTagOrder[i];
                newVariant.GenotypeTagOrder[i] = tag;
                newVariant.Genotypes[0].Add(tag, originalVariant.Genotypes[0][tag]);
            }


            newVariant.InfoFields = new Dictionary<string, string>();
            for (var i = 0; i < originalVariant.InfoTagOrder.Length; i++)
            {
                var tag = originalVariant.InfoTagOrder[i];
                newVariant.InfoTagOrder[i] = tag;
                newVariant.InfoFields.Add(tag, originalVariant.InfoFields[tag]);
            }

            return newVariant;
        }
    }


}
