using Common.IO.Utility;
using CommandLine.VersionProvider;
using CommandLine.Util;
using CommandLine.Application;

namespace Psara
{
    public class Program : BaseApplication<PsaraOptions>
    {
        static string _commandlineExample = " --vcf <vcf path>";
        static string _programDescription = "Psara: post-processing filter";
        static string _programName = "Psara";


        public Program(string programDescription, string commandLineExample, string programAuthors, string programName,
            IVersionProvider versionProvider = null) : base(programDescription, commandLineExample, programAuthors, programName, versionProvider = null)
        {
            _options = new PsaraOptions();
            _appOptionParser = new PsaraOptionsParser();
        }


        public static int Main(string[] args)
        {

            Program psara = new Program(_programDescription, _commandlineExample, UsageInfoHelper.GetWebsite(), _programName);
            psara.DoParsing(args);
            psara.Execute();

            return psara.ExitCode;
        }

        //wrapper should now handle all throwing and catching..
        protected override void ProgramExecution()
        {
            VcfFilter.DoFiltering(_options);
            Logger.WriteToLog("filtering complete.");  
        }

   
    }
}
