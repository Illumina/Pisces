using System;
using System.Collections.Generic;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Util;

namespace VennVcf
{

    public class Program : BaseApplication<VennVcfOptions>
    {
        static string _commandlineExample = " -if [A.genome.vcf,B.genome.vcf] -o \\outfolder -consensus myConsensus2.vcf";
        static string _programDescription = "VennVcf: Gets the intersection and disjoint segmentation of two vcfs";
        static string _programName = "VennVcf";

        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
           IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {

            _options = new VennVcfOptions();
            _appOptionParser = new VennVcfOptionsParser();
        }

        public static int Main(string[] args)
        {

            Program vennVcf = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            vennVcf.DoParsing(args);
            vennVcf.Execute();

            return vennVcf.ExitCode;
        }

     
    

        protected override void ProgramExecution()
        {
            
            Console.WriteLine(">>> Processing files:");
            foreach (string vcfFile in _options.InputFiles)
            {
                Console.WriteLine(">>> \t" + vcfFile);
            }

            int numVcfs = _options.InputFiles.Length;

            Console.WriteLine(">>> starting Venn");

            //We used to have an option where we recursively diff'ed any number of files. 
            //We dropped it, as it did not get enough use to support.
            if (numVcfs == 2)
            {
                VennProcessor Venn = new VennProcessor(_options.InputFiles, _options);
                Venn.DoPairwiseVenn();
            }
            else
            {
                Console.WriteLine(">>> Exactly two vcf files are required. Number of vcfs found: " + numVcfs);
            }


            Console.WriteLine(">>> Work complete.");

        }

        //this was used when we diff'd any number of files
        /// <summary>
        /// This method was used when we diff'd any number of vcf/s
        /// </summary>
        /// <param name="ListOfFiles"></param>
        /// <returns></returns>
        private static List<string[]> GetPairs(string[] ListOfFiles)
        {
            int numVcfs = ListOfFiles.Length;
            List<string[]> Pairs = new List<string[]>();

            if (numVcfs > 2)
            {
                for (int i = 0; i < numVcfs; i++)
                {
                    for (int j = i + 1; j < numVcfs; j++)
                    {
                        Pairs.Add(new string[] { ListOfFiles[i], ListOfFiles[j] });
                    }
                }
            }
            return Pairs;
        }

    }

}
