using System;
using System.IO;
using Common.IO;
using Common.IO.Utility;

namespace Pisces.Domain.Options
{
   
    public class BaseApplicationOptions : IApplicationOptions
    {
        private string[] _commandLineArguments;
        private string _outputDirectory;
        private string _logDirectory;
        protected string _inputDirectory;

        //leave this null for now.
        //If you tried to be clever and get it from the entry assembly,
        //it will accidentally get filled in with "TestHost" when running unit tests.
        //ir will be populated by the user at the command line OR with program defaults by SetIODirectories
        protected string _defaultLogFolderName;
        protected string _defaultLogFileNameBase; 
    
   
        public string QuotedCommandLineArgumentsString
        {
            get
            {
                if (_commandLineArguments == null)
                    return "";

                return (string.Format("\"{0}\"", string.Join(' ', _commandLineArguments)));
            }
        }

       

        public string[] CommandLineArguments
        {
            get
            {
                return _commandLineArguments;
            }

            set
            {
                _commandLineArguments = value;
            }
        }

        public string LogFileNameBase
        {
            get
            {
                return _defaultLogFileNameBase;
            }
            set
            {
                _defaultLogFileNameBase = value;
            }
        }

   

        public string OutputDirectory
        {
            get
            {
                return _outputDirectory;
            }

            set
            {
                 _outputDirectory=value;
            }
        }


        public string LogFolder
        {

            get
            {
               return _logDirectory;
            }

            set
            {
                _logDirectory = value;
            }


        }


        //we could do this in the Getter/Setter but it would be inefficient
        public virtual string GetMainInputDirectory()
        {
            throw new NotImplementedException();

        }

            //we could do this in the Getter/Setter but it would be inefficient
            public void SetIODirectories(string programName)
        {
            //note, if these are NOT null, then they were explicitly set on the command line.
            //This method might be better moved to Validation.

            if (_defaultLogFolderName == null)
                _defaultLogFolderName = programName + "Logs";

            if (_defaultLogFileNameBase == null)
                _defaultLogFileNameBase = programName + "Log.txt";

            if (string.IsNullOrEmpty(_outputDirectory))
            {
                _outputDirectory = GetMainInputDirectory();
            }

            if (string.IsNullOrEmpty(_logDirectory))
            {
                _logDirectory = Path.Combine(OutputDirectory, _defaultLogFolderName);

                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
        }

        public void Save(string filepath)
        {
            try
            {
                JsonUtil.Save(filepath, this);
            }
            catch (Exception ex)
            {
                //This is a workaround, incase any applications (ie, stitcher!) are run in parrallel, directed to the same options file.
                //Once we centralize this init code, this can be cleaned up.
                Logger.WriteToLog("Warning, unable to save options file: " + filepath);
                Logger.WriteWarningToLog(ex.Message);
            }
        }


    }
}