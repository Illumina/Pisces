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
        public string UmiStatsFormat = "US";
        public string AlleleDepthFormat = "AD";
        public string TotalDepthFormat = "DP";
        public string VariantFrequencyFormat = "VF";
        public string NoiseLevelFormat = "NL";
        public string StrandBiasFormat = "SB";
        public string FractionNoCallFormat = "NC";
        public string DepthInfo = "DP";
        public string FrequencySigFigFormat;
        public const string PassFilter = "PASS";
        public const char FilterSeparator = ';';
        public const bool SumMultipleVF = true;

        public string FrequencyFilterThresholdString
        {
            get { return _config.FrequencyFilterThreshold.HasValue ? _config.FrequencyFilterThreshold.Value.ToString(FrequencySigFigFormat) : string.Empty; }
        }

        private const double MinStrandBiasScore = -100;
        private const double MaxStrandBiasScore = 0;

        private VcfWriterConfig _config;

        public VcfFormatter() { }

        public VcfFormatter(VcfWriterConfig Config)
        {
            _config = Config;
            UpdateFrequencyFormat();
        }

        private void UpdateFrequencyFormat()
        {
            FrequencySigFigFormat = "0.";
            var minFreqString = _config.MinFrequencyThreshold.ToString();

            var freqSignificantDigits = GetNumSigDigits(minFreqString);

            if (_config.FrequencyFilterThreshold.HasValue)
                freqSignificantDigits = Math.Max(freqSignificantDigits, GetNumSigDigits(_config.FrequencyFilterThreshold.Value.ToString()));

            for (var i = 0; i < freqSignificantDigits; i++)
                FrequencySigFigFormat += "0";
        }

        private int GetNumSigDigits(string inputValue)
        {
            return inputValue.Contains("E")
                ? Math.Abs(int.Parse(inputValue.Split('E')[1]))
                : inputValue.Length - 1;
        }

        public Dictionary<FilterType,string> GenerateFilterStringsByType()
        {
            var filterStringsForHeader = new Dictionary<FilterType, string>();

            if (_config.VariantQualityFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.LowVariantQscore,  string.Format("##FILTER=<ID=q{0},Description=\"Quality score less than {0}\">", _config.VariantQualityFilterThreshold.Value));

            if (_config.DepthFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.LowDepth, string.Format("##FILTER=<ID=LowDP,Description=\"Low coverage (DP tag), therefore no genotype called\">"));

            if (_config.StrandBiasFilterThreshold.HasValue && _config.ShouldFilterOnlyOneStrandCoverage)
            {
                filterStringsForHeader.Add(FilterType.StrandBias, string.Format("##FILTER=<ID={0},Description=\"Variant strand bias too high or coverage on only one strand\">", StrandBiasFormat));
            }
            else if (_config.StrandBiasFilterThreshold.HasValue)
            {
                filterStringsForHeader.Add(FilterType.StrandBias, string.Format("##FILTER=<ID={0},Description=\"Variant strand bias too high\">", StrandBiasFormat));
            }
            else if (_config.ShouldFilterOnlyOneStrandCoverage)
            {
                filterStringsForHeader.Add(FilterType.StrandBias, string.Format("##FILTER=<ID={0},Description=\"Variant support on only one strand\">", StrandBiasFormat));
            }

            if (_config.FrequencyFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.LowVariantFrequency, string.Format("##FILTER=<ID=LowVariantFreq,Description=\"Variant frequency less than {0}\">", FrequencyFilterThresholdString));

            if (_config.GenotypeQualityFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.LowGenotypeQuality, string.Format("##FILTER=<ID=LowGQ,Description=\"Genotype Quality less than {0}\">", _config.GenotypeQualityFilterThreshold.Value));

            if (_config.IndelRepeatFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.IndelRepeatLength, string.Format("##FILTER=<ID=R{0},Description=\"Indel repeat greater than or equal to {0}\">", _config.IndelRepeatFilterThreshold));

            if (_config.PloidyModel == PloidyModel.Diploid)
                filterStringsForHeader.Add(FilterType.MultiAllelicSite, string.Format("##FILTER=<ID=MultiAllelicSite,Description=\"Variant does not conform to diploid model\">"));

            if (_config.RMxNFilterMaxLengthRepeat.HasValue && _config.RMxNFilterMinRepetitions.HasValue)
                filterStringsForHeader.Add(FilterType.RMxN, string.Format("##FILTER=<ID=R{0}x{1},Description=\"Repeats of part or all of the variant allele (max repeat length {0}) in the reference greater than or equal to {1}\">", _config.RMxNFilterMaxLengthRepeat, _config.RMxNFilterMinRepetitions));

            return filterStringsForHeader;
        }
        public string MapFilters(IEnumerable<CalledAllele> variants)
        {
            var filters = MergeFilters(variants);
            var filterString = string.Join(FilterSeparator.ToString(), filters.Select(MapFilter));
            return string.IsNullOrEmpty(filterString) ? PassFilter : filterString;
        }

        public string MapFilter(FilterType filter)
        {
            switch (filter)
            {
                case FilterType.LowVariantQscore:
                    if (!_config.VariantQualityFilterThreshold.HasValue)
                        throw new Exception("Variant has low qscore filter but threshold is not set.");
                    return "q" + _config.VariantQualityFilterThreshold.Value;
                case FilterType.StrandBias:
                    return StrandBiasFormat;
                case FilterType.LowDepth:
                    return "LowDP";
                case FilterType.LowVariantFrequency:
                    return "LowVariantFreq";
                case FilterType.LowGenotypeQuality:
                    return "LowGQ";
                case FilterType.IndelRepeatLength:
                    if (!_config.IndelRepeatFilterThreshold.HasValue)
                        throw new Exception("Variant has indel repeat filter but threshold is not set.");
                    return "R" + _config.IndelRepeatFilterThreshold;
                case FilterType.RMxN:
                    if (!_config.RMxNFilterMaxLengthRepeat.HasValue || !_config.RMxNFilterMinRepetitions.HasValue)
                        throw new Exception("Variant has RMxN filter but M or N value is not set.");
                    return "R" + _config.RMxNFilterMaxLengthRepeat + "x" + _config.RMxNFilterMinRepetitions;
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
                case Genotype.RefAndNoCall:
                    return "0/.";
                case Genotype.AltAndNoCall:
                    return "1/.";
                default:
                    return "./.";
            }
        }

        public string[] ConstructFormatAndSampleString(IEnumerable<CalledAllele> variants, int totalDepth)
        {
            CalledAllele firstVariant = variants.First();
            var gtQuality = MergeGenotypeQScores(variants);
            var gtString = MapGenotype(firstVariant.Genotype);
            var isReference = (firstVariant.Type == AlleleCategory.Reference);

            var alleleCountString = GetAlleleCountString(variants, isReference);
            var frequencyString = GetFrequencyString(variants, isReference);

            var formatStringBuilder = new StringBuilder("GT:GQ:AD:DP:VF");
            var sampleStringBuilder = new StringBuilder(string.Format("{0}:{1}:{2}:{3}:{4}", gtString, gtQuality, alleleCountString, totalDepth, frequencyString));

            if (_config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                var biasScoreString = (Math.Min(Math.Max(MinStrandBiasScore, firstVariant.StrandBiasResults.GATKBiasScore), MaxStrandBiasScore)).ToString("0.0000");

                formatStringBuilder.Append(":NL:SB");
                sampleStringBuilder.Append(string.Format(":{0}:{1}", firstVariant.NoiseLevelApplied, biasScoreString));

            }

            if (_config.ShouldOutputNoCallFraction)
            {
                var noCallFractionString = firstVariant.FractionNoCalls.ToString("0.0000");

                formatStringBuilder.Append(":NC");
                sampleStringBuilder.Append(string.Format(":{0}", noCallFractionString));
            }

            if (_config.ShouldOutputRcCounts)
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

        private string GetFrequencyString(IEnumerable<CalledAllele> variants, bool isReference)
        {
            CalledAllele firstVariant = variants.First();

            if (isReference)
            {
                if (firstVariant.TotalCoverage==0)
                    return (0).ToString(FrequencySigFigFormat);
                else
                    return (1 - firstVariant.Frequency).ToString(FrequencySigFigFormat);
            }
            else
            {
                Genotype gt = firstVariant.Genotype;
                if ((gt == Genotype.HeterozygousAlt1Alt2) || (gt == Genotype.Alt12LikeNoCall))
                {
                    if (SumMultipleVF)
                    {
                        return variants.Select(v => v.Frequency).Sum().ToString(FrequencySigFigFormat);
                    }
                    else
                    {
                        var altAllelesFrequencies = (variants.Select(v => v.Frequency.ToString(FrequencySigFigFormat))).ToList();
                        return (string.Join(",", altAllelesFrequencies));
                    }
                }
                else
                    return (firstVariant.Frequency).ToString(FrequencySigFigFormat);
            }
        }

        //this is not as obvious as it seems. What is the depth of a het-alt1-alt2, when you have two insertions of different length?
        //Least controversial is to take the maximum.
        public int GetDepthCountInt(IEnumerable<CalledAllele> variants)
        {
            CalledAllele firstVariant = variants.First();
            int totalDepth = 0;
            foreach (var variant in variants)
            {
                totalDepth = Math.Max(variant.TotalCoverage, totalDepth);
            }
            return totalDepth;
        }

        private static string GetAlleleCountString(IEnumerable<CalledAllele> variants, bool isReference)
        {
            CalledAllele firstVariant = variants.First();

            if (isReference)
                return firstVariant.AlleleSupport.ToString();

            else
            {
                Genotype gt = firstVariant.Genotype;
                if ((gt == Genotype.HeterozygousAlt1Alt2) || (gt == Genotype.Alt12LikeNoCall))
                {
                    List<string> altAllelesSupport = (variants.Select(v => v.AlleleSupport.ToString()).ToList());
                    return (string.Join(",", altAllelesSupport));
                }
                else
                {
                    return string.Format("{0},{1}", variants.First().ReferenceSupport, firstVariant.AlleleSupport);
                }
            }
        }


        public static IEnumerable<FilterType> MergeFilters(IEnumerable<CalledAllele> variants)
        {
            List<FilterType> filters = new List<FilterType>();
            foreach (var allele in variants)
            {
                filters.AddRange(allele.Filters);
            }
            return filters.Distinct();
        }

        public string[] MergeReferenceAndAlt(IEnumerable<CalledAllele> variants)
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

      

        public int MergeVariantQScores(IEnumerable<CalledAllele> variants)
        {
            return (variants.Min(v => v.VariantQscore));
        }

        public int MergeGenotypeQScores(IEnumerable<CalledAllele> variants)
        {
            return (variants.Min(v => v.GenotypeQscore));
        }
    }
}
