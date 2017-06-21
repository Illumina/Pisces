using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

            var calledVariants = Convert(reader.GetVariants());
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

        public static List<VcfVariant> UnpackVariants(IEnumerable<VcfVariant> tempOriginalVariants)
        {
            var originalVariants = new List<VcfVariant>();

            foreach (var variant in tempOriginalVariants)
                originalVariants.AddRange(UnpackVariant(variant));

            return originalVariants;
        }

        public static List<VcfVariant> UnpackVariant(VcfVariant v)
        {
            var variants = new List<VcfVariant> { };

            if (v.VariantAlleles.Count() == 1)
                return new List<VcfVariant> { v };

            var totalDP = 0;
            var vfStringLength = 0;

            if ((v.InfoFields != null) && ( v.InfoFields.ContainsKey("DP")))
            {

                var totalDPstring = v.InfoFields["DP"];
                bool worked = int.TryParse(totalDPstring, out totalDP);
            }
            var refCountEstimate = totalDP;

            List<int> alleleDepths = new List<int>();
            for (int i = 0; i < v.VariantAlleles.Length; i++)
            {
                var varAllele = v.VariantAlleles[i];

                if (varAllele == "*")
                    continue;

                var alleleDP = 0;

                if ((v.Genotypes != null) &&
                        (v.Genotypes.Count > 0))
                {
                    if (v.Genotypes[0].ContainsKey("AD")
                        && v.Genotypes[0]["AD"].Split(',').Count() > i)
                    {

                        var alleleDPstring = v.Genotypes[0]["AD"].Split(',')[i];
                        bool worked = int.TryParse(alleleDPstring, out alleleDP);

                        if (worked)
                        {
                            alleleDepths.Add(alleleDP);
                            refCountEstimate -= alleleDP;
                        }
                        else
                            alleleDepths.Add(0);
                    }

                    if (v.Genotypes[0].ContainsKey("VF")
                       && v.Genotypes[0]["VF"].Split(',').Count() > i)
                    {

                        var alleleVFstring = v.Genotypes[0]["VF"].Split(',')[i];
                        vfStringLength = Math.Max(vfStringLength, alleleVFstring.Length);
                    }
                }
                var newVariant = new VcfVariant()
                {
                    ReferenceName = v.ReferenceName,
                    ReferencePosition = v.ReferencePosition,
                    ReferenceAllele = v.ReferenceAllele,
                    VariantAlleles = new string[] { v.VariantAlleles[i] },
                    Filters = v.Filters,
                    HasQuality = v.HasQuality,
                    Quality = v.Quality,
                    InfoTagOrder = v.InfoTagOrder,
                    GenotypeTagOrder = v.GenotypeTagOrder,
                    Genotypes = new List<Dictionary<string, string>>() { new Dictionary<string, string>() },
                    InfoFields = new Dictionary<string, string>()
                };

                if (v.InfoFields != null)
                    foreach (var key in v.InfoFields.Keys)
                        newVariant.InfoFields.Add(key, v.InfoFields[key]);

                if ((v.Genotypes != null) && (v.Genotypes.Count > 0))
                {
                    foreach (var key in v.Genotypes[0].Keys)
                        newVariant.Genotypes[0].Add(key, v.Genotypes[0][key]);
                }


                variants.Add(newVariant);
            }

            string vfFormat = "0.";
            if (vfStringLength > 2)
            {
                for (int i = 2; i < vfStringLength; i++)
                    vfFormat += "0";
            }

            for (int i = 0; i < variants.Count; i++)
            {
                if ((v.Genotypes != null) && (v.Genotypes.Count > 0))
                {
                   
                    if (variants[i].Genotypes[0].ContainsKey("AD"))
                        variants[i].Genotypes[0]["AD"] = (Math.Max(0,refCountEstimate)) + "," + alleleDepths[i];

                    if (variants[i].Genotypes[0].ContainsKey("VF"))
                    {
                        if (totalDP <= 0)
                            variants[i].Genotypes[0]["VF"] = (0.0).ToString(vfFormat);
                        else
                        {
                            float vf = ((float)alleleDepths[i]) / (float)totalDP;
                            variants[i].Genotypes[0]["VF"] = vf.ToString(vfFormat);
                        }
                    }
                }
            }

            return variants;
        }

        public static IEnumerable<CalledAllele> Convert(IEnumerable<VcfVariant> vcfVariants)
        {
            var unpackedVariants = UnpackVariants(vcfVariants);
            var alleles = new List<CalledAllele>();

            foreach (var unpackedVar in unpackedVariants)
            {
               alleles.Add(ConvertUnpackedVariant(unpackedVar));
            }

            return alleles;
        }
        public static CalledAllele ConvertUnpackedVariant(VcfVariant v)
        {
            if (v == null)
                return null;

            if (v.VariantAlleles.Count() > 1)
                throw new ArgumentException("This method does not handle crushed vcf format. Use Convert(IEnumerable<VcfVariant> vcfVariants)");

            
            var genotypeQscore = 0;
            var referenceSupport = 0;
            var altSupport = 0;
            var genotypeString = "";
            var isRef = ((v.VariantAlleles.Count() == 1) && v.VariantAlleles[0] == ".");
            var totalCoverage = Int32.Parse(v.InfoFields["DP"]);
            var variantQuality = v.Quality;
            var numAlts = 1;
            var noiseLevel = 1;
            var fractionNocalls = 0f;
            var strandBiasInGATKScaleCoords = -100f;

            if (v.Genotypes.Count > 0)
            {
                if (v.Genotypes[0].ContainsKey("GQ"))
                    genotypeQscore = Int32.Parse(v.Genotypes[0]["GQ"]);
                else if (v.Genotypes[0].ContainsKey("GQX"))
                    genotypeQscore = Int32.Parse(v.Genotypes[0]["GQX"]);
                genotypeString = v.Genotypes[0]["GT"];

                if (v.Genotypes[0].ContainsKey("NL"))
                    noiseLevel = Int32.Parse(v.Genotypes[0]["NL"]);

                if (v.Genotypes[0].ContainsKey("NC"))
                    fractionNocalls = float.Parse(v.Genotypes[0]["NC"]);

                if (v.Genotypes[0].ContainsKey("SB"))
                    strandBiasInGATKScaleCoords = float.Parse(v.Genotypes[0]["SB"]);

                var ADstring = new string[]{ "0","0"};

                if (v.Genotypes[0].ContainsKey("AD"))
                    ADstring = v.Genotypes[0]["AD"].Split(',');

                var VFstring = new string[] { "0", "0" };
                if (v.Genotypes[0].ContainsKey("VF"))
                    VFstring = v.Genotypes[0]["VF"].Split(',');

                referenceSupport = int.Parse(ADstring[0]);
                altSupport = isRef ? 0 : int.Parse(ADstring[1]);

                if (isRef)
                    numAlts = 0;
                else
                    numAlts = VFstring.Length; //note this, method should never get a value here >1. yet.
            }

            var strandBiasResults = new BiasResults();
            strandBiasResults.GATKBiasScore = strandBiasInGATKScaleCoords;

            var allele = new CalledAllele()
            {
                Chromosome = v.ReferenceName,
                ReferencePosition = v.ReferencePosition,
                ReferenceAllele = v.ReferenceAllele,
                AlternateAllele = v.VariantAlleles[0],
                TotalCoverage = totalCoverage,
                AlleleSupport = isRef ? referenceSupport : altSupport,
                ReferenceSupport = referenceSupport,
                VariantQscore = (int)variantQuality,
                GenotypeQscore = genotypeQscore,
                Genotype = MapGTString(genotypeString, numAlts),
                Filters = MapFilterString(v.Filters),
                NoiseLevelApplied = noiseLevel,
                StrandBiasResults = strandBiasResults,
            };

            allele.SetType();
            allele.ForceFractionNoCalls(fractionNocalls);
            return allele;
        }

        public static bool IsRMxN(string filter)
        {
            int m = -1;
            int n = -1;

            return IsRMxN(filter, out m, out n);
        }
        /// <summary>
        /// Limititations:
        /// "r-1x-9" // in current implementation, will return true
        /// "r5x"+((long)int.MaxValue + 1) // in current implementation, will return false
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="m"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static bool IsRMxN(string filter, out int m, out int n)
        {
            filter = filter.ToLower();
            m = -1;
            n = -1;
            bool worked = false;

            if (filter[0] == 'r')
            {
                var MN = filter.Substring(1, filter.Length - 1).Split('x');

                if (MN.Length != 2)
                    return false;

               
                bool gotM = int.TryParse(MN[0], out m);
                bool gotN = int.TryParse(MN[1], out n);

                if (gotM && gotN)
                    worked = true;

            }

            if (!worked)
            {
                m = -1;
                n = -1;
            }

            return worked;
        }


        public static List<FilterType> MapFilterString(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
                return new List<FilterType>();

            filterString = filterString.ToLower();
            var filterStrings = filterString.Split(VcfFormatter.FilterSeparator);
            var filterAsEnum = new List<FilterType>();

            if (filterString == VcfFormatter.PassFilter.ToLower())
                return filterAsEnum;

            foreach (var filter in filterStrings)
            {
                int thresholdValue = LookForThresholdValue(1, filter);

                if (filter.Contains("lowq") || (filter[0] == 'q') && (thresholdValue > 0))
                    filterAsEnum.Add(FilterType.LowVariantQscore);

                else if (filter == "sb")
                    filterAsEnum.Add(FilterType.StrandBias);

                else if ((filter == "lowdp") || (filter == "lowdepth"))
                    filterAsEnum.Add(FilterType.LowDepth);

                else if ((filter == "lowvariantfreq") || (filter == "lowfreq"))
                    filterAsEnum.Add(FilterType.LowVariantFrequency);

                else if ((filter == "lowgq") || (filter.Substring(0, 2) == "gq"))
                    filterAsEnum.Add(FilterType.LowGenotypeQuality);

                else if ((filter[0] == 'r') && (thresholdValue > 0))
                    filterAsEnum.Add(FilterType.IndelRepeatLength);

                else if (IsRMxN(filter))
                    filterAsEnum.Add(FilterType.RMxN);

                else if (filter == "multiallelicsite")
                    filterAsEnum.Add(FilterType.MultiAllelicSite);

            }

            return filterAsEnum;
        }

        public static int LookForThresholdValue(int startPostition, string filter)
        {
            int intValue = -1;
            bool isInt = int.TryParse(filter.Substring(startPostition, filter.Length - startPostition), out intValue);
            return intValue;
        }

        
        public static Dictionary<FilterType, string> GetFilterStringsByType(List<string> vcfHeaderLines)
        {
            var filterStringsForHeader = new Dictionary<FilterType, string>();
            var filterLines = vcfHeaderLines.FindAll(x => x.StartsWith("##FILTER"));

            foreach (var x in filterLines)
            {
                var filterString = (x.Split(',')[0].Replace("##FILTER=<ID=", ""));
                var filterTypesInString = MapFilterString(filterString);  //should have length 0 or 1.

                if (filterTypesInString.Count() == 1)//otherwise somethign really odd happened...
                {
                    if (!filterStringsForHeader.ContainsKey(filterTypesInString[0]))
                        filterStringsForHeader.Add(filterTypesInString[0], x);
                }
            }

            return filterStringsForHeader;
        }


        public static Genotype MapGTString(string gtString, int numAlts)
        {

            switch (gtString)
            {
                case "1/1":
                    return Genotype.HomozygousAlt;
                case "0/0":
                    return Genotype.HomozygousRef;
                case "1/.":
                    return Genotype.AltAndNoCall;
                case "0/.":
                    return Genotype.RefAndNoCall;
                case "0/1":
                    return Genotype.HeterozygousAltRef;
                case "1/2":
                    return Genotype.HeterozygousAlt1Alt2;
                case "./.":
                    if (numAlts == 0)
                        return Genotype.RefLikeNoCall;
                    else if (numAlts == 1)
                        return Genotype.AltLikeNoCall;
                    else
                        return Genotype.Alt12LikeNoCall;
				case ".":
					return Genotype.HemizygousNoCall;
				case "0":
					return Genotype.HemizygousRef;
				case "1":
					return Genotype.HemizygousAlt;
				case "*/*":
					return Genotype.Others;
                default:
                    return Genotype.RefLikeNoCall;
            }
        }

        public static bool CompareSubstring(string str1, string str2, int startPos2)
        {
            for (int i = 0; i < str1.Length; ++i)
            {
                if (str1[i] != str2[startPos2 + i])
                {
                    return false;
                }
            }

            return true;
        }


    }


}
