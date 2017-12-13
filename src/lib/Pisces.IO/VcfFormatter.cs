using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

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
        public string ProbeBiasFormat = "PB";
        public string FractionNoCallFormat = "NC";
        public string DepthInfo = "DP";
        public string FrequencySigFigFormat;
        public const string PassFilter = "PASS";
        public const char FilterSeparator = ';';
        public const bool SumMultipleVF = true;
        public const string UnspecifiedAllele = "<M>"; //typically this is "*" or "<*>" in unary representation

        public string FrequencyFilterThresholdString
        {
            get { return _config.FrequencyFilterThreshold.HasValue ? _config.FrequencyFilterThreshold.Value.ToString(FrequencySigFigFormat) : string.Empty; }
        }

        private const double MinStrandBiasScore = -100;
        private const double MaxStrandBiasScore = 0;

        protected VcfWriterConfig _config;

        public VcfFormatter() { }

        public VcfFormatter(VcfWriterConfig Config)
        {
            _config = Config;
            UpdateFrequencyFormat();
        }

        protected void UpdateFrequencyFormat()
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

        public Dictionary<FilterType, string> GenerateFilterStringsByType()
        {
            var filterStringsForHeader = new Dictionary<FilterType, string>();

            if (_config.VariantQualityFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.LowVariantQscore, string.Format("##FILTER=<ID=q{0},Description=\"Quality score less than {0}\">", _config.VariantQualityFilterThreshold.Value));

            if (_config.ProbePoolBiasFilterThreshold.HasValue)
                filterStringsForHeader.Add(FilterType.PoolBias, string.Format(
"##FILTER=<ID=PB,Description=\"Probe pool bias - variant not found, or found with low frequency, in one of two probe pools\">"));


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

            if (_config.HasForcedGt)
            {
                filterStringsForHeader.Add(FilterType.ForcedReport, string.Format("##FILTER=<ID=ForcedReport,Description=\"Variants is called because it is one of forced genotype alleles\">"));

                if (!_config.DepthFilterThreshold.HasValue)
                    filterStringsForHeader.Add(FilterType.LowDepth, string.Format("##FILTER=<ID=LowDP,Description=\"Low coverage (DP tag), therefore no genotype called\">"));

                if (!_config.FrequencyFilterThreshold.HasValue)
                    filterStringsForHeader.Add(FilterType.LowVariantFrequency, string.Format("##FILTER=<ID=LowVariantFreq,Description=\"Variant frequency less than {0}\">", _config.MinFrequencyThreshold.ToString(FrequencySigFigFormat)));
            }
                
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
                        throw new InvalidDataException("Variant has low qscore filter but threshold is not set.");
                    return "q" + _config.VariantQualityFilterThreshold.Value;
                case FilterType.StrandBias:
                    return StrandBiasFormat;
                case FilterType.PoolBias:
                    return ProbeBiasFormat;
                case FilterType.LowDepth:
                    return "LowDP";
                case FilterType.LowVariantFrequency:
                    return "LowVariantFreq";
                case FilterType.LowGenotypeQuality:
                    return "LowGQ";
                case FilterType.IndelRepeatLength:
                    if (!_config.IndelRepeatFilterThreshold.HasValue)
                        throw new InvalidDataException("Variant has indel repeat filter but threshold is not set.");
                    return "R" + _config.IndelRepeatFilterThreshold;
                case FilterType.RMxN:
                    if (!_config.RMxNFilterMaxLengthRepeat.HasValue || !_config.RMxNFilterMinRepetitions.HasValue)
                        throw new InvalidDataException("Variant has RMxN filter but M or N value is not set.");
                    return "R" + _config.RMxNFilterMaxLengthRepeat + "x" + _config.RMxNFilterMinRepetitions;
                case FilterType.MultiAllelicSite:
                    return "MultiAllelicSite";
                case FilterType.ForcedReport:
                    return "ForcedReport";
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
                case Genotype.HemizygousAlt:
                    return "1";
                case Genotype.HemizygousNoCall:
                    return ".";
                case Genotype.HemizygousRef:
                    return "0";
                case Genotype.Others:
                    return "2/2";
                default:
                    return "./.";
            }
        }

        //leave it to any child classes if they have special tags to write to VCF
        public virtual StringBuilder[] AddCustomTags(IEnumerable<CalledAllele> variants, StringBuilder[] formatAndSampleString)
        {
            //do nothing by default
            return formatAndSampleString;
        }

        public string[] ConstructFormatAndSampleString(IEnumerable<CalledAllele> variants, int totalDepth)
        {
            CalledAllele firstVariant = variants.First();
            var gtQuality = MergeGenotypeQScores(variants);
            var gtString = MapGenotype(firstVariant.Genotype);
            var isReference = (firstVariant.IsRefType);

            var alleleCountString = GetAlleleCountString(variants, isReference,totalDepth);
            var frequencyString = GetFrequencyString(variants, isReference, totalDepth);

            var formatStringBuilder = new StringBuilder("GT:GQ:AD:DP:VF");
            var sampleStringBuilder = new StringBuilder(string.Format("{0}:{1}:{2}:{3}:{4}", gtString, gtQuality, alleleCountString, totalDepth, frequencyString));

            if (_config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                var biasScoreString = (Math.Min(Math.Max(MinStrandBiasScore, firstVariant.StrandBiasResults.GATKBiasScore), MaxStrandBiasScore)).ToString("0.0000");

                formatStringBuilder.Append(":NL:SB");
                sampleStringBuilder.Append(string.Format(":{0}:{1}", firstVariant.NoiseLevelApplied, biasScoreString));
            }

            if (_config.ShouldOutputProbeBias)
            {
                var ag = (AggregateAllele)firstVariant;
                var biasScoreString = (Math.Min(Math.Max(MinStrandBiasScore, ag.PoolBiasResults.GATKBiasScore), MaxStrandBiasScore)).ToString("0.0000");

                formatStringBuilder.Append(":PB");
                sampleStringBuilder.Append(string.Format(":{0}", biasScoreString));
            }

            if (_config.ShouldOutputNoCallFraction)
            {
                var noCallFractionString = firstVariant.FractionNoCalls.ToString("0.0000");

                formatStringBuilder.Append(":NC");
                sampleStringBuilder.Append(string.Format(":{0}", noCallFractionString));
            }

            if (_config.ShouldOutputRcCounts)
            {
                if (_config.ShouldOutputTsCounts)
                {
                    formatStringBuilder.Append(":US");
                    sampleStringBuilder.Append(string.Format(":{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.DuplexStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.DuplexNonStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexForwardStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexForwardNonStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexReverseStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexReverseNonStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.DuplexStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.DuplexNonStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexForwardStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexForwardNonStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexReverseStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexReverseNonStitched]));
                }
                else
                {
                    formatStringBuilder.Append(":US");
                    sampleStringBuilder.Append(string.Format(":{0},{1},{2},{3},{4},{5},{6},{7}",
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.DuplexStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.DuplexNonStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexStitched],
                        firstVariant.ReadCollapsedCountsMut[(int)ReadCollapsedType.SimplexNonStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.DuplexStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.DuplexNonStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexStitched],
                        firstVariant.ReadCollapsedCountTotal[(int)ReadCollapsedType.SimplexNonStitched]));
                }
                
            }



            AddCustomTags(variants, new StringBuilder[] { formatStringBuilder, sampleStringBuilder });

            return new[]
            {
                formatStringBuilder.ToString(),
                sampleStringBuilder.ToString()
            };
        }

        protected string GetFrequencyString(IEnumerable<CalledAllele> variants, bool isReference, int totalDepth)
        {
            CalledAllele firstVariant = variants.First();

            if (isReference)
            {
                if (firstVariant.TotalCoverage == 0)
                    return (0).ToString(FrequencySigFigFormat);
                return (1 - firstVariant.Frequency).ToString(FrequencySigFigFormat);
            }
            Genotype gt = firstVariant.Genotype;

            if ((gt == Genotype.HeterozygousAlt1Alt2) || (gt == Genotype.Alt12LikeNoCall))
            {
                if (SumMultipleVF)
                {
                    return variants.Select(v => ((double)v.AlleleSupport / (double)totalDepth)).Sum().ToString(FrequencySigFigFormat);
                }

                // tjd +
                // commented out since we are currently just using one number to represent
                // VF, even for alt1, alt2 variants.
                // IE, SumMultipleVF is ucrrently never false.
                //
                //var altAllelesFrequencies = (variants.Select(v => ((double)v.AlleleSupport / (double)totalDepth).ToString(FrequencySigFigFormat))).ToList();
                //return (string.Join(",", altAllelesFrequencies));
                //tjd -
            }
            return (firstVariant.Frequency).ToString(FrequencySigFigFormat);
        }

        //this is not as obvious as it seems. What is the depth of a het-alt1-alt2, when you have two insertions of different length?
        //Least controversial is to take the maximum. 
        //(1) Update to the comment above: Nope! Then we occasionally got cornercases where we have two indel VF > 50%, resulting in a total VF >1.0.
        //So, lets keep tabs on the total number of variant reads (which must be mutually exclusive) + number of reference reads (might not be mutually exclusive for indels, so use carefully)
        //(2) Second update. Here is another difficult corner case (PICS-657):  Collapsing is on and so is MNV reallocation.  SPS we have one count of an open ended SNP (A>G) at position 1 and  MNV (AAA>GGG), 3 counts, anchored.
        //Then the SNP would collapse into the MNV, raising the MNV counts for all bases up to 4. But, then sps that MNV failed filters and gets reallocated (4 counts per all bases). Then sps we call the last A>G at positon 3.
        //We could potentially be calling one count of last "G" that was never really observed (only, assumed) in the depth count.
        //We added a patch to the code (PULL #400) that atempted to fix the above issue upstream, by preventing collapsing from adding counts to MNVs.
        //The observed issue was reduced but not entirely resolved by pull #400. 
        //The following changes (inbetween the + and - sections of code) is just a band-aid fix, to add to the DP our extra depth counted in the AD,
        //We need a more invasive fix that solves where this discrepancy is coming from. Specifically, we have seen instances of the same issue with MNV
        //calling turned off.  
        
        public int GetDepthCountInt(IEnumerable<CalledAllele> variants)
        {
            CalledAllele firstVariant = variants.First();
            int totalDepth = 0;

            //set a reasonable minimum (given the two corner cases, above)
            //+
            if (firstVariant.IsRefType)
                totalDepth = firstVariant.ReferenceSupport;
            else
                totalDepth = firstVariant.ReferenceSupport + firstVariant.AlleleSupport; // note that ReferenceSupport == DP*(1-vf for indels) so still safe, if used only once. 
            //-

            int totalVariantReads = 0;

            foreach (var variant in variants)
            {
                totalDepth = Math.Max(totalDepth, Math.Max(variant.TotalCoverage, totalDepth));
                totalVariantReads += variant.AlleleSupport;
            }
            return Math.Max(totalDepth, totalVariantReads);
        }

        private static string GetAlleleCountString(IEnumerable<CalledAllele> variants, bool isReference,int totalDepth)
        {
            CalledAllele firstVariant = variants.First();

            if (isReference)
                return firstVariant.AlleleSupport.ToString();

            Genotype gt = firstVariant.Genotype;
            if (gt == Genotype.HeterozygousAlt1Alt2 || gt == Genotype.Alt12LikeNoCall || gt == Genotype.Others)
            {
                List<string> altAllelesSupport = variants.Select(v => v.AlleleSupport.ToString()).ToList();

                if (variants.Count() > 1)
                    return string.Join(",", altAllelesSupport);

                var otherReads = totalDepth - firstVariant.AlleleSupport -
                                 firstVariant.ReferenceSupport;

                if (firstVariant.PhaseSetIndex == 1 || gt == Genotype.Others)
                    return string.Format("{0},{1},{2}", firstVariant.ReferenceSupport, firstVariant.AlleleSupport, otherReads);

                return string.Format("{0},{1},{2}", firstVariant.ReferenceSupport, otherReads, firstVariant.AlleleSupport);
            }
            return string.Format("{0},{1}", variants.First().ReferenceSupport, firstVariant.AlleleSupport);
        }


        public static IEnumerable<FilterType> MergeFilters(IEnumerable<CalledAllele> variants)
        {
            List<FilterType> filters = new List<FilterType>();
            foreach (var allele in variants)
            {
                if (allele != null)
                    filters.AddRange(allele.Filters);
            }
            return filters.Distinct();
        }

        public string[] SetUncrushedReferenceAndAlt(CalledAllele variant)
        {
            string refString = variant.ReferenceAllele;
            string altString = variant.AlternateAllele;

            if (variant.Genotype == Genotype.HeterozygousAlt1Alt2 || variant.Genotype == Genotype.Alt12LikeNoCall || variant.Genotype == Genotype.Others)
            {
                if (variant.PhaseSetIndex == 1|| variant.Genotype == Genotype.Others)
                    altString = variant.AlternateAllele + "," + UnspecifiedAllele;
                else
                    altString = UnspecifiedAllele + "," + variant.AlternateAllele;
            }

            return new string[] { refString, altString };
        }

        public string[] MergeCrushedReferenceAndAlt(IEnumerable<CalledAllele> variants)
        {

            string refWithMaxLength = "";
            string altString = "";

            foreach (var v in variants)
            {
                if (v.ReferenceAllele.Length > refWithMaxLength.Length)
                    refWithMaxLength = v.ReferenceAllele;
            }

            bool started = false;
            foreach (var v in variants)
            {
                string varRepresenation = v.AlternateAllele;
                if (refWithMaxLength.Length != v.ReferenceAllele.Length) 
                {
                    string basesToAppend = refWithMaxLength.Substring(v.ReferenceAllele.Length);
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