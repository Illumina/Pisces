using System;
using System.IO;
using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.NDesk.Options;
using CommandLine.Options;
using Pisces.Domain.Options;

namespace CreateGenomeSizeFile
{
    public class CreateGenomeSizeFileOptions : BaseApplicationOptions
    {
        public string InputFastaFolder;
        public string SpeciesName;

        public override string GetMainInputDirectory()
        {
            return Path.GetDirectoryName(InputFastaFolder);
        }
    }

    public class GenomeSizeOptionsParser : BaseOptionParser
    {

        public GenomeSizeOptionsParser()
        {
            Options = new CreateGenomeSizeFileOptions();
        }

        public CreateGenomeSizeFileOptions GSFOptions { get => (CreateGenomeSizeFileOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {

            var requiredOps = new OptionSet
            {
                {
                    "g=",
                    OptionTypes.FOLDER+ @" Genome folder.  Example folder structure: \\Genomes\Homo_sapiens\UCSC\hg19\Sequence\WholeGenomeFASTA",
                    value=> GSFOptions.InputFastaFolder = value
                },
                 {
                    "s=",
                    OptionTypes.STRING + " Species and build, in quotes. Example format: Genus Species (Source Build). - e.g. \"Rattus norvegicus (UCSC rn4)\"",
                    value=> GSFOptions.SpeciesName = value
                }
            };

            var commonOps = new OptionSet{
                {
                    "o|out|outfolder=",
                    OptionTypes.FOLDER + " output directory",
                    value=> GSFOptions.OutputDirectory = value
                },
           
            };


            var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
            };

            return optionDict;
        }


        public override void ValidateOptions()
        {
            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.GetDirectoryName(GSFOptions.InputFastaFolder);
            }

            if (!Directory.Exists(Options.OutputDirectory))
            {
                Directory.CreateDirectory(GSFOptions.OutputDirectory);
            }


            if (string.IsNullOrEmpty(GSFOptions.SpeciesName) || GSFOptions.SpeciesName.Split(' ').Length < 3)
            {
                Console.WriteLine("Please specify the full genome name (\"Genus Species (Source Build)\" - e.g. \"Rattus norvegicus (UCSC rn4)\"; include the strain name if available, e.g. \"Bacillus cereus ATCC 10987 (NCBI 2004-02-13)\").");
                ParsingResult.UpdateExitCode(CommandLine.Util.ExitCodeType.BadArguments);
            }
        }
    }
}
