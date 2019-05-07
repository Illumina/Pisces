using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;

namespace Common.IO.Utility
{
    public class Logger
    {
        private static string _logfileName;// keep this empty by default => if its not set, we skip writing to the log. 
        private static int _shortWaitTime = 6;
        private static int _waitTimeMultiplier = 10000; //only applicable to tests using the log
        private static AutoResetEvent _unitTestToken = new AutoResetEvent(true);

        #region opening

        private static void WaitForReset()
        {

            bool gotToken = false;

            for (int i = 0; i < _waitTimeMultiplier; i++)
            {
                gotToken = _unitTestToken.WaitOne(_shortWaitTime);

                if (gotToken)
                    break;
                else
                    Thread.Sleep(_shortWaitTime);
            }

            if (gotToken == false)
            {
                //presumably, we have a hanging unit test..
                throw new IOException("Unable to acquire the log, after waiting " + _shortWaitTime + " seconds. Lock held by: " + _logfileName);
            }

        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public static bool OpenLog(string logDir, string logFilePath, bool shouldLockForInstance = false)
        {
            //When ever the logger is run in the test framework,
            //we want to InstanceLock the log with a reset event, so only one unit test can open/close to the log 
            //at a time. We dont want different unit tests closing each other's log.
            //When Pisces.exe is running, this is not an issue. Pisces isnt going to try to open the same log twice.

            WaitForReset();

            lock (typeof(Logger))
            {

                var oldlogFilePath = _logfileName;

                if (!logFilePath.Contains(logDir))
                    logFilePath = Path.Combine(logDir, logFilePath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("Unable to create log directory path: " + logDir);
                        return false;
                    }
                }
                _logfileName = logFilePath;
                Write(("************* Starting **************"));

                Write("Version:  " + Assembly.GetEntryAssembly().GetName().Version);


                return true;

            }
        }

        #endregion

        #region closing

        //note, the try-catches for these methods will all happen upstream, in the output class.
        //(The output class has the io-locker, that will allow any errors to be visibly output & logged to the user)
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public static bool CloseLog()
        {

            lock (typeof(Logger))
            {
                Write("******************** Ending *********************");            
                _unitTestToken.Set();
            }
            return true;
        }

 

        #endregion

        #region writing to files


        public static bool WriteToLog(string message, params object[] args)
        {
            lock (typeof(Logger))
            {
                return Write(string.Format(message, args));
            }
        }

        public static bool WriteWarningToLog(string message, params object[] args)
        {
            lock (typeof(Logger))
            {
                return Write(string.Format("WARNING:   " + message, args));
            }
        }


        public static bool WriteExceptionToLog(Exception ex)
        {
            lock (typeof(Logger))
            {
                return WriteToLog("Exception reported:  \n" + ex);
            }
        }

        public static bool WriteProcessToLog(string processName, string message, params object[] args)
        {
            lock (typeof(Logger))
            {
                return Write(string.Format("PROCESS " + processName + ": " + message, args));
            }
        }

        public static void AppendRaw(string line)
        {
            lock (typeof(Logger))
            {
                File.AppendAllText(_logfileName, line + Environment.NewLine);
            }
        }

        // /////////////////////////////////////////////////////////////////
        internal static bool Write(string message)
        {

            try
            {
                if (message != null)
                {

                    message = message.TrimEnd('\n');

                    string dot = message.EndsWith(".") || message.EndsWith("*") ? "" : ".";

                    message = string.Format(
                        "{0} {1} {4}  {2}{3}",
                        DateTime.Now.ToString("d"),
                        DateTime.Now.ToString("t"),
                        message,
                        dot,
                        Thread.CurrentThread.ManagedThreadId);

                    if ((message.ToLower().Contains("error")) ||
                        (message.ToLower().Contains("exception")))
                        Console.Error.WriteLine(message);
                    else
                    {
                        Console.WriteLine(message);
                    }


                    int attempts = 0;
                    int attempsAllowed = 10;
                    List<Exception> issues = new List<Exception>();
                    while (true)
                    {
                        try
                        {
                            lock (typeof(Logger))
                            {
                                if (String.IsNullOrEmpty(_logfileName))
                                    break;

                                if (issues.Count > 0)
                                    File.AppendAllText(_logfileName, "past logging issues:" + Environment.NewLine);

                                foreach (var ex in issues)
                                    File.AppendAllText(_logfileName, ex + Environment.NewLine);


                                File.AppendAllText(_logfileName, message + Environment.NewLine);
                                break;
                            }

                        }
                        catch (Exception ex)
                        {
                            issues.Add(ex);
                            Console.WriteLine(ex);
                            Thread.Sleep(_shortWaitTime);
                        }

                        attempts++;

                        if (attempts > attempsAllowed)
                        {
                            Console.WriteLine("Pisces logging failure.");
                            foreach (var ex in issues)
                                Console.WriteLine(ex);
                            break;
                        }

                    }

                }
                else
                    return false;

            }
            catch
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}