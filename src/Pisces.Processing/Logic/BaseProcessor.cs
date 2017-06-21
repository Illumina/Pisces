using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.Domain.Interfaces;
using Common.IO.Utility;

namespace Pisces.Processing.Logic
{
    public abstract class BaseProcessor
    {
        private object _sync = new object();
        private readonly Dictionary<string, double> _bamProcessingTimes = new Dictionary<string, double>();
        protected readonly List<Exception> Exceptions = new List<Exception>();
        protected IGenome Genome;

        public void Execute(int maxThreads)
        {
            var startTime = DateTime.Now;

            InternalExecute(maxThreads);

            if (Exceptions.Any())
                throw Exceptions[0];

            LogProcessingTimes();

            Logger.WriteToLog("Total execution time is {0}s", DateTime.Now.Subtract(startTime).TotalSeconds);
        }

        public abstract void InternalExecute(int maxThreads);

        protected void TrackTime(string bamFilePath, double time)
        {
            lock (_sync)
            {
                if (!_bamProcessingTimes.ContainsKey(bamFilePath))
                    _bamProcessingTimes[bamFilePath] = time;
                else
                {
                    _bamProcessingTimes[bamFilePath] += time;
                }
            }
        }

        private void LogProcessingTimes()
        {
            lock (_sync)
            {
                foreach (var bamFilePath in _bamProcessingTimes.Keys.Where(k => !string.IsNullOrEmpty(k)))
                {
                    Logger.WriteToLog("{0}: Total processing time is {1}s", Path.GetFileName(bamFilePath),
                        _bamProcessingTimes[bamFilePath]);
                }
            }
        }
    }
}
