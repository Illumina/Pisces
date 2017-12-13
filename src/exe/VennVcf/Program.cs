using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Pisces.IO.Sequencing;
using Pisces.Domain;

namespace VennVcf
{     
    public delegate void ErrorHandler(string message);

    class Program
    {
        //-if [A.genome.vcf,B.genome.vcf] -o pisces\Bugs\differences -consensus myConsensus2.vcf -Mfirst true

        static int Main(string[] arguments)
        {

            VennVcfOptions parameters = new VennVcfOptions();
            if (!parameters.ParseCommandLine(arguments))
            {
                VennVcfOptions.PrintUsageInfo();
                return 1;
            }
            try
            {
                ProcessFiles(parameters.InputFiles, parameters);
            }
            catch (Exception e)
            {
                Console.WriteLine("*** Error encountered: {0}", e);
            }
            Console.WriteLine(">>> Work complete.");
            return 0;
        }

  
        private static void ProcessFiles(string[] listOfFiles, VennVcfOptions parameters)
        {

            Console.WriteLine(">>> Processing files:");
            foreach (string vcfFile in listOfFiles)
            {
                Console.WriteLine(">>> \t" + vcfFile);
            }

            int numVcfs = listOfFiles.Length;


            Console.WriteLine(">>> starting Venn");

            if (numVcfs == 2)
            {
                VennProcessor Venn = new VennProcessor(listOfFiles, parameters);
                Venn.DoPairwiseVenn(parameters.VcfWritingParams.MitochondrialChrComesFirst);
            }
            else
            {
                Console.WriteLine(">>> too many files: " + numVcfs);   
            }
              

            

        }

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
