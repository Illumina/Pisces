using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Common.IO.Sequencing;
using CommandLine.VersionProvider;
using CommandLine.IO;
using CommandLine.IO.Utilities;
using Pisces.IO;

namespace CreateGenomeSizeFile
{
    public class Program : BaseApplication
    {
        private GenomeSizeOptions _options;
        static string _commandlineExample = "-s <species name and build> -g <genome path> -out <output folder>";
        static string _programDescription = "CreateGenomeSizeFile: create a genome size xml file from a fasta file.";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }

        public static int Main(string[] args)
        {

            Program app = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            app.DoParsing(args);
            app.Execute();

            return app.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new GenomeSizeOptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _options = ((GenomeSizeOptionsParser)ApplicationOptionParser).Options;
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }

        protected override void Init()
        {
            //
        }

        protected override void Close()
        {
            //
        }

        protected override void ProgramExecution()
        {
            GenomeMetadata.CheckReferenceGenomeFolderState(_options.InputFastaFolder, false, false);
       
            Console.WriteLine("Preparing GenomeSize.xml for folder {0}...", _options.OutputDirectory);
            GenomeMetadata metadata = new GenomeMetadata();
            metadata.ImportFromFastaFilesAndCreateIndexAndDict(_options.InputFastaFolder, _options.OutputDirectory);
            metadata.Name = _options.SpeciesName;
            string genomeSizePath = Path.Combine(_options.OutputDirectory, "GenomeSize.xml");

            if (File.Exists(genomeSizePath))
            {
                throw new ArgumentException("GenomeSize.xml already exists on " + _options.OutputDirectory);
            }

            metadata.Serialize(genomeSizePath);
            Console.WriteLine("GenomeSize.xml prepared at {0}", genomeSizePath);
        }

    }
}