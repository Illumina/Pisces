using System;
using System.IO;
using System.Linq;
using System.Threading;
using Common.IO.Utility;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class LoggerTests
    {
        private const string LOG_FILENAME1 = "TestLog1.txt";
        private const string LOG_FILENAME2 = "TestLog.txt";
        [Fact]
        public void OpenAndClose()
        {
            // --------------------------------------------
            // Happy path - create log in new directory, and close
            // ---------------------------------------------
            var logDir = Path.Combine(TestPaths.LocalScratchDirectory, "TestLogDir_OpenAndClose");
            //var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestLogDir");
            var logFilePath = Path.Combine(logDir, LOG_FILENAME1);

            if (Directory.Exists(logDir))
                Directory.Delete(logDir, true);

            Logger.OpenLog(logDir, LOG_FILENAME1, true);
            Logger.WriteToLog("some msg");
            Logger.CloseLog();

            Assert.True(File.Exists(logFilePath));
           

            VerifyLog(logFilePath, 1, 1);

            // --------------------------------------------
            // Open existing log, and close - should append
            // ---------------------------------------------
            Logger.OpenLog(logDir, LOG_FILENAME1,true);
            Logger.CloseLog();
           
            VerifyLog(logFilePath, 2, 2);

            // --------------------------------------------
            // Trying to close twice is fine
            // --------------------------------------------
            Assert.True(Logger.CloseLog());
            VerifyLog(logFilePath, 2, 3);

            // --------------------------------------------
            // Trying to open twice is NOT fine, when we are locking it
            // This would cause the unit test to hang and throw
            // - but lets not do this and slow the tester
            // --------------------------------------------
            //Assert.True(Logger.OpenLog(logDir, LOG_FILENAME1));
            //Assert.True(Logger.OpenLog(logDir, LOG_FILENAME1));
            //Logger.CloseLog();
            //VerifyLog(logFilePath, 3, 3);

            // --------------------------------------------
            // Opening with full path in filename is fine.
            //it shouldnt throw. but since we did not lock it
            //we have no control over other threads that might of done things.
            //all we can do is verify its existence.
            // --------------------------------------------
            Logger.OpenLog(logDir, logFilePath);
            Logger.CloseLog();

            Assert.True(File.Exists(logFilePath));

            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt",true);
            Logger.CloseLog();

            //clean up
            if (Directory.Exists(logDir))
                Directory.Delete(logDir, true);
        }

        [Fact]
        public void Writing()
        {
            var logDir = Path.Combine(TestPaths.LocalScratchDirectory, "TestLogDir_Writing");
            var logFilePath = Path.Combine(logDir, LOG_FILENAME2);

            if (Directory.Exists(logDir))
                Directory.Delete(logDir, true);

            Logger.OpenLog(logDir, logFilePath, true);
            Logger.WriteToLog("Test1");
            Logger.WriteToLog("Test {0}{1}", 2, 3);
            Logger.WriteExceptionToLog(new Exception("Exception!"));
            Logger.CloseLog();

            var logContent = File.ReadAllLines(logFilePath);
            Assert.Equal(1, logContent.Count(l => l.Contains("Test1")));
            Assert.Equal(1, logContent.Count(l => l.Contains("Test 23")));
            Assert.Equal(1, logContent.Count(l => l.Contains("Exception!")));

            // --------------------------------------------
            // Trying to write when not ready is ok.
            // --------------------------------------------
            Assert.True(Logger.WriteToLog("NotReady"));

            //doesnt crash, but no guarantee it showed up in the right file, without a lock.
            //logContent = File.ReadAllLines(logFilePath);
            //Assert.True(logContent.Any(l => l.Contains("NotReady")));

            var SafeLogDir = TestPaths.LocalScratchDirectory;
            Logger.OpenLog(SafeLogDir, "DefaultLog.txt", true);
            Logger.CloseLog();
        }


        private void VerifyLog(string logFilePath, int expectedStartingLines, int expectedEndingLines)
        {
            var logContent = File.ReadAllLines(logFilePath);
            Assert.Equal(expectedStartingLines, logContent.Count(l => l.Contains("***** Starting")));
            Assert.Equal(expectedEndingLines, logContent.Count(l => l.Contains("***** Ending")));
        }
    }
}
