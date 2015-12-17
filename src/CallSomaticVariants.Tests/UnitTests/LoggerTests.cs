using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallSomaticVariants.Utility;
using Xunit;

namespace CallSomaticVariants.Tests.UnitTests
{
    public class LoggerTests
    {
        private const string LOG_FILENAME = "TestLog.txt";

        [Fact]
        public void OpenAndClose()
        {
            // --------------------------------------------
            // Happy path - create log in new directory, and close
            // ---------------------------------------------
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestLogDir");
            var logFilePath = Path.Combine(logDir, LOG_FILENAME);

            if (Directory.Exists(logDir))
                Directory.Delete(logDir, true);

            Logger.TryOpenLog(logDir, LOG_FILENAME);
            Assert.True(Logger.GeneralLogReady);

            Assert.True(Logger.TryCloseLog());
            Assert.True(File.Exists(logFilePath));
            Assert.False(Logger.GeneralLogReady);

            VerifyLog(logFilePath, 1, 1);

            // --------------------------------------------
            // Open existing log, and close - should append
            // ---------------------------------------------
            Logger.TryOpenLog(logDir, LOG_FILENAME);
            Assert.True(Logger.TryCloseLog());

            VerifyLog(logFilePath, 2, 2);

            // --------------------------------------------
            // Trying to close twice is fine, no additional thing written
            // --------------------------------------------
            Assert.True(Logger.TryCloseLog());
            VerifyLog(logFilePath, 2, 2);

            // --------------------------------------------
            // Trying to open twice is fine, no additional thing written
            // --------------------------------------------
            Assert.True(Logger.TryOpenLog(logDir, LOG_FILENAME));
            Assert.True(Logger.TryOpenLog(logDir, LOG_FILENAME));
            Logger.TryCloseLog();

            VerifyLog(logFilePath, 3, 3);

            // --------------------------------------------
            // Opening with full path in filename is fine
            // --------------------------------------------
            Assert.True(Logger.TryOpenLog(logDir, logFilePath));
            Logger.TryCloseLog();

            VerifyLog(logFilePath, 4, 4);
        }

        [Fact]
        public void Writing()
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestLogDir2");
            var logFilePath = Path.Combine(logDir, LOG_FILENAME);

            if (Directory.Exists(logDir))
                Directory.Delete(logDir, true);

            Logger.TryOpenLog(logDir, logFilePath);
            Logger.WriteToLog("Test1");
            Logger.WriteToLog("Test {0}{1}", 2, 3);
            Logger.WriteExceptionToLog(new Exception("Exception!"));
            Logger.TryCloseLog();

            var logContent = File.ReadAllLines(logFilePath);
            Assert.Equal(1, logContent.Count(l => l.Contains("Test1")));
            Assert.Equal(1, logContent.Count(l => l.Contains("Test 23")));
            Assert.Equal(1, logContent.Count(l => l.Contains("Exception!")));

            // --------------------------------------------
            // Trying to write when not ready is ok, but nothing makes it to file
            // --------------------------------------------
            Assert.False(Logger.WriteToLog("NotReady"));

            logContent = File.ReadAllLines(logFilePath);
            Assert.False(logContent.Any(l => l.Contains("NotReady")));
        }


        private void VerifyLog(string logFilePath, int expectedStartingLines, int expectedEndingLines)
        {
            var logContent = File.ReadAllLines(logFilePath);
            Assert.Equal(expectedStartingLines, logContent.Count(l => l.Contains("*****starting")));
            Assert.Equal(expectedEndingLines, logContent.Count(l => l.Contains("*****ending")));
            Logger.TryCloseLog();
        }
    }
}
