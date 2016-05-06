using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Pisces.Processing.Utility
{
    /// <summary>
    /// Executes an external process.
    /// 
    /// Note: this is a very bare bones version of functionality in the Isas job manager.  
    /// </summary>
    public class ExternalProcessJob : IJob
    {
        public string CommandLineArguments { get; set; }
        public string ExecutablePath { get; set; }
        public string Name { get; private set; }
        public string OutputLogPath { get; set; }
        public string ErrorLogPath { get; set; }

        public ExternalProcessJob(string name)
        {
            Name = name;
        }

        public void Execute()
        {
            var startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = ExecutablePath;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = CommandLineArguments;

            // Call WaitForExit and then the using statement will close.
            using (var exeProcess = new Process() {StartInfo = startInfo})
            {
                SetupLogger(startInfo, exeProcess);
                exeProcess.Start();

                if (startInfo.RedirectStandardOutput)
                    exeProcess.BeginOutputReadLine();
                if (startInfo.RedirectStandardError)
                    exeProcess.BeginErrorReadLine();

                exeProcess.WaitForExit();
                if (GetProcessExitCode(exeProcess) != 0)
                    throw new Exception(string.Format("Job '{0}' exited with exit code {1}", Name, exeProcess.ExitCode));
            }
        }

        private void SetupLogger(ProcessStartInfo startInfo, Process process)
        {
            if (!string.IsNullOrEmpty(OutputLogPath))
            {
                startInfo.RedirectStandardOutput = true;
                process.OutputDataReceived += new OutputRedirector(OutputLogPath).LineHandler;
            }

            if (!string.IsNullOrEmpty(ErrorLogPath))
            {
                startInfo.RedirectStandardError = true;
                process.ErrorDataReceived += new OutputRedirector(ErrorLogPath).LineHandler;
            }
        }

        private static int GetProcessExitCode(Process jobProcess)
        {
            var needExitCode = true;
            var exitCode = 0;

            while (needExitCode)
            {
                try
                {
                    exitCode = jobProcess.ExitCode;
                    needExitCode = false;
                }
                catch (InvalidOperationException)
                {
                    // even though we used jobProcess.WaitForExit, we sometimes get an InvalidOperationException
                    // in mono because the process has not exited yet *shrug*. So let's wait and try again.
                    Thread.Sleep(1000);
                }
            }

            return exitCode;
        }

        public void UpdateToMono()
        {
            CommandLineArguments = ExecutablePath + " " + CommandLineArguments;
            ExecutablePath = "mono";
        }
    }

    public class OutputRedirector : IDisposable
    {
        #region Members

        private readonly string _filePath;
        private StreamWriter _writer;

        #endregion

        public OutputRedirector(string filePath, bool autoFlush = true)
        {
            _filePath = filePath;
            _writer = new StreamWriter(filePath);
            _writer.NewLine = "\n";
            _writer.AutoFlush = autoFlush;
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Close();
                _writer = null;
            }
        }

        public void LineHandler(object sendingProcess, DataReceivedEventArgs arguments)
        {
            if (_writer == null || arguments == null) return; // Just-in-case sanity check
            if (arguments.Data == null) return;
            try
            {
                _writer.WriteLine(arguments.Data);
            }
            catch (ObjectDisposedException)
            {
                // in case the TextWriter is closed already
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error writing to file: {0}\n  {1}", _filePath, ex));
            }
        }
    }
}
