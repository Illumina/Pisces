using System;
using System.Collections.Generic;
using System.Text;
using CommandLine.IO.Utilities;


namespace CommandLine.IO
{
    public class CommandLineParseResult
    {
        public bool ShowHelpMenu;
        public bool ShowVersion;
        public int ExitCode;
        public string ErrorSpacer;
        public StringBuilder ErrorBuilder;
        public List<string> UnsupportedOps = new List<string>();
        public Exception Exception;

        /// <summary>
        /// sets the exit code. 
        /// </summary>
        public int UpdateExitCode(ExitCodeType NewExitCode)
        {
            if (ExitCode == 0)
            {
                ExitCode = (int) NewExitCode;
            }

            return ExitCode;
        }
    }
}
