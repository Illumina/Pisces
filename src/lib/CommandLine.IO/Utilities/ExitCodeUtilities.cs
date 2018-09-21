using System;
using System.Collections.Generic;
using System.IO;

namespace CommandLine.Util
{
    public static class ExitCodeUtilities
    {
        #region members

        private static readonly Dictionary<Type, int> ExceptionsToExitCodes;
        private static readonly HashSet<Type> UserFriendlyExceptions;

        #endregion

        // constructor
        static ExitCodeUtilities()
        {
            // add the exception to exit code mappings
            ExceptionsToExitCodes = new Dictionary<Type, int>
            {
                { typeof(ArgumentNullException),              (int)ExitCodeType.BadArguments },
                { typeof(ArgumentOutOfRangeException),        (int)ExitCodeType.BadArguments },
                { typeof(Exception),                          (int)ExitCodeType.InvalidFunction },
                { typeof(FileNotFoundException),              (int)ExitCodeType.FileNotFound },
                { typeof(FormatException),                    (int)ExitCodeType.BadFormat },
                { typeof(InvalidDataException),               (int)ExitCodeType.InvalidData },
                { typeof(InvalidOperationException),          (int)ExitCodeType.InvalidFunction },
                { typeof(NotImplementedException),            (int)ExitCodeType.CallNotImplemented },
                { typeof(UnauthorizedAccessException),        (int)ExitCodeType.AccessDenied },
                { typeof(OutOfMemoryException),               (int)ExitCodeType.OutofMemory },
            };

            // define which exceptions should not include a full stack trace
            UserFriendlyExceptions = new HashSet<Type>
            {
               typeof(UnauthorizedAccessException),
               typeof(OutOfMemoryException),
            };
        }

        /// <summary>
        /// Displays the details behind the exception
        /// </summary>
        public static int ShowExceptionAndUpdateExitCode(Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("\nERROR: ");
            Console.ResetColor();

            Console.WriteLine("{0}", e.Message);

            var exceptionType = e.GetType();

            if (!UserFriendlyExceptions.Contains(exceptionType))
            {
                // print the stack trace
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nStack trace:");
                Console.ResetColor();
                Console.WriteLine(e.StackTrace);

                // extract out the vcf line
                if (e.Data.Contains("VcfLine"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nVCF line:");
                    Console.ResetColor();
                    Console.WriteLine(e.Data["VcfLine"]);
                }
            }

            // return a non-zero exit code
            int exitCode;
            if (!ExceptionsToExitCodes.TryGetValue(exceptionType, out exitCode)) exitCode = 1;
            return exitCode;
        }
    }
}
