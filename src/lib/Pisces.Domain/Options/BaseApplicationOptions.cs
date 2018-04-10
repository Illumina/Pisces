using System;
using Common.IO.Utility;

namespace Pisces.Domain.Options
{
    public abstract class BaseApplicationOptions
    {
        public string[] CommandLineArguments = new string[] { };

        public string QuotedCommandLineArgumentsString
        {
            get
            {
                return (string.Format("\"{0}\"", string.Join(' ', CommandLineArguments)));
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