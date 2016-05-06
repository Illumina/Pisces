using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using System.Threading.Tasks;

namespace Pisces.IO
{
    public class VcfFormatter
    {

        public string GenotypeFormat = "GT";
        public string GenotypeQualityFormat = "GQ";
        public string AlleleDepthFormat = "AD";
        public string TotalDepthFormat = "DP";
        public string VariantFrequencyFormat = "VF";
        public string NoiseLevelFormat = "NL";
        public string StrandBiasFormat = "SB";
        public string FractionNoCallFormat = "NC";
        public string FrequencySigFigFormat;
        public const string PassFilter = "PASS";
        public const string FilterSeparator = ";";
        
        public string FrequencyFilterThresholdString
        {
            get { return _variantOutFileConfig.FrequencyFilterThreshold.HasValue ? _variantOutFileConfig.FrequencyFilterThreshold.Value.ToString(FrequencySigFigFormat) : string.Empty; }
        }

        private const double MinStrandBiasScore = -100;
        private const double MaxStrandBiasScore = 0;

        private VcfWriterConfig _variantOutFileConfig;

        public VcfFormatter() { }

        public VcfFormatter(VcfWriterConfig Config)
        {
            _variantOutFileConfig = Config;
            UpdateFrequencyFormat();
        }

        private void UpdateFrequencyFormat()
        {
            FrequencySigFigFormat = "0.";
            var minFreqString = _variantOutFileConfig.MinFrequencyThreshold.ToString();

            var freqSignificantDigits = GetNumSigDigits(minFreqString);

            if (_variantOutFileConfig.FrequencyFilterThreshold.HasValue)
                freqSignificantDigits = Math.Max(freqSignificantDigits, GetNumSigDigits(_variantOutFileConfig.FrequencyFilterThreshold.Value.ToString()));

            for (var i = 0; i < freqSignificantDigits; i++)
                FrequencySigFigFormat += "0";
        }

        private int GetNumSigDigits(string inputValue)
        {
            return inputValue.Contains("E")
                ? Math.Abs(int.Parse(inputValue.Split('E')[1]))
                : inputValue.Length - 1;
        }

        public string MapFilters(IEnumerable<BaseCalledAllele> variants)
        {
            var filters = MergeFilters(variants);
            var filterString = string.Join(FilterSeparator, filters.Select(MapFilter));
            return string.IsNullOrEmpty(filterString) ? PassFilter : filterString;
        }

        public string MapFilter(FilterType filter)
        {
            switch (filter)
            {
                case FilterType.LowVariantQscore:
                    if (!_variantOutFileConfig.VariantQualityFilterThreshold.HasValue)
                        throw new Exception("Variant has low qscore filter but threshold is not set.");
                    return "q" + _variantOutFileConfig.VariantQualityFilterThreshold.Value;
                case FilterType.StrandBias:
                    return StrandBiasFormat;
                case FilterType.LowDepth:
                    return "LowDP";
                case FilterType.LowVariantFrequency:
                    return "LowVariantFreq";
                case FilterType.LowGenotypeQuality:
                    return "LowGQ";
                case FilterType.IndelRepeatLength:
                    if (!_variantOutFileConfig.IndelRepeatFilterThreshold.HasValue)
                        throw new Exception("Variant has indel repeat filter but threshold is not set.");
                    return "R" + _variantOutFileConfig.IndelRepeatFilterThreshold;
                case FilterType.RMxN:
                    if (!_variantOutFileConfig.RMxNFilterMaxLengthRepeat.HasValue || !_variantOutFileConfig.RMxNFilterMinRepetitions.HasValue)
                        throw new Exception("Variant has RMxN filter but M or N value is not set.");
                    return "R" + _variantOutFileConfig.RMxNFilterMaxLengthRepeat + "x" + _variantOutFileConfig.RMxNFilterMinRepetitions;
                case FilterType.MultiAllelicSite:
                    return "MultiAllelicSite";
                default:
                    return "";
            }
        }

        public string MapGenotype(Genotype genotype)
        {
            switch (genotype)
            {
                case Genotype.HomozygousAlt:
                    return "1/1";
                case Genotype.HomozygousRef:
                    return "0/0";
                case Genotype.HeterozygousAltRef:
                    return "0/1";
                case Genotype.HeterozygousAlt1Alt2:
                    return "1/2";
                case Genotype.RefLikeNoCall:
                    return "./.";
                case Genotype.AltLikeNoCall:
                    return "./.";
                default:
                    return "./.";
            }
        }

        public string[] ConstructFormatAndSampleString(IEnumerable<BaseCalledAllele> variants, int totalDepth)
        {
            BaseCalledAllele firstVariant = variants.First();
            var gtQuality = MergeGenotypeQScores(variants);
            var gtString = MapGenotype(firstVariant.Genotype);
            var isReference = firstVariant is CalledReference;         
            var alleleCountString = GetAlleleCountString(variants, isReference);
            var frequencyString = GetFrequencyString(variants, isReference);

            var formatStringBuilder = new StringBuilder("GT:GQ:AD:DP:VF");
            var sampleStringBuilder = new StringBuilder(string.Format("{0}:{1}:{2}:{3}:{4}", gtString, gtQuality, alleleCountString, totalDepth, frequencyString));

            if (_variantOutFileConfig.ShouldOutputStrandBiasAndNoiseLevel)
            {
                var biasScoreString = (Math.Min(Math.Max(MinStrandBiasScore, firstVariant.StrandBiasResults.GATKBiasScore), MaxStrandBiasScore)).ToString("0.0000");

                formatStringBuilder.Append(":NL:SB");
                sampleStringBuilder.Append(string.Format(":{0}:{1}", _variantOutFileConfig.EstimatedBaseCallQuality, biasScoreString));
            }

            if (_variantOutFileConfig.ShouldOutputNoCallFraction)
            {
                var noCallFractionString = firstVariant.FractionNoCalls.ToString("0.0000");

                formatStringBuilder.Append(":NC");
                sampleStringBuilder.Append(string.Format(":{0}", noCallFractionString));
            }

            if (_variantOutFileConfig.ShouldOutputRcCounts)
            {
                formatStringBuilder.Append(":US");
                sampleStringBuilder.Append(string.Format(":{0},{1},{2},{3}",
                    firstVariant.ReadCollapsedCounts[(int)ReadCollapsedType.DuplexStitched],
                    firstVariant.ReadCollapsedCounts[(int)ReadCollapsedType.DuplexNonStitched],
                    firstVariant.ReadCollapsedCounts[(int)ReadCollapsedType.SimplexStitched],
                    firstVariant.ReadCollapsedCounts[(int)ReadCollapsedType.SimplexNonStitched]));
            }

            return new[]
            {
                formatStringBuilder.ToString(),
                sampleStringBuilder.ToString()
            };
        }

        private string GetFrequencyString(IEnumerable<BaseCalledAllele> variants, bool isReference)
        {
            BaseCalledAllele firstVariant = variants.First();

            if (isReference)
                return (1 - firstVariant.Frequency).ToString(FrequencySigFigFormat);
            else
            {
                Genotype gt = ((CalledVariant)firstVariant).Genotype;
                if ((gt == Genotype.HeterozygousAlt1Alt2) || (gt == Genotype.Alt12LikeNoCall))
                {
                    List<string> altAllelesFrequencies = (variants.Select(v => v.Frequency.ToString(FrequencySigFigFormat))).ToList();
                    return (string.Join(",", altAllelesFrequencies));
                }
                else
                    return (firstVariant.Frequency).ToString(FrequencySigFigFormat);
            }
        }

        //this is not as obvious as it seems. What is the depth of a het-alt1-alt2, when you have two insertions of different length?
        //Least controversial is to take the maximum.
        public int GetDepthCountInt(IEnumerable<BaseCalledAllele> variants)
        {
            BaseCalledAllele firstVariant = variants.First();
            int totalDepth = 0;
            foreach (var variant in variants)
            {
                totalDepth = Math.Max(variant.TotalCoverage, totalDepth);
            }
            return totalDepth;
        }

        private static string GetAlleleCountString(IEnumerable<BaseCalledAllele> variants, bool isReference)
        {
            BaseCalledAllele firstVariant = variants.First();

            if (isReference)
                return firstVariant.AlleleSupport.ToString();
            else
            {

                Genotype gt = ((CalledVariant)firstVariant).Genotype;
                if ((gt == Genotype.HeterozygousAlt1Alt2) || (gt == Genotype.Alt12LikeNoCall))
                {
                    List<string> altAllelesSupport = (variants.Select(v => v.AlleleSupport.ToString()).ToList());
                    return (string.Join(",", altAllelesSupport));
                }
                else
                    return string.Format("{0},{1}", ((CalledVariant)variants.First()).ReferenceSupport, firstVariant.AlleleSupport);
            }

        }


        public static IEnumerable<FilterType> MergeFilters(IEnumerable<BaseCalledAllele> variants)
        {
            List<FilterType> filters = new List<FilterType>();
            foreach (var allele in variants)
            {
                filters.AddRange(allele.Filters);
            }
            return filters.Distinct();
        }

        public string[] MergeReferenceAndAlt(IEnumerable<BaseCalledAllele> variants)
        {

            string refWithMaxLength = "";
            string altString = "";

            foreach (var v in variants)
            {
                if (v.Reference.Length > refWithMaxLength.Length)
                    refWithMaxLength = v.Reference;
            }

            bool started = false;
            foreach (var v in variants)
            {
                string varRepresenation = v.Alternate;
                if(refWithMaxLength.Length != v.Reference.Length);
                {
                    string basesToAppend = refWithMaxLength.Substring(v.Reference.Length);
                    varRepresenation += basesToAppend;
                }
      
                if (started)
                    altString += ",";

                altString += varRepresenation;

                started = true;
            }

          
            return new string[] { refWithMaxLength, altString };
        }

        private string MergeReference(IEnumerable<BaseCalledAllele> variants)
        {
            return (string.Join(",", variants.Select(v => v.Reference)));
        }

        private string MergeAlternate(IEnumerable<BaseCalledAllele> variants)
        {
            return (string.Join(",", variants.Select(v => v.Alternate)));
        }

        public int MergeVariantQScores(IEnumerable<BaseCalledAllele> variants)
        {
            return (variants.Min(v => v.VariantQscore));
        }

        public int MergeGenotypeQScores(IEnumerable<BaseCalledAllele> variants)
        {
            return (variants.Min(v => v.GenotypeQscore));
        }
    }
}
