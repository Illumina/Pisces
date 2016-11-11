using System;
using System.IO;
using System.Reflection;
using NDesk.Options;
using Pisces.Domain.Utility;
using Pisces.Processing.Utility;

namespace Stitcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var programOptions = new ApplicationOptions(args);

	        if (programOptions.ShowVersion)
	        {
		        ShowVersion();
		        return;
	        }

            if (!Directory.Exists(programOptions.OutFolder))
            {
                try
                {
                    //lets be nice...
                    Directory.CreateDirectory(programOptions.OutFolder);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Environment.NewLine + "Validation Error: Unable to create the OutFolder.");
                    Console.WriteLine(ex);
                }
            }

            var doExit = programOptions.InputBam == null || programOptions.OutFolder == null || !File.Exists(programOptions.InputBam) || !Directory.Exists(programOptions.OutFolder);

            if (doExit)
            {
                ShowHelp(programOptions.OptionSet);
                var userException = new Exception(Environment.NewLine + "Validation Error: You must supply a valid Bam and OutFolder.");
				Logger.WriteExceptionToLog(userException);
	            throw userException;
            }


            InitializeLog(programOptions.OutFolder, string.Join(" ", args), programOptions.StitcherOptions.LogFileName);

            try
            {
                var processor = programOptions.StitcherOptions.ThreadByChromosome ? (IStitcherProcessor)new GenomeProcessor(programOptions.InputBam) : new BamProcessor();
                processor.Process(programOptions.InputBam, programOptions.OutFolder, programOptions.StitcherOptions);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception("Unable to process: " + ex.Message, ex);
                Logger.WriteExceptionToLog(wrappedException);

                throw wrappedException;
            }
            finally
            {
                Logger.TryCloseLog();
            }


        }

        static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: Stitcher.exe [PARAMETERS]");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            options.WriteOptionDescriptions(Console.Out);
        }

	    static void ShowVersion()
	    {
			var currentAssembly = Assembly.GetExecutingAssembly().GetName();
			Console.WriteLine(currentAssembly.Name + " " + currentAssembly.Version);
			Console.WriteLine(UsageInfoHelper.GetWebsite());
			Console.WriteLine();
		}

        static void InitializeLog(string outputDirectory, string commandLine, string logFileName)
        {
            Logger.TryOpenLog(outputDirectory, logFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(commandLine);
        }
    }
}
