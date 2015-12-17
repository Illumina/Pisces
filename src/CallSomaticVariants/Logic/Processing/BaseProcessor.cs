using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants.Logic.Processing
{
    public abstract class BaseProcessor
    {
        private object _sync = new object();
        private readonly Dictionary<string, double> _bamProcessingTimes = new Dictionary<string, double>();
        protected readonly List<Exception> Exceptions = new List<Exception>();
        protected Factory Factory;
        protected IGenome Genome;

        public void Execute(int maxThreads)
        {
            InternalExecute(maxThreads);

            if (Exceptions.Any())
                throw Exceptions[0];

            LogProcessingTimes();
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
