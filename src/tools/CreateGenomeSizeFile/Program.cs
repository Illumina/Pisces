using System;
using System.IO;
using Common.IO.Sequencing;
using CommandLine.VersionProvider;
using CommandLine.Application;
using CommandLine.Util;

namespace CreateGenomeSizeFile
{
    public class Program : BaseApplication<CreateGenomeSizeFileOptions>
    {
        static string _commandlineExample = "-s <species name and build> -g <genome path> -out <output folder>";
        static string _programDescription = "CreateGenomeSizeFile: create a genome size xml file from a fasta file.";
        static string _programName = "CreateGenomeSizeFile";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new CreateGenomeSizeFileOptions();
            _appOptionParser = new GenomeSizeOptionsParser();
        }
        public static int Main(string[] args)
        {
            Program app = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            app.DoParsing(args);
            app.Execute();

            return app.ExitCode;
        }

     
        protected override void ProgramExecution()
        {
            GenomeMetadata.CheckReferenceGenomeFolderState(_options.InputFastaFolder, false, false);
       
            Console.WriteLine("Preparing GenomeSize.xml for folder {0}...", _options.OutputDirectory);
            GenomeMetadata metadata = new GenomeMetadata();
            metadata.ImportFromFastaFiles(_options.InputFastaFolder, _options.OutputDirectory);
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