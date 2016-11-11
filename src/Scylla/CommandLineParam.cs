using System;
using System.IO;
using System.Reflection;
using Pisces.Domain.Utility;
using Pisces.Domain.Types;
using Pisces.Calculators;
using VariantPhasing;

namespace Scylla
{
    public class CommandLineParameters
    {
        private const char _delimiter = ',';

        public static void PrintVersionToConsole()
        {
            var currentAssembly = Assembly.GetExecutingAssembly().GetName();
            Console.WriteLine(currentAssembly.Name + " " + currentAssembly.Version);
            Console.WriteLine(UsageInfoHelper.GetWebsite());
            Console.WriteLine();
        }

        public static void PrintUsageInfo()
        {
            PrintVersionToConsole();

            Console.WriteLine("-ver/-v : Print version.");
            Console.WriteLine("-help : Print help.");
            Console.WriteLine();
            Console.WriteLine("Required arguments:");
            Console.WriteLine("-vcf : Path to input vcf file.");
            Console.WriteLine("-bam : Path to bam file.");
            Console.WriteLine();
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("-Dist : How close variants need to be to chain together. Should be less than read length.");
            Console.WriteLine("-Out : Output directory. Default is to return results to vcf path.");
            Console.WriteLine("-PassingVariantsOnly : Whether only passing variants should be allowed to phase, 'true' or 'false'. Default, true");
            Console.WriteLine("-ClusterConstraint : Constrain the number of clusters to this number, if possible. Analogous to forced ploidy.");
            Console.WriteLine("-AllowClusterMerging : Whether clusters should be allowed to merge, 'true' or 'false'. Default, true");
            Console.WriteLine("-AllowWorstFitRemoval : Whether a cluster should try to remove and reassign its worst fit, 'true' or 'false'. Default, true");
            Console.WriteLine("-Chr : Array indicating which chromosomes to process (ie, [chr1,chr9]). If empty, all chromosomes will be processed. Default, empty (all)");
            Console.WriteLine("-MaxNbhdsToProcess : A debug option, an integer cap on the number of neighborhoods to process. If -1, all neighborhoods will be processed. Default, -1 (all)");
            Console.WriteLine("-Debug : Run the program in debug mode (additional logging).");
            Console.WriteLine("-t : Number of threads to use. Default, 10");
            Console.WriteLine("-b : Minimum base call quality. Cigar operations with quality below the minimum will be treated as 'N's. Default, 20");
            Console.WriteLine("-m : Minimum map quality to consider a read. Default, 1");
        }

        public static ApplicationOptions ParseAndValidateCommandLine(string[] arguments)
        {
            var options = ParseCommandLine(arguments);

            if (arguments.Length != 0 && options != null)
            {
                Validate(options);
            }
            return options;
        }

        public static ApplicationOptions ParseCommandLine(string[] arguments)
        {


            

            if ((arguments == null) || (arguments.Length == 0))
            {
                PrintUsageInfo();
                return null;
            }

            var options = new ApplicationOptions();

            var lastArgumentField = string.Empty;

            try
            {
                var argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (string.IsNullOrEmpty(arguments[argumentIndex]))
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1];

                    lastArgumentField = arguments[argumentIndex];

                    switch (lastArgumentField.ToLower())
                    {
                        case "-out":
                            options.OutFolder = value;
                            break;
                        case "-bam":
                            options.BamPath = value;
                            break;
                        case "-vcf":
                            options.VcfPath = value;
                            break;
                        case "-dist":
                            options.PhasableVariantCriteria.PhasingDistance = int.Parse(value);
                            break;
						case "-v":
                        case "-ver":
                            var currentAssembly = System.Reflection.Assembly.GetCallingAssembly().GetName();
                            Console.WriteLine("Version:\t" + currentAssembly.Version);
                            return null;
                        case "-h":
                        case "-help":
                            PrintUsageInfo();
                            return null;
                        case "-passingvariantsonly":
                            options.PhasableVariantCriteria.PassingVariantsOnly = bool.Parse(value);
                            break;
                        case "-hetvariantsonly":
                            options.PhasableVariantCriteria.HetVariantsOnly = bool.Parse(value);
                            break;
                        case "-allowclustermerging":
                            options.ClusteringParams.AllowClusterMerging = bool.Parse(value);
                            break;
                        case "-allowworstfitremoval":
                            options.ClusteringParams.AllowWorstFitRemoval = bool.Parse(value);
                            break;
                        case "-debug":
                            options.Debug = bool.Parse(value);
                            break;
                        case "-maxnbhdstoprocess":
                            options.PhasableVariantCriteria.MaxNumNbhdsToProcess = int.Parse(value);
                            break;
                        case "-t":
                        case "-maxnumthreads":
                            options.NumThreads = int.Parse(value);
                            break;
                        case "-minvariantqscore":
                            options.VariantCallingParams.MinimumVariantQScore = int.Parse(value);
                            break;
                        case "-minimumfrequency":
                            options.VariantCallingParams.MinimumFrequency = float.Parse(value);
                            break;
                        case "-variantqualityfilter":
                            options.VariantCallingParams.MinimumVariantQScoreFilter = int.Parse(value);
                            break;
                        case "-minvariantfrequencyfilter":
                            options.VariantCallingParams.MinimumFrequencyFilter = float.Parse(value);
                            break;
                        case "-b":
                        case "-minbasecallquality":
                            options.BamFilterParams.MinimumBaseCallQuality = int.Parse(value);
                            break;
                        case "-filterduplicates":
                            options.BamFilterParams.RemoveDuplicates = bool.Parse(value);
                            break;
                        case "-m":
                        case "-minmapquality":
                            options.BamFilterParams.MinimumMapQuality = int.Parse(value);
                            break;
                        case "-clusterconstraint":
                            options.ClusteringParams.ClusterConstraint = int.Parse(value);
                            break;
                        case "-ploidy":
                            if (value.ToLower().Contains("somatic"))
                                options.VariantCallingParams.PloidyModel = PloidyModel.Somatic;
                            else if (value.ToLower().Contains("diploid") || value == "2")
                                options.VariantCallingParams.PloidyModel = PloidyModel.Diploid;
                            else
                                throw new ArgumentException(string.Format("Unknown ploidy model '{0}'", value));
                            break;
                        case "-diploidgenotypeparameters":
                            var parameters = ParseStringToFloat(value.Split(_delimiter));
                            if (parameters.Length != 3)
                                throw new ArgumentException(string.Format("DiploidGenotypeParamteers argument requires exactly three values."));
                            options.VariantCallingParams.DiploidThresholdingParameters = new DiploidThresholdingParameters(parameters);
                            break;
                        case "-crushvcf":
                            bool crushedallelestyle = bool.Parse(value);
                            options.VcfWritingParams.AllowMultipleVcfLinesPerLoci = !(crushedallelestyle);
                            break;
                        case "-chr":
                            options.PhasableVariantCriteria.ChrToProcessArray = ListOfParamsToStringArray(value);
                            break;
                        case "-nbhd":
                            options.PhasableVariantCriteria.FilteredNbhdToProcess = value;
                            break;

                        default:
                            PrintUsageInfo();
                            throw new ArgumentException(string.Format("Unknown argument '{0}'", value));
                    }

                    argumentIndex += 2;
                }

                options.CommandLineArguments = string.Join(" ", arguments);
                options.LogFileName = Path.GetFileName(options.VcfPath).Replace(".genome.vcf", ".phased.genome.log");

                if (options.VariantCallingParams.PloidyModel == PloidyModel.Diploid)
                    options.VariantCallingParams.MinimumFrequency = options.VariantCallingParams.DiploidThresholdingParameters.MinorVF;

                if (options.VariantCallingParams.MinimumFrequencyFilter < options.VariantCallingParams.MinimumFrequency)
                    options.VariantCallingParams.MinimumFrequencyFilter = options.VariantCallingParams.MinimumFrequency;

                if (options.VariantCallingParams.MinimumVariantQScoreFilter < options.VariantCallingParams.MinimumVariantQScore)
                    options.VariantCallingParams.MinimumVariantQScoreFilter = options.VariantCallingParams.MinimumVariantQScore;


                return options;
            }
            catch (ArgumentException ex)
            {
                PrintUsageInfo();
                throw new Exception(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }

        }

        public static void Validate(ApplicationOptions options)
        {
            // Check for required fields.
            if (string.IsNullOrEmpty(options.BamPath) || string.IsNullOrEmpty(options.VcfPath))
            {
                throw new ArgumentNullException("Missing required parameters. " +
                                                "Minimal required parameters are: BAM path, VCF path.");
            }

            if (!File.Exists(options.BamPath))
                throw new ArgumentException(string.Format("BAM path '{0}' does not exist.", options.BamPath));

            if (!File.Exists(options.VcfPath))
                throw new ArgumentException(string.Format("VCF path '{0}' does not exist.", options.VcfPath));

            if (string.IsNullOrEmpty(options.OutFolder))
                options.OutFolder = Path.GetDirectoryName(options.VcfPath);

        }

        public static string[] ListOfParamsToStringArray(string param)
        {
            return param.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static float[] ParseStringToFloat(string[] stringArray)
        {
            var parameters = new float[stringArray.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                try
                {
                    parameters[i] = float.Parse(stringArray[i]);
                }
                catch
                {
                    throw new ArgumentException(string.Format("Unable to parse float type from " + stringArray[i]
                        + ".  Please check parameters."));
                }
            }

            return parameters;
        }


    }

}
