using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Pisces.Domain;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Logic
{
    public interface IStrandBiasFileWriter : IDisposable
    {
        void WriteHeader();
        void Write(IEnumerable<CalledAllele> BaseCalledAlleles);
    }

    public class StrandBiasFileWriter : IStrandBiasFileWriter
    {
        private StreamWriter _writer;
        private readonly string _outputFilePath;

        public StrandBiasFileWriter(string vcfFilePath)
        {
            _outputFilePath = GetBiasFilePath(vcfFilePath);

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(_outputFilePath)))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_outputFilePath));
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException(string.Format("Failed to create the Output Folder: {0}", _outputFilePath));
                    }
                }
                _writer = new StreamWriter(new FileStream(_outputFilePath, FileMode.Create));
            }
            catch (Exception)
            {
                throw new IOException(String.Format("Failed to create {0} in the specified folder.", _outputFilePath));
            }
            
        }

        public void WriteHeader()
        {
            if (_writer == null)
                throw new IOException("Stream already closed");
            string header = "Chr\tPosition\tReference\tAlternate\t" + ToColHeaders();
            _writer.WriteLine(header);
        }

        public static string GetBiasFilePath(string vcfPath)
        {
            //return Path.GetFileNameWithoutExtension(vcfPath)+".ReadStrandBias.txt";
            return vcfPath.Replace(".vcf", ".ReadStrandBias.txt");
        }

        public static void PrintBiasStats(StreamWriter writer, CalledAllele variant)
        {

            if (variant.ReferenceAllele == variant.AlternateAllele)
                return; //skip ref calls. 

            var strandBiasResults = StatsToString(variant.StrandBiasResults);
            StringBuilder sb = new StringBuilder(string.Format("{0}\t{1}\t{2}\t{3}\t",
                                                                variant.Chromosome, variant.ReferencePosition, variant.ReferenceAllele,
                                                                variant.AlternateAllele));
            sb.Append(strandBiasResults);
            writer.WriteLine(sb.ToString());

        }

        public void Write(IEnumerable<CalledAllele> BaseCalledAlleles)
        {
            if (_writer == null)
                throw new IOException("Stream already closed");

            foreach (var variant in BaseCalledAlleles)
                PrintBiasStats(_writer, variant);
        }

        public static string ToColHeaders()
        {
            StringBuilder builder = new StringBuilder();
            string[] s = new string[4 * 5];

            HeaderHelper(s, 0, 3, "Overall_", "\t");
            HeaderHelper(s, 1, 3, "Forward_", "\t");
            HeaderHelper(s, 2, 3, "Reverse_", "\t");

            foreach (string t in s)
            {
                builder.Append(t);
            }

            for (int i = 0; i < Constants.NumDirectionTypes; i++)
                builder.Append("RawCoverageCountByReadType_" + i + "\t");

            for (int i = 0; i < Constants.NumDirectionTypes; i++)
                builder.Append("RawSupportCountByReadType_" + i + "\t");


            builder.Append("BiasScore\t");
            builder.Append("BiasAcceptable?\t");
            builder.Append("VarPresentOnBothStrands?\t");
            builder.Append("CoverageAvailableOnBothStrands?\t");
            return (builder.ToString());
        }

        private static void StringHelper(StrandBiasStats stat, string[] s, int i, int d, string suffix)
        {
            s[i + 0 * d] = stat.ChanceFalsePos + suffix;
            s[i + 1 * d] = stat.ChanceFalseNeg + suffix;
            s[i + 2 * d] = stat.Frequency + suffix;
            s[i + 3 * d] = stat.Support + suffix;
            s[i + 4 * d] = stat.Coverage + suffix;
        }

        private static void HeaderHelper(string[] s, int i, int d, string prefix, string suffix)
        {
            s[i + 0 * d] = prefix + "ChanceFalsePos" + suffix;
            s[i + 1 * d] = prefix + "ChanceFalseNeg" + suffix;
            s[i + 2 * d] = prefix + "Freq" + suffix;
            s[i + 3 * d] = prefix + "Support" + suffix;
            s[i + 4 * d] = prefix + "Coverage" + suffix;
        }

        public static string StatsToString(BiasResults stat)
        {
            var delimiter = "\t";
            StringBuilder builder = new StringBuilder();
            string[] statsData = new string[3 * 5];

            StringHelper(stat.OverallStats, statsData, 0, 3, delimiter);
            StringHelper(stat.ForwardStats, statsData, 1, 3, delimiter);
            StringHelper(stat.ReverseStats, statsData, 2, 3, delimiter);

            foreach (string t in statsData)
            {
                builder.Append(t);
            }

            //raw data
            //for (int i = 0; i < Constants.NumDirectionTypes; i++)
            //    builder.Append(stat.CoverageByStrandDirection[i] + "\t");
            builder.Append(stat.ForwardStats.Coverage + delimiter);
            builder.Append(stat.ReverseStats.Coverage + delimiter);
            builder.Append(stat.StitchedStats.Coverage + delimiter);

            //for (int i = 0; i < Constants.NumDirectionTypes; i++)
            //    builder.Append(SupportByStrandDirection[i] + "\t");
            builder.Append(stat.ForwardStats.Support + delimiter);
            builder.Append(stat.ReverseStats.Support + delimiter);
            builder.Append(stat.StitchedStats.Support + delimiter);

            //results

            builder.Append(stat.BiasScore + delimiter);
            builder.Append(stat.BiasAcceptable + delimiter);

            builder.Append(stat.VarPresentOnBothStrands + delimiter);
            builder.Append(stat.CovPresentOnBothStrands + delimiter);
            return (builder.ToString());
        }


        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();

                _writer = null;
            }
        }
    }
}
