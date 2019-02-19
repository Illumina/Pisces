using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.IO.Utility;

namespace Pisces.Processing.Utility
{
    /// <inheritdoc />
    /// <summary>
    /// cli Task
    /// </summary>
    public class CliTask : ICliTask
    {
        public string CommandLineArguments { get; set; }
        public string ExecutablePath { get; set; }
        public int ExitCode { get; private set; }
        public string Name { get; }
        private bool Terminated { get; set; }
        private string OutputLogDir { get; }
        private string ErrorLogDir { get; }

        public CliTask(string name, string runtimePath, string executablePath, string commandLineArguments, string outputLogDir = null, string errorLogDir = null)
            : this(name, runtimePath, $"{executablePath} {commandLineArguments}")
        { }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="executablePath"></param>
        /// <param name="commandLineArguments"></param>
        /// <param name="logger"></param>
        /// <param name="outputLogDir"></param>
        /// <param name="errorLogDir"></param>
        public CliTask(string name, string executablePath, string commandLineArguments,// ElapsedTimeLogger logger,
            string outputLogDir = null, string errorLogDir = null)
        {
            Name = name;
            CommandLineArguments = commandLineArguments;
            ExecutablePath = executablePath;
            OutputLogDir = outputLogDir;
            ErrorLogDir = errorLogDir;
            Terminated = false;
            ExitCode = int.MinValue;  // init non-zero exit code.
        }

        /// <inheritdoc />
        /// <summary>
        /// Execute the cli task async
        /// </summary>
        /// <returns>error code</returns>
        public Task<int> ExecuteAsync()
        {
            // Call WaitForExit and then the using statement will close.
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = ExecutablePath,
                Arguments = CommandLineArguments
            };
            var tcs = new TaskCompletionSource<int>();
            try
            {
                Logger.WriteProcessToLog(Name, $"Executing '{ExecutablePath}' with '{CommandLineArguments}'");

                var exeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                RedirectStdOut(exeProcess.StartInfo);
                exeProcess.Start();

                OutputRedirector stdOutToFile = null;
                OutputRedirector stdErrToFile = null;
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                if (startInfo.RedirectStandardOutput)
                {
                    stdOutToFile = new OutputRedirector(OutputLogDir, $"{Name}-{exeProcess.Id}-{timestamp}.stdout");
                    exeProcess.OutputDataReceived += stdOutToFile.LineHandler;
                    exeProcess.BeginOutputReadLine();
                }
                if (startInfo.RedirectStandardError)
                {
                    stdErrToFile = new OutputRedirector(ErrorLogDir, $"{Name}-{exeProcess.Id}-{timestamp}.stderr");
                    exeProcess.ErrorDataReceived += stdErrToFile.LineHandler;
                    exeProcess.BeginErrorReadLine();
                }

                exeProcess.Exited += (s, e) =>
                {
                    ExitCode = exeProcess.ExitCode;

                    Logger.WriteProcessToLog(Name, $"ExitCode: {ExitCode}.");

                    tcs.TrySetResult(ExitCode);
                    stdOutToFile?.Dispose();
                    stdErrToFile?.Dispose();
                    exeProcess.Dispose();
                };
            }
            catch (Exception ex)
            {
                ExitCode = -1;
                tcs.TrySetResult(ExitCode);
                tcs.TrySetException(ex);

                Logger.WriteProcessToLog(Name, "the task is being terminated due to a failure.");
                Logger.WriteExceptionToLog(ex);

            }
            return tcs.Task;
        }

        /// <inheritdoc />
        /// <summary>
        ///  Execute the cli task
        /// </summary>
        /// <param name="stdout">StandOut</param>
        /// <param name="stderr">standError</param>
        public void Execute(out StreamReader stdout, out StreamReader stderr)
        {
            // Call WaitForExit and then the using statement will close.
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = ExecutablePath,
                Arguments = CommandLineArguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Logger.WriteProcessToLog(Name, $"Executing '{ExecutablePath}' with '{CommandLineArguments}'");

            using (var exeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                stdout = null;
                stderr = null;
                try
                {
                    exeProcess.Start();
                    stdout = exeProcess.StandardOutput;
                    stderr = exeProcess.StandardError;
                    exeProcess.WaitForExit();
                    ExitCode = GetProcessExitCode(exeProcess);

                    Logger.WriteProcessToLog(Name, $"ExitCode: {ExitCode}.");

                }
                catch (Exception ex)
                {
                    ExitCode = -1;

                    Logger.WriteProcessToLog(Name, "the task is being terminated due to a failure.");
                    Logger.WriteExceptionToLog(ex);

                    try
                    {
                        exeProcess?.Kill();
                        Terminated = true;
                    }
                    catch (Exception e)
                    {
                        Logger.WriteProcessToLog(Name, "failed to kill the process.");
                        Logger.WriteExceptionToLog(e);

                    }
                }
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Execute the cli task
        /// </summary>
        /// <remarks>blocking call</remarks>
        public void Execute()
        {
            // Call WaitForExit and then the using statement will close.
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = ExecutablePath,
                Arguments = CommandLineArguments
            };

            Logger.WriteProcessToLog(Name, $"Executing '{ExecutablePath}' with '{CommandLineArguments}'");

            using (var exeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                OutputRedirector stdOutToFile = null;
                OutputRedirector stdErrToFile = null;
                try
                {
                    RedirectStdOut(exeProcess.StartInfo);
                    exeProcess.Start();
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                    if (startInfo.RedirectStandardOutput)
                    {
                        stdOutToFile = new OutputRedirector(OutputLogDir, $"{Name}-{exeProcess.Id}-{timestamp}.stdout");
                        exeProcess.OutputDataReceived += stdOutToFile.LineHandler;
                        exeProcess.BeginOutputReadLine();
                    }
                    if (startInfo.RedirectStandardError)
                    {
                        stdErrToFile = new OutputRedirector(ErrorLogDir, $"{Name}-{exeProcess.Id}-{timestamp}.stderr");
                        exeProcess.ErrorDataReceived += stdErrToFile.LineHandler;
                        exeProcess.BeginErrorReadLine();
                    }
                    exeProcess.WaitForExit();
                    ExitCode = GetProcessExitCode(exeProcess);

                    Logger.WriteProcessToLog(Name, $"ExitCode: {ExitCode}.");

                }
                catch (Exception ex)
                {
                    ExitCode = -1;

                    Logger.WriteProcessToLog(Name, "the task is being terminated due to a failure.");
                    Logger.WriteExceptionToLog(ex);

                    try
                    {
                        exeProcess?.Kill();
                        Terminated = true;
                    }
                    catch (Exception e)
                    {
                        Logger.WriteProcessToLog(Name, "failed to kill the process.");
                        Logger.WriteExceptionToLog(e);

                    }
                }
                finally
                {
                    stdOutToFile?.Dispose();
                    stdErrToFile?.Dispose();
                }
            }
        }
        private void RedirectStdOut(ProcessStartInfo startInfo)
        {
            if (!string.IsNullOrEmpty(OutputLogDir))
            {
                startInfo.RedirectStandardOutput = true;
            }

            if (!string.IsNullOrEmpty(ErrorLogDir))
            {
                startInfo.RedirectStandardError = true;
            }
        }

        private int GetProcessExitCode(Process jobProcess)
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
                    // So let's wait and try again. (observed in .net core). 
                    Thread.Sleep(1000);
                }
            }
            return exitCode;
        }
    }
}