using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Pisces.Domain.Options;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.IO.Interfaces;
using Common.IO;

namespace Pisces.IO
{
    public class VcfFileWriter : BaseVcfFileWriter<CalledAllele>
    {
        private const string VcfVersion = "VCFv4.1";
        private const string MissingValue = ".";
        
        protected VcfWriterInputContext _context;
        protected VcfWriterConfig _config;
        protected VcfFormatter _formatter;
        protected int _lastVariantPositionWritten;

        public VcfFileWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, int bufferLimit = 2000)
            : base(outputFilePath, bufferLimit)
        {
            _context = context;
            _config = config;
            _formatter = new VcfFormatter(config);

            AllowMultipleVcfLinesPerLoci = config.AllowMultipleVcfLinesPerLoci;

            OutputFilePath = outputFilePath;
        }

        public override void WriteHeader()
        {
            if (Writer == null)
                throw new IOException("Stream already closed");

            var currentAssembly = Assembly.GetEntryAssembly().GetName();  //for GetCallingAssembly, originally from .net core

            Writer.WriteLine("##fileformat=" + VcfVersion);
            Writer.WriteLine("##fileDate=" + string.Format("{0:yyyyMMdd}", DateTime.Now));
            Writer.WriteLine("##source=" + currentAssembly.Name + " " + currentAssembly.Version);
            Writer.WriteLine("##" + currentAssembly.Name + "_cmdline=\"" + (_context.CommandLine == null? "" : string.Join(" ",_context.CommandLine)) + "\"");
            Writer.WriteLine("##reference=" + _context.ReferenceName);

            // info fields
            Writer.WriteLine("##INFO=<ID=" + _formatter.DepthInfo + ",Number=1,Type=Integer,Description=\"Total Depth\">");

            // filter fields
            var filterStringsForHeader = _formatter.GenerateFilterStringsByType();
            foreach (var filter in filterStringsForHeader)
            {
                Writer.WriteLine(filter.Value);
            }

            
            

            // format fields
            Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=String,Description=\"Genotype\">", _formatter.GenotypeFormat);
            Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Integer,Description=\"Genotype Quality\">", _formatter.GenotypeQualityFormat);
            Writer.WriteLine("##FORMAT=<ID={0},Number=.,Type=Integer,Description=\"Allele Depth\">", _formatter.AlleleDepthFormat);
            Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Integer,Description=\"Total Depth Used For Variant Calling\">", _formatter.TotalDepthFormat);
            Writer.WriteLine("##FORMAT=<ID={0},Number=.,Type=Float,Description=\"Variant Frequency\">", _formatter.VariantFrequencyFormat);

            if (_config.ShouldOutputProbeBias)
            {
                Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"ProbeBias Score\">", _formatter.ProbeBiasFormat);
            }

            if (_config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Integer,Description=\"Applied BaseCall Noise Level\">", _formatter.NoiseLevelFormat);
                Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"StrandBias Score\">", _formatter.StrandBiasFormat);
            }

            if (_config.ShouldOutputNoCallFraction)
                Writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"Fraction of bases which were uncalled or with basecall quality below the minimum threshold\">", _formatter.FractionNoCallFormat);

            if (_config.ShouldOutputRcCounts)
                Writer.WriteLine("##FORMAT=<ID={0},Number=.,Type=Integer,Description=\"Supporting read type counts\">", _formatter.UmiStatsFormat);

            WriteContigs(Writer);
            WriteColHeaders(Writer);
        }

        private void WriteColHeaders(StreamWriter writer)
        {
            writer.Write("#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO\tFORMAT\t" + _context.SampleName + "\n");
        }

        private void WriteContigs(StreamWriter writer)
        {
            if (_context.ContigsByChr == null) return;

            foreach (var contig in _context.ContigsByChr)
            {
                writer.WriteLine("##contig=<ID=" + contig.Item1 + ",length=" + contig.Item2 + ">");
            }
        }

        private void PadIfNeeded(StreamWriter writer, int position, IRegionMapper mapper)
        {
            // Pad any no calls that are in intervals between this allele and the last allele written.
            // We should also enter this block on the first allele encountered (to pick up any intervals before coverage starts).
            if (mapper != null &&
                (_lastVariantPositionWritten == 0 || _lastVariantPositionWritten + 1 < position))
            {
                CalledAllele nocall;
                var alleleList = new List<CalledAllele>(); // reuse the list
                while ((nocall = mapper.GetNextEmptyCall(_lastVariantPositionWritten + 1, position - 1)) !=
                       null)
                {
                    alleleList.Clear();
                    alleleList.Add(nocall);
                    WriteListOfColocatedAlleles(writer, alleleList);
                }
            }
        }

        protected override void WriteSingleAllele(StreamWriter writer, CalledAllele variant, IRegionMapper mapper = null)
        {
            // Pad intervals if necessary
            PadIfNeeded(writer, variant.ReferencePosition, mapper);

            WriteListOfColocatedAlleles(writer, new List<CalledAllele> { variant });
        }

        public override void WriteRemaining(IRegionMapper mapper = null, Dictionary<int, List<Tuple<string, string>>> forcedAllelesByPos = null)
        {
            // Pad any nocalls in intervals after coverage ends.
            // Unlike WriteSingleAllele which is called internally during buffer flushing this is called directly
            // by external components, like the somatic variant caller. 
            if (mapper != null)
            {
                FlushBuffer(mapper);  // flush real alleles that are hanging out

                CalledAllele nocall;
                var alleleList = new List<CalledAllele>(); // reuse list
                while ((nocall = mapper.GetNextEmptyCall(_lastVariantPositionWritten + 1, null)) != null)
                {
                    alleleList.Clear();
                    alleleList.Add(nocall);
                    WriteListOfColocatedAlleles(Writer, alleleList);
                    if (forcedAllelesByPos != null && forcedAllelesByPos.ContainsKey(nocall.ReferencePosition))
                    {
                        WriteNoCallForcedAllele(nocall.Chromosome, nocall.ReferencePosition,
                            forcedAllelesByPos[nocall.ReferencePosition]);
                    }

				}

                _lastVariantPositionWritten = 0; // reset in case writer is reused for the next chromosome
            }
        }

	    private void WriteNoCallForcedAllele(string chr,int referencePosition, List<Tuple<string, string>> forcedAllelesInPos)
	    {
		    foreach (var allele in forcedAllelesInPos)
		    {
			    var forcedCallAllele = new CalledAllele(AlleleCategory.NonReference)
			    {
				    ReferencePosition = referencePosition,
				    Chromosome = chr,
				    GenotypeQscore = 0,
				    ReferenceAllele = allele.Item1,
				    AlternateAllele = allele.Item2,
				    TotalCoverage = 0,
				    AlleleSupport = 0,
				    ReferenceSupport = 0,
				    IsForcedToReport = true,
				    Genotype = Genotype.RefLikeNoCall
			    };
				forcedCallAllele.Filters = new List<FilterType> {FilterType.ForcedReport};
			    WriteListOfColocatedAlleles(Writer, new List<CalledAllele>{ forcedCallAllele});
			}
	    }

	    protected override void GroupsAllelesThenWrite(StreamWriter writer, List<CalledAllele> variants, IRegionMapper mapper = null)
        {
            // variant list is already sorted properly, group as we go.
            var variantsAtSamePosition = new List<CalledAllele>();

            foreach (var variant in variants)
            {
                if (!variantsAtSamePosition.Any() ||
                    ((variant.ReferencePosition == variantsAtSamePosition[0].ReferencePosition) && (variant.Chromosome== variantsAtSamePosition[0].Chromosome)))
                    variantsAtSamePosition.Add(variant);
                else
                {
                    // flush
                    PadIfNeeded(writer, variantsAtSamePosition[0].ReferencePosition, mapper);  // Pad intervals if necessary
                    WriteListOfColocatedAlleles(Writer, variantsAtSamePosition);

                    variantsAtSamePosition.Clear();
                    variantsAtSamePosition.Add(variant);
                }
            }

            // final flush
            if (variantsAtSamePosition.Any())
            {
                PadIfNeeded(writer, variantsAtSamePosition[0].ReferencePosition, mapper); // Pad intervals if necessary
                WriteListOfColocatedAlleles(Writer, variantsAtSamePosition);
            }
        }

        protected void WriteListOfColocatedAlleles(StreamWriter writer, List<CalledAllele> variants)
        {
            if (!variants.Any())
                return;

            _lastVariantPositionWritten = variants[0].ReferencePosition;  // record last written real allele position

            try
            {
                var totalDepth = _formatter.GetDepthCountInt(variants);
                var jointVariantQuality = _formatter.MergeVariantQScores(variants);
                var jointGenotypeQuality = _formatter.MergeGenotypeQScores(variants);

                var firstVariant = variants.First();
                var formatAndSampleString = _formatter.ConstructFormatAndSampleString(variants, totalDepth);

                var refAndAltString =
                        (variants.Count == 1)? _formatter.SetUncrushedReferenceAndAlt(firstVariant) : _formatter.MergeCrushedReferenceAndAlt(variants);

                //CHROM
                writer.Write(firstVariant.Chromosome + "\t");
                //POS
                writer.Write(firstVariant.ReferencePosition + "\t");
                //ID
                writer.Write("." + "\t");
                //REF
                writer.Write(refAndAltString[0] + "\t");
                //ALT
                if (!firstVariant.IsForcedToReport && ((firstVariant.Genotype == Genotype.HomozygousRef)
                    || (firstVariant.Genotype == Genotype.RefLikeNoCall)
                     || (firstVariant.Genotype == Genotype.RefAndNoCall)
					 || (firstVariant.Genotype == Genotype.HemizygousNoCall)
					 || (firstVariant.Genotype == Genotype.HemizygousRef)))
                    //note, nocall is only used for low-depth regions where we do not try to var-call.
                    writer.Write("." + "\t");
                else
                {
                    writer.Write((refAndAltString[1] ?? ".") + "\t");
                }
                //QUAL
                writer.Write(jointVariantQuality + "\t");
                //FILTER
                writer.Write(_formatter.MapFilters(variants) + "\t");
                //INFO
                writer.Write(_formatter.DepthInfo + "=" + totalDepth + "\t");
                //FORMAT
                writer.Write(formatAndSampleString[0] + "\t");
                //SAMPLE
                writer.Write(formatAndSampleString[1] + "\n");
            }
            catch (Exception ex)
            {
                OnException(ex);
            }
        }

    }

    public class VcfWriterConfig
    {
        public bool ShouldOutputNoCallFraction { get; set; }
        public bool ShouldOutputStrandBiasAndNoiseLevel { get; set; }
        public bool ShouldOutputProbeBias { get; set; }
        public bool ShouldFilterOnlyOneStrandCoverage { get; set; }
        public bool ShouldOutputRcCounts { get; set; }
        public bool AllowMultipleVcfLinesPerLoci { get; set; }
        public PloidyModel PloidyModel { get; set; }
        public int? VariantQualityFilterThreshold { get; set; }
        public int? GenotypeQualityFilterThreshold { get; set; }
        public int? DepthFilterThreshold { get; set; }
        public int? IndelRepeatFilterThreshold { get; set; }
        public float? StrandBiasFilterThreshold { get; set; }
        public float? ProbePoolBiasFilterThreshold { get; set; }
        public float MinFrequencyThreshold { get; set; }
        public float? FrequencyFilterThreshold { get; set; }
        public int EstimatedBaseCallQuality { get; set; }
        public int? RMxNFilterMaxLengthRepeat { get; set; }
        public int? RMxNFilterMinRepetitions { get; set; }
        public float? RMxNFilterFrequencyLimit { get; set; }
		public NoiseModel NoiseModel { get; set; }
        public bool HasForcedGt { get; set; }

        public VcfWriterConfig()
        { }

        public VcfWriterConfig(VariantCallingParameters callerOptions,
            VcfWritingParameters outputOptions, BamFilterParameters bamFilterOptions, SampleAggregationParameters sampleAggregationParameters,
            bool debugMode, bool outputBiasFiles,bool hasForcedGT=false)
        {

            DepthFilterThreshold = outputOptions.OutputGvcfFile ? callerOptions.MinimumCoverage : (callerOptions.LowDepthFilter > callerOptions.MinimumCoverage) ? callerOptions.LowDepthFilter : (int?)null;
            IndelRepeatFilterThreshold = callerOptions.IndelRepeatFilter > 0 ? callerOptions.IndelRepeatFilter : (int?)null;
            VariantQualityFilterThreshold = callerOptions.MinimumVariantQScoreFilter;
            GenotypeQualityFilterThreshold = callerOptions.LowGenotypeQualityFilter.HasValue && callerOptions.MinimumVariantQScoreFilter > callerOptions.MinimumVariantQScore ? callerOptions.LowGenotypeQualityFilter : null;
            StrandBiasFilterThreshold = callerOptions.StrandBiasAcceptanceCriteria < 1 ? callerOptions.StrandBiasAcceptanceCriteria : (float?)null;
            FrequencyFilterThreshold = (callerOptions.MinimumFrequencyFilter > callerOptions.MinimumFrequency) ? callerOptions.MinimumFrequencyFilter : (float?)null;
            MinFrequencyThreshold = callerOptions.MinimumFrequency;
            ShouldOutputNoCallFraction = outputOptions.ReportNoCalls;
            ShouldOutputStrandBiasAndNoiseLevel = ShouldOutputNoiseLevelAndStrandBias(debugMode, outputBiasFiles, callerOptions.StrandBiasAcceptanceCriteria);
            ShouldFilterOnlyOneStrandCoverage = callerOptions.FilterOutVariantsPresentOnlyOneStrand;
            EstimatedBaseCallQuality = callerOptions.NoiseLevelUsedForQScoring;
            ShouldOutputRcCounts = outputOptions.ReportRcCounts;
            AllowMultipleVcfLinesPerLoci = outputOptions.AllowMultipleVcfLinesPerLoci;
            PloidyModel = callerOptions.PloidyModel;
            RMxNFilterMaxLengthRepeat = callerOptions.RMxNFilterMaxLengthRepeat;
            RMxNFilterMinRepetitions = callerOptions.RMxNFilterMinRepetitions;
            RMxNFilterFrequencyLimit = callerOptions.RMxNFilterFrequencyLimit;
            NoiseModel = callerOptions.NoiseModel;

            if (sampleAggregationParameters != null)
            {
                ShouldOutputProbeBias = true;
                ProbePoolBiasFilterThreshold = sampleAggregationParameters.ProbePoolBiasThreshold;
            }
            HasForcedGt = hasForcedGT;
        }


        //When should we output the extra info to the VCF?  By default, we should not bother.
        //but if we are testing something fancy, or filtering based on SB, it is probably useful. 
        public bool ShouldOutputNoiseLevelAndStrandBias(bool debugMode, bool outputBiasFiles, float strandBiasAcceptanceCriteria)
        {
            return debugMode || outputBiasFiles || (strandBiasAcceptanceCriteria < 1);
        }
       

    }

    public struct VcfWriterInputContext
    {
        public string ReferenceName { get; set; }
        public string SampleName { get; set; }
        public string[] CommandLine { get; set; }
        public IEnumerable<Tuple<string, long>> ContigsByChr { get; set; }
    }
}