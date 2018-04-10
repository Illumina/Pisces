//using System;
//using System.Diagnostics;
//using System.IO;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using System.Threading;

//namespace Pisces.Processing.Utility
//{
//    /// <summary>
//    /// Executes an external process.
//    /// 
//    /// Note: this is a very bare bones version of functionality in the Isas job manager.  
//    /// can we replace with: https://docs.microsoft.com/en-us/dotnet/core/api/system.collections.concurrent.concurrentbag-1
//    /// or System.Threading.Tasks;
//    /// </summary>
//    public class ExternalProcessJob : IJob
//    {
//        public string CommandLineArguments { get; set; }
//        public string ExecutablePath { get; set; }
//        public string Name { get; private set; }
//        public string OutputLogPath { get; set; }
//        public string ErrorLogPath { get; set; }

//        private bool _Started = false;
//        private bool _Terminated = false;

//        public bool Terminated
//        {
//            get
//            {
//                lock (typeof(ExternalProcessJob))
//                {
//                    return _Terminated;
//                }
//            }
//            set
//            {
//                lock (typeof(ExternalProcessJob))
//                {
//                    _Terminated = value; ;
//                }
//            }
//        }

//        public bool Started  //tell jobpool
//        {

//            get
//            {
//                lock (typeof(ExternalProcessJob))
//                {
//                    return _Started;
//                }
//            }
//            set
//            {
//                lock (typeof(ExternalProcessJob))
//                {
//                    _Started = value; ;
//                }
//            }
//        }

//        public ExternalProcessJob(string name)
//        {
//            Name = name;
//        }

//        public void Execute()
//        {
//            try
//            {
//                var startInfo = new ProcessStartInfo();
//                startInfo.CreateNoWindow = false;
//                startInfo.UseShellExecute = false;
//                startInfo.FileName = ExecutablePath;
//                //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
//                startInfo.Arguments = CommandLineArguments;

//                // Call WaitForExit and then the using statement will close.
//                using (var exeProcess = new Process() { StartInfo = startInfo })
//                {
//                    SetupLogger(startInfo, exeProcess);
//                    try
//                    {
//                        exeProcess.Start();
//                        Started = true;

//                        if (startInfo.RedirectStandardOutput)
//                            exeProcess.BeginOutputReadLine();
//                        if (startInfo.RedirectStandardError)
//                            exeProcess.BeginErrorReadLine();
                        
             

//                        if (GetProcessExitCode(exeProcess) != 0)
//                            throw new Exception(string.Format("Job '{0}' exited with exit code {1}", Name,
//                                exeProcess.ExitCode));
//                    }
//                    //catch (ThreadAbortException)
//                    catch (Exception)
//                    {
//                        // the job is being terminated due to a failure in another job - terminate the executable
//                        exeProcess.Kill();
//                        throw;
//                    }
//                }
//            }
//            //catch (ThreadAbortException)
//            catch (Exception)
//            {
//                Terminated = true;
//                throw;
//            }
//        }

//        private void SetupLogger(ProcessStartInfo startInfo, Process process)
//        {
//            if (!string.IsNullOrEmpty(OutputLogPath))
//            {
//                startInfo.RedirectStandardOutput = true;
//                process.OutputDataReceived += new OutputRedirector(OutputLogPath).LineHandler;
//            }

//            if (!string.IsNullOrEmpty(ErrorLogPath))
//            {
//                startInfo.RedirectStandardError = true;
//                process.ErrorDataReceived += new OutputRedirector(ErrorLogPath).LineHandler;
//            }
//        }

//        private static int GetProcessExitCode(Process jobProcess)
//        {
//            var needExitCode = true;
//            var exitCode = 0;

//            while (needExitCode)
//            {
            
//                try
//                {
//                    exitCode = jobProcess.ExitCode;
//                    needExitCode = false;
//                }
//                catch (InvalidOperationException)
//                {
//                    // even though we used jobProcess.WaitForExit, we sometimes get an InvalidOperationException
//                    // in mono because the process has not exited yet *shrug*. So let's wait and try again.
//                    Thread.Sleep(1000);
//                }
//            }

//            return exitCode;
//        }

//        public void UpdateToMono()
//        {
//            CommandLineArguments = ExecutablePath + " " + CommandLineArguments;
//            ExecutablePath = "mono";
//        }
//    }

//    public class OutputRedirector : IDisposable
//    {
//        #region Members

//        private readonly string _filePath;
//        private StreamWriter _writer;

//        #endregion

//        public OutputRedirector(string filePath, bool autoFlush = true)
//        {
//            _filePath = filePath;
//            _writer = new StreamWriter(new FileStream(filePath, FileMode.Create));
//            _writer.NewLine = "\n";
//            _writer.AutoFlush = autoFlush;
//        }

//        public void Dispose()
//        {
//            if (_writer != null)
//            {
//                //_writer.Close();
//                _writer.Dispose();
//                _writer = null;
//            }
//        }

//        public void LineHandler(object sendingProcess, DataReceivedEventArgs arguments)
//        {
//            if (_writer == null || arguments == null) return; // Just-in-case sanity check
//            if (arguments.Data == null) return;
//            try
//            {
//                _writer.WriteLine(arguments.Data);
//            }
//            catch (ObjectDisposedException)
//            {
//                // in case the TextWriter is closed already
//                return;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine(string.Format("Error writing to file: {0}\n  {1}", _filePath, ex));
//            }
//        }
//    }
//}
////suggstions: https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
