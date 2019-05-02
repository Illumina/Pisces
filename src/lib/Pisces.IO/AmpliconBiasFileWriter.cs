using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.IO
{
    public interface IAmpliconBiasFileWriter : IDisposable
    {
        void WriteHeader();
        void Write(IEnumerable<CalledAllele> BaseCalledAlleles);
    }

    public class AmpliconBiasFileWriter : IAmpliconBiasFileWriter
    {
        private StreamWriter _writer;
        private readonly string _outputFilePath;

        static string NewCol = ",";
        
        public AmpliconBiasFileWriter(string vcfFilePath)
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
            string header = "Chr,Position,Reference,Alternate," + ToColHeaders();
            _writer.WriteLine(header);
        }

        public static string GetBiasFilePath(string vcfPath)
        {
            return vcfPath.Replace(".vcf", ".AmpliconBias.csv");
        }

        public static void PrintBiasStats(StreamWriter writer, CalledAllele variant)
        {
            if (variant.Type == Domain.Types.AlleleCategory.Reference)
                return;

            var  variantString = new StringBuilder(string.Format("{0},{1},{2},{3},",
                                                          variant.Chromosome, variant.ReferencePosition, variant.ReferenceAllele,
                                                          variant.AlternateAllele));
           
            var biasData = variant.AmpliconBiasResults;
            if (biasData != null)
            {
                foreach (var amplicon in biasData.ResultsByAmpliconName.Keys)
                {
                    var biasOutput = AmpliconBiasDataToString(biasData.ResultsByAmpliconName[amplicon], biasData.BiasDetected, "QBIAS_FOR");

                    writer.WriteLine(variantString + biasOutput);
                }
            }
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
            var headers = "Name,freq,obs support, expected support, prob its real, confidence Qscore, bias detected?, Filter Variant?";
            return headers;
        }

        /// <summary>
        /// Name, freq, obs spt, expected spt, bias prob, BiasQscore, bias detected, full string
        /// </summary>
        /// <param name="resultForAmplicon"></param>
        /// <param name="nameToPrePend"></param>
        /// <returns></returns>
        public static string AmpliconBiasDataToString(AmpliconBiasResult resultForAmplicon, bool shouldFilter, string nameToPrePend)
        {
            StringBuilder sb = new StringBuilder();


            if ((resultForAmplicon == null) ||
                (resultForAmplicon.Name == null))
                return null;
        
            sb.Append(resultForAmplicon.Name + NewCol);
            sb.Append(resultForAmplicon.Frequency + NewCol);
            sb.Append(resultForAmplicon.ObservedSupport + NewCol);
            sb.Append(resultForAmplicon.ExpectedSupport + NewCol);
            sb.Append(resultForAmplicon.ChanceItsReal + NewCol);
            sb.Append(resultForAmplicon.ConfidenceQScore + NewCol);
            sb.Append(resultForAmplicon.BiasDetected + NewCol);
            sb.Append(shouldFilter + NewCol);
            
            return sb.ToString();

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
