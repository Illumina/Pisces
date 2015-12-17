using System;
using System.IO;
using System.Reflection;
using SequencingFiles;

namespace CallSomaticVariants.Utility
{
    public class Logger
    {
        #region members

        private static StreamWriter _sw;
        private static bool _ready;

        public static bool GeneralLogReady
        {
            get { return _ready; }
            set { _ready = value; }
        }

        #endregion

        #region opening

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public static bool TryOpenLog(string logDir, string logFilePath)
        {
            lock (typeof(Logger))
            {
                if (!_ready)
                {
                    if (!logFilePath.Contains(logDir))
                        logFilePath = Path.Combine(logDir, logFilePath);
                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);

                    _sw = new StreamWriter(logFilePath, true);
                    _ready = true;
                    _sw.WriteLine();
                    Write(("*************starting Somatic Variant Caller**************"), _sw, ref _ready);
                    Write("Version:  " + Assembly.GetExecutingAssembly().GetName().Version, _sw, ref _ready);
                }
                return _ready;
            }
        }

        #endregion

        #region closing

        //note, the try-catches for these methods will all happen upstream, in the output class.
        //(The output class has the io-locker, that will allow any errors to be visibly output & logged to the user)
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public static bool TryCloseLog()
        {
            lock (typeof(Logger))
            {
                if (_ready)
                {
                    Write(("********************ending Somatic Variant Caller*********************"), _sw, ref _ready);
                    _sw.Close();
                    _ready = false;
                }

                return (!_ready);
            }
        }

        #endregion

        #region writing to files

        public static bool WriteToLog(string message)
        {
            lock (typeof(Logger))
            {
                return Write(message, _sw, ref _ready);
            }
        }

        public static bool WriteToLog(string message, params object[] args)
        {
            lock (typeof(Logger))
            {
                return Write(string.Format(message, args), _sw, ref _ready);
            }
        }

        public static bool WriteExceptionToLog(Exception ex)
        {
            lock (typeof(Logger))
            {
                return WriteToLog("Exception reported:  \n" + ex);
            }
        }

        // /////////////////////////////////////////////////////////////////
        private static bool Write(string message, StreamWriter sw, ref bool ready)
        {
            if (!ready) return false;

            try
            {
                message = message.TrimEnd('\n');

                string dot = message.EndsWith(".") || message.EndsWith("*") ? "" : ".";

                message = string.Format(
                    "{0} {1} {4}  {2}{3}",
                    DateTime.Today.ToShortDateString(),
                    DateTime.Now.ToLongTimeString(),
                    message,
                    dot,
                    System.Threading.Thread.CurrentThread.ManagedThreadId);

                Console.WriteLine(message);

                if ((message.ToLower().Contains("error")) ||
                    (message.ToLower().Contains("exception")))
                    Console.Error.WriteLine(message);

                sw.WriteLine(message);
                sw.Flush();
                return true;
            }
            catch
            {
                ready = false;
                return false;
            }
        }

        #endregion
    }
}