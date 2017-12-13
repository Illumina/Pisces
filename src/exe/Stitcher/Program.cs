using System;
using System.IO;
using System.Reflection;
using NDOptions;
using Pisces.Domain.Utility;
using Common.IO.Utility;
using Common.IO;

namespace Stitcher
{
    public class Program
    {
        static void Main(string[] args)
        {
            var programOptions = new ApplicationOptions(args);

	        if (programOptions.ShowVersion)
	        {
		        ShowVersion();
		        return;
	        }

            if (string.IsNullOrEmpty(programOptions.OutFolder))
            {
                programOptions.OutFolder = Path.GetDirectoryName(programOptions.InputBam);
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
                    Console.WriteLine("Validation Error: Unable to create the OutFolder.");
                    Console.WriteLine(ex);
                }
            }

            var doExit = programOptions.InputBam == null || programOptions.OutFolder == null || !File.Exists(programOptions.InputBam) || !Directory.Exists(programOptions.OutFolder);

            if (doExit)
            {
                ShowHelp(programOptions.OptionSet);
                var userException = new ArgumentException("Validation Error: You must supply a valid Bam and OutFolder.");
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
                Logger.CloseLog();
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
            var currentAssemblyName = FileUtilities.LocalAssemblyName<Program>();
            var currentAssemblyVersion = FileUtilities.LocalAssemblyVersion<Program>();
            Console.WriteLine(currentAssemblyName + " " + currentAssemblyVersion);
			Console.WriteLine(UsageInfoHelper.GetWebsite());
			Console.WriteLine();
		}

        static void InitializeLog(string outputDirectory, string commandLine, string logFileName)
        {
            Logger.OpenLog(outputDirectory, logFileName);
            Logger.WriteToLog("Command-line arguments: ");
            Logger.WriteToLog(commandLine);
        }
    }
}
