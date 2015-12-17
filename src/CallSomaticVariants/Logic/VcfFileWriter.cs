using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic
{
    public class VcfFileWriter : IVcfFileWriter
    {
        private const string VcfVersion = "VCFv4.1";
        private const string MissingValue = ".";

        private const string DepthInfo = "DP";
        private const string TranscriptIfInfo = "TI";
        private const string GeneIfInfo = "GI";
        private const string ExonRegionInfo = "EXON";
        private const string FunctionalConsequenceInfo = "FC";
        private const string GenotypeFormat = "GT";
        private const string GenotypeQualityFormat = "GQ";
        private const string AlleleDepthFormat = "AD";
        private const string VariantFrequencyFormat = "VF";
        private const string NoiseLevelFormat = "NL";
        private const string StrandBiasFormat = "SB";
        private const string FractionNoCallFormat = "NC";

        private const string PassFilter = "PASS";

        // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        private const double MinStrandBiasScore = -100; 
        private const double MaxStrandBiasScore = 0;
        private string _frequencySigFigFormat;

        private StreamWriter _writer;
        private VcfWriterInputContext _context;
        private VcfWriterConfig _config;
        private readonly string _outputFilePath;
        private List<BaseCalledAllele> _bufferList;
        private int _bufferLimit;

        public VcfFileWriter(string outputFilePath, VcfWriterConfig config, VcfWriterInputContext context, int? bufferLimit = null)
        {
            _context = context;
            _config = config;
            _outputFilePath = outputFilePath;

            UpdateFrequencyFormat();

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(outputFilePath)))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException(string.Format("Failed to create the Output Folder: {0}", outputFilePath));
                    }
                }
            File.Delete(_outputFilePath);
            _writer = new StreamWriter(outputFilePath);
            _bufferLimit = bufferLimit ?? Constants.RegionSize * 2;
            _bufferList = new List<BaseCalledAllele>(_bufferLimit);
            }
            catch (Exception)
            {
                throw new Exception(String.Format("Failed to create {0} in the specified folder.", outputFilePath));
            }
        }

        private void UpdateFrequencyFormat()
        {
            _frequencySigFigFormat = "0.";
            var minFreqString = _config.FrequencyFilterThreshold.ToString();

            var freqSignificantDigits = minFreqString.Contains("E")
                ? Math.Abs(int.Parse(minFreqString.Split('E')[1]))
                : minFreqString.Length - 1;

            for (var i = 0; i < freqSignificantDigits; i++)
                _frequencySigFigFormat += "0";
        }

        public void Write(IEnumerable<BaseCalledAllele> calledVariants)
        {
            if (_writer == null)
                throw new Exception("Stream already closed");

            _bufferList.AddRange(calledVariants.OrderBy(a => a.Coordinate).ThenBy(a => a.Reference).ThenBy(a => a.Alternate));

            if (_bufferList.Count >= _bufferLimit)
                FlushBuffer();
        }

        private void FlushBuffer()
        {
            foreach (var variant in _bufferList)
            {
                WriteVariant(_writer, variant);
            }

            _bufferList.Clear();
        }

        public void WriteHeader()
        {
            if (_writer == null)
                throw new Exception("Stream already closed");

            var currentAssembly = Assembly.GetExecutingAssembly().GetName();

            _writer.WriteLine("##fileformat=" + VcfVersion);
            _writer.WriteLine("##fileDate=" + string.Format("{0:yyyyMMdd}", DateTime.Now));
            _writer.WriteLine("##source=" + currentAssembly.Name + " " + currentAssembly.Version);
            _writer.WriteLine("##" + currentAssembly.Name + "_cmdline=\"" + _context.CommandLine + "\"");
            _writer.WriteLine("##reference=" + _context.ReferenceName);

            // info fields
            _writer.WriteLine("##INFO=<ID=" + DepthInfo + ",Number=1,Type=Integer,Description=\"Total Depth\">");
            _writer.WriteLine("##INFO=<ID=" + TranscriptIfInfo + ",Number=" + MissingValue + ",Type=String,Description=\"Transcript ID\">");
            _writer.WriteLine("##INFO=<ID=" + GeneIfInfo + ",Number=" + MissingValue + ",Type=String,Description=\"Gene ID\">");
            _writer.WriteLine("##INFO=<ID=" + ExonRegionInfo + ",Number=0,Type=Flag,Description=\"Exon Region\">");
            _writer.WriteLine("##INFO=<ID=" + FunctionalConsequenceInfo + ",Number=" + MissingValue + ",Type=String,Description=\"Functional Consequence\">");

            // filter fields
            if (_config.QscoreFilterThreshold.HasValue)
                _writer.WriteLine("##FILTER=<ID=q{0},Description=\"Quality below {0}\">", _config.QscoreFilterThreshold.Value);

            if (_config.DepthFilterThreshold.HasValue)
                _writer.WriteLine("##FILTER=<ID=LowDP,Description=\"Low coverage (DP tag), therefore no genotype called\">");

            if (_config.StrandBiasFilterThreshold.HasValue && _config.ShouldFilterOnlyOneStrandCoverage)
            {
                _writer.WriteLine("##FILTER=<ID={0},Description=\"Variant strand bias too high or coverage on only one strand\">", StrandBiasFormat);
            }
            else if (_config.StrandBiasFilterThreshold.HasValue)
            {
                _writer.WriteLine("##FILTER=<ID={0},Description=\"Variant strand bias too high\">", StrandBiasFormat);
            }
            else if (_config.ShouldFilterOnlyOneStrandCoverage)
            {
                _writer.WriteLine("##FILTER=<ID={0},Description=\"Variant support on only one strand\">", StrandBiasFormat);
            }

            // format fields
            _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=String,Description=\"Genotype\">", GenotypeFormat);
            _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Integer,Description=\"Genotype Quality\">", GenotypeQualityFormat);
            _writer.WriteLine("##FORMAT=<ID={0},Number=.,Type=Integer,Description=\"Allele Depth\">", AlleleDepthFormat);
            _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"Variant Frequency\">", VariantFrequencyFormat);

            if (_config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Integer,Description=\"Applied BaseCall Noise Level\">", NoiseLevelFormat);
                _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"StrandBias Score\">", StrandBiasFormat);
            }

            if (_config.ShouldOutputNoCallFraction)
                _writer.WriteLine("##FORMAT=<ID={0},Number=1,Type=Float,Description=\"Fraction of bases which were uncalled or with basecall quality below the minimum threshold\">", FractionNoCallFormat);

            WriteContigs(_writer);
            WriteColHeaders(_writer);
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

        private void WriteVariant(StreamWriter writer, BaseCalledAllele variant)
        {
            try
            {
                string[] formatAndSampleString = ConstructFormatAndSampleString(variant);

                //CHROM
                writer.Write(variant.Chromosome + "\t");
                //POS
                writer.Write(variant.Coordinate + "\t");
                //ID
                writer.Write("." + "\t");
                //REF
                writer.Write(variant.Reference + "\t");
                //ALT
                if ((variant.Genotype == Genotype.HomozygousRef)
                    || (variant.Genotype == Genotype.RefLikeNoCall))
                    //note, nocall is only used for low-depth regions where we do not try to var-call.
                    writer.Write("." + "\t");
                else
                {
                    writer.Write((variant.Alternate ?? ".") + "\t");
                }
                //QUAL
                writer.Write(variant.Qscore + "\t");
                //FILTER
                writer.Write(MapFilters(variant.Filters) + "\t");
                //INFO
                writer.Write(DepthInfo + "=" + variant.TotalCoverage + "\t");
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

        private void OnException(Exception ex)
        {
            _bufferList.Clear(); // dont care about list, clear now so we dont try to flush again later

            Dispose();
            File.Delete(_outputFilePath);

            // throw again
            throw ex;
        }

        private string MapFilters(List<FilterType> filters)
        {
            return filters.Any() ? string.Join(";", filters.Select(MapFilter)) : PassFilter;
        }

        private string MapFilter(FilterType filter)
        {
            switch (filter)
            {
                case FilterType.LowQscore:
                    if (!_config.QscoreFilterThreshold.HasValue)
                        throw new Exception("Variant has low qscore filter but threshold is not set.");
                    return "q" + _config.QscoreFilterThreshold.Value;
                case FilterType.StrandBias:
                    return StrandBiasFormat;
                default:
                    return "LowDP"; // LowDepth left
            }
        }

        private string MapGenotype(Genotype genotype)
        {
            switch (genotype)
            {
                case Genotype.HomozygousAlt:
                    return "1/1";
                case Genotype.HomozygousRef:
                    return "0/0";
                case Genotype.RefLikeNoCall:
                    return "./.";
                case Genotype.AltLikeNoCall:
                    return "./.";
                default:
                    return "0/1";
            }
        }

        private string[] ConstructFormatAndSampleString(BaseCalledAllele variant)
        {
            var gtString = MapGenotype(variant.Genotype);

            var isReference = variant is CalledReference;

            var alleleCountString = isReference
                ? variant.AlleleSupport.ToString()
                : string.Format("{0},{1}", ((CalledVariant)variant).ReferenceSupport, variant.AlleleSupport);

            var frequencyString = (isReference ? (1 - variant.Frequency) : variant.Frequency).ToString(_frequencySigFigFormat);

            var formatStringBuilder = new StringBuilder("GT:GQ:AD:VF");
            var sampleStringBuilder = new StringBuilder(string.Format("{0}:{1}:{2}:{3}", gtString, variant.Qscore, alleleCountString, frequencyString));

            if (_config.ShouldOutputStrandBiasAndNoiseLevel)
            {
                var biasScoreString = (Math.Min(Math.Max(MinStrandBiasScore, variant.StrandBiasResults.GATKBiasScore), MaxStrandBiasScore)).ToString("0.0000");

                formatStringBuilder.Append(":NL:SB");
                sampleStringBuilder.Append(string.Format(":{0}:{1}", _config.EstimatedBaseCallQuality, biasScoreString));
            }

            if (_config.ShouldOutputNoCallFraction)
            {
                var noCallFractionString = variant.FractionNoCalls.ToString("0.0000");

                formatStringBuilder.Append(":NC");
                sampleStringBuilder.Append(string.Format(":{0}", noCallFractionString));
            }

            return new []
            {
                formatStringBuilder.ToString(),
                sampleStringBuilder.ToString()
            };
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                FlushBuffer();
                
                _writer.Close();
                _writer.Dispose();

                _writer = null;
            }
        }
    }

    public struct VcfWriterConfig
    {
        public bool ShouldOutputNoCallFraction { get; set; }
        public bool ShouldOutputStrandBiasAndNoiseLevel { get; set; }
        public bool ShouldFilterOnlyOneStrandCoverage { get; set; }

        public int? QscoreFilterThreshold { get; set; }
        public int? DepthFilterThreshold { get; set; }
        public float? StrandBiasFilterThreshold { get; set; }
        public float FrequencyFilterThreshold { get; set; }

        public int EstimatedBaseCallQuality { get; set; }
    }

    public struct VcfWriterInputContext
    {
        public string ReferenceName { get; set; }
        public string SampleName { get; set; }
        public string CommandLine { get; set; }
        public IEnumerable<Tuple<string, long>> ContigsByChr { get; set; } 
    }
}