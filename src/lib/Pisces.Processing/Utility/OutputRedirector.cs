using System;
using System.Diagnostics;
using System.IO;

namespace Pisces.Processing.Utility
{
    /// <inheritdoc />
    /// <summary>
    /// Redirect StandardOut and StandardError of a command line process
    /// </summary>
    /// <remarks>originally from Pisces suite code</remarks>
    public class OutputRedirector : IDisposable
    {
        #region Members
        private readonly string _filePath;
        private StreamWriter _writer;
        #endregion

        public OutputRedirector(string filePath, string filename, bool autoFlush = true)
        {
            if (!string.IsNullOrEmpty(filePath) && !Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);
            _filePath = Path.Combine(filePath, filename);
            _writer = new StreamWriter(new FileStream(_filePath, FileMode.Create))
            {
                NewLine = "\n",
                AutoFlush = autoFlush
            };
        }
        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error writing to file: {0}\n  {1}", _filePath, ex));
            }
        }
    }
}