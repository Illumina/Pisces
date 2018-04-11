using System.IO;
using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.IO;
using CommandLine.IO.Utilities;

namespace Psara
{
    public class Program : BaseApplication
    {
        private PsaraOptions _options;
        static string _commandlineExample = " --vcf <vcf path>";
        static string _programDescription = "Psara: post-processing filter";

        public Program(string programDescription, string commandLineExample, string programAuthors, IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, versionProvider = null) { }


        public static int Main(string[] args)
        {

            Program psara = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite());
            psara.DoParsing(args);
            psara.Execute();

            return psara.ExitCode;
        }

        public void DoParsing(string[] args)
        {
            ApplicationOptionParser = new PsaraOptionsParser();
            ApplicationOptionParser.ParseArgs(args);
            _options = ((PsaraOptionsParser) ApplicationOptionParser).PsaraOptions;

            //Psara is going to need these to write the output vcf.
            //We could tuck this line into the OptionsParser() constructor if we had a base options class. TODO, maybe.
            _options.CommandLineArguments = ApplicationOptionParser.CommandLineArguments;
        }

        protected override void Init()
        {
            Logger.OpenLog(_options.LogFolder, _options.LogFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(string.Join(" ", ApplicationOptionParser.CommandLineArguments));

            _options.Save(Path.Combine(_options.LogFolder, "PsaraOptions.used.json"));
        }

        protected override void Close()
        {
            Logger.CloseLog();
        }


        //wrapper should now handle all throwing and catching..
        protected override void ProgramExecution()
        {
            if (_options == null) return;

            VcfFilter.DoFiltering(_options);

            Logger.WriteToLog("filtering complete.");
            Logger.CloseLog();
        }

   
    }
}
