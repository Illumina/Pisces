using System;
using System.Collections.Generic;
using System.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace TestUtilities
{
    public class TestVariantsLoader
    {
        public static List<BaseCalledAllele> LoadCalledVariantsFile(string filepath)
        {
            var variants = new List<BaseCalledAllele>();
            var columns = new string[0];
            
            using (var reader = new StreamReader(filepath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var tokens = line.Split('\t');

                    if (line.StartsWith("Chromosome"))
                        columns = tokens;
                    else
                    {
                        var variant = new CalledVariant(AlleleCategory.Snv); // note doesn't matter what the call type is, vcf writer doesnt care
                        for(var i = 0; i < columns.Length; i ++)
                        {
                            var column = columns[i];
                            var dataValue = tokens[i];

                                var type = typeof(BaseCalledAllele);
                                var property = type.GetProperty(column);

                            switch (column)
                            {
                                case "Chromosome":
                                case "Reference":
                                case "Alternate":
                                    property.SetValue(variant, dataValue);
                                    break;
                                case "Coordinate":
                                case "Qscore":
                                case "TotalCoverage":
                                case "AlleleSupport":
                                    property.SetValue(variant, Int32.Parse(dataValue));
                                    break;
                                case "FractionNoCalls":
                                    property.SetValue(variant, float.Parse(dataValue));
                                    break;
                                case "StrandBiasScore":
                                    variant.StrandBiasResults.GATKBiasScore = float.Parse(dataValue);
                                    break;
                                case "Filters":
                                    var filterStrings = dataValue.Split(',');
                                    foreach (var filter in filterStrings)
                                    {
                                        if (!string.IsNullOrEmpty(filter))
                                        {
                                            var filterEnum = (FilterType) Enum.Parse(typeof (FilterType), filter, true);
                                            variant.Filters.Add(filterEnum);
                                        }
                                    }
                                    break;
                                case "Genotype":
                                    variant.Genotype = (Genotype) Enum.Parse(typeof (Genotype), dataValue, true);
                                    break;
                            }
                        }

                        if (variant.Genotype == Genotype.HomozygousRef || variant.Genotype == Genotype.RefLikeNoCall)
                        {
                            variants.Add(Map(variant));
                        }
                        else
                            variants.Add(variant);
                    }
                }
            }

            return variants;
        }

        public static List<BaseCalledAllele> LoadCalledVariantsArray(string[] candidates)
        {
            var variants = new List<BaseCalledAllele>();
            var columns = new string[0];
            foreach (var line in candidates) {
            {
                var tokens = line.Split('\t');

                if (line.StartsWith("Chromosome"))
                    columns = tokens;
                else
                {
                    var variant = new CalledVariant(AlleleCategory.Snv); // note doesn't matter what the call type is, vcf writer doesnt care
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var column = columns[i];
                        var dataValue = tokens[i];

                        var type = typeof(BaseCalledAllele);
                        var property = type.GetProperty(column);

                        switch (column)
                        {
                            case "Chromosome":
                            case "Reference":
                            case "Alternate":
                                property.SetValue(variant, dataValue);
                                break;
                            case "Coordinate":
                            case "Qscore":
                            case "TotalCoverage":
                            case "AlleleSupport":
                                property.SetValue(variant, Int32.Parse(dataValue));
                                break;
                            case "FractionNoCalls":
                                property.SetValue(variant, float.Parse(dataValue));
                                break;
                            case "StrandBiasScore":
                                variant.StrandBiasResults.GATKBiasScore = float.Parse(dataValue);
                                break;
                            case "Filters":
                                var filterStrings = dataValue.Split(',');
                                foreach (var filter in filterStrings)
                                {
                                    if (!string.IsNullOrEmpty(filter))
                                    {
                                        var filterEnum = (FilterType)Enum.Parse(typeof(FilterType), filter, true);
                                        variant.Filters.Add(filterEnum);
                                    }
                                }
                                break;
                            case "Genotype":
                                variant.Genotype = (Genotype)Enum.Parse(typeof(Genotype), dataValue, true);
                                break;
                        }
                    }

                    if (variant.Genotype == Genotype.HomozygousRef || variant.Genotype == Genotype.RefLikeNoCall)
                    {
                        variants.Add(Map(variant));
                    }
                    else
                        variants.Add(variant);
                }
            }
        }

            return variants;
        }


        private static CalledReference Map(BaseCalledAllele variant)
        {
            return new CalledReference()
            {
                Chromosome = variant.Chromosome,
                Coordinate = variant.Coordinate,
                Alternate = variant.Alternate,
                Reference = variant.Reference,
                StrandBiasResults = variant.StrandBiasResults,
                Filters = variant.Filters,
                Genotype = variant.Genotype,
                AlleleSupport = variant.AlleleSupport,
                FractionNoCalls = variant.FractionNoCalls,
                VariantQscore = variant.VariantQscore,
                TotalCoverage = variant.TotalCoverage
            };
        }
    }
}
