using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Processing.Utility;

namespace Pisces.Processing.Logic
{
    public abstract class BaseGenomeProcessor : BaseProcessor
    {
        protected readonly List<BamWorkRequest> WorkRequests;

        private Dictionary<string, ChrReference> _currentlyProcessingChrs = new Dictionary<string, ChrReference>();
        private Dictionary<string, List<BamWorkRequest>> _remainingWorkByChr = new Dictionary<string, List<BamWorkRequest>>();
        private Dictionary<BamWorkRequest, List<string>> _remainingChrByBam = new Dictionary<BamWorkRequest, List<string>>(); 
        private object _sync = new object();
        private Dictionary<string, AutoResetEvent> _throttleSignalsByBamChr;

        protected IJobManager JobManager;
        protected bool ShouldThrottle { get { return _throttleSignalsByBamChr != null; } }
        private int? _maxChrsProcessingAllowed;

        public BaseGenomeProcessor(List<BamWorkRequest> workRequests, IGenome genome, 
            bool throttlePerBam = true, int? maxChrsProcessingAllowed = null)
        {
            Genome = genome;
            WorkRequests = workRequests;

            if (throttlePerBam)
            {
                _throttleSignalsByBamChr = new Dictionary<string, AutoResetEvent>();
                _maxChrsProcessingAllowed = maxChrsProcessingAllowed;
            }
        }

        public override void InternalExecute(int maxThreads)
        {
            try
            {
                Logger.WriteToLog("Processing genome '{0}' with {1} threads", Genome.Directory, maxThreads);

                Initialize();

                // if throttling, clip max threads to number of work request so we don't have idle threads
                if (ShouldThrottle)
                    maxThreads = Math.Min(maxThreads, WorkRequests.Count);

                JobManager = new JobManager(maxThreads);
                var jobs = new List<IJob>();

                foreach (var workRequest in WorkRequests)
                    _remainingChrByBam.Add(workRequest, new List<string>());

                // break out work by chr and bam.  make sure to lump chr work together
                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    var chrWork = new List<BamWorkRequest>();

                    foreach (var workRequest in WorkRequests)
                    {
                        chrWork.Add(workRequest);
                        jobs.Add(new GenericJob(() => ProcessByBam(workRequest, chrName)));
                        _remainingChrByBam[workRequest].Add(chrName);
                    }

                    _remainingWorkByChr.Add(chrName, chrWork);
                }

                // set up throttle signals
                if (ShouldThrottle)
                    InitThrottleSignals(WorkRequests);

                if (maxThreads > 1)
                    JobManager.Process(jobs); // process all jobs
                else
                {
                    foreach (var job in jobs)
                        job.Execute();
                }
            }
            finally
            {
                Finish();
            }
        }

        protected abstract void Initialize();
        protected abstract void Process(BamWorkRequest workRequest, ChrReference chrReference);
        protected abstract void Finish();

        private void ProcessByBam(BamWorkRequest workRequest, string chrName)
        {
            var bamFileName = workRequest.BamFileName;

            try
            {
                if (ShouldThrottle)
                    AcquireSignal(workRequest, chrName);

                if (_maxChrsProcessingAllowed.HasValue)
                    EnforceChrLimit(chrName);

                var chrReference = GetChrReference(chrName);
                if (chrReference == null)
                    return;  // missing chr, move on

                Logger.WriteToLog(string.Format("{0}: Start processing chr '{1}'.", bamFileName, chrName));
                var startTime = DateTime.UtcNow;

                Process(workRequest, chrReference);

                var processingTime = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                Logger.WriteToLog(string.Format("{0}: Completed processing chr '{2}' in {1}s", bamFileName, processingTime, chrName));
                TrackTime(workRequest.BamFilePath, processingTime);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception(string.Format("{1}: Error processing chr '{0}': {2}", chrName, bamFileName, ex.Message), ex);
                Logger.WriteExceptionToLog(wrappedException);

                lock (this)
                    Exceptions.Add(ex);
            }
            finally
            {
                CheckChrComplete(chrName, workRequest);
                if (ShouldThrottle)
                    SignalNext(workRequest, chrName);
            }
        }

        private ChrReference GetChrReference(string chrName)
        {
            lock (_sync)
            {
                ChrReference chrReference;
                _currentlyProcessingChrs.TryGetValue(chrName, out chrReference);

                if (chrReference == null)
                {
                    var startTime = DateTime.Now;
                    chrReference = Genome.GetChrReference(chrName);
                    Logger.WriteToLog("Loaded chromosome '{0}' in {1} secs", chrName, DateTime.Now.Subtract(startTime).TotalSeconds);

                    if (chrReference == null)
                    {
                        // we still add to the lookup so subprocesses can bail out when they encounter null without having to try loading again.
                        Logger.WriteToLog("Unable to find {0} in the reference genome.", chrName);
                        Logger.WriteToLog("Skipping {0}.", chrName);
                    }

                    _currentlyProcessingChrs.Add(chrName, chrReference);
                }

                return chrReference;
            }
        }

        private void CheckChrComplete(string chrName, BamWorkRequest completedWorkRequest)
        {
            lock (_sync)
            {
                var chrWork = _remainingWorkByChr[chrName];

                chrWork.Remove(completedWorkRequest);

                if (!chrWork.Any()) // no more work left
                {
                    if (_currentlyProcessingChrs.ContainsKey(chrName))
                    {
                        _currentlyProcessingChrs[chrName].Sequence = null;
                        _currentlyProcessingChrs.Remove(chrName);
                    }

                    Logger.WriteToLog("Unloaded chromosome '{0}', all jobs complete.", chrName);
                }
            }
        }

        private string GetThrottleKey(BamWorkRequest workRequest, string chrName)
        {
            return workRequest.BamFilePath + "-" + chrName;
        }

        private void InitThrottleSignals(List<BamWorkRequest> workRequests)
        {
            foreach (var workRequest in workRequests)
            {
                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    _throttleSignalsByBamChr.Add(GetThrottleKey(workRequest, chrName), new AutoResetEvent(false));
                }
            }

            // signal first chromosome 
            if (Genome.ChromosomesToProcess.Any())
            {
                var firstChromosome = Genome.ChromosomesToProcess.First();
                foreach (var workRequest in WorkRequests)
                {
                    _throttleSignalsByBamChr[GetThrottleKey(workRequest, firstChromosome)].Set();
                }
            }
        }

        private void AcquireSignal(BamWorkRequest workRequest, string chrName)
        {
            _throttleSignalsByBamChr[GetThrottleKey(workRequest, chrName)].WaitOne();
        }

        private void SignalNext(BamWorkRequest workRequest, string chrName)
        {
            lock (_sync)
            {
                var remainingWork = _remainingChrByBam[workRequest];
                remainingWork.Remove(chrName);

                if (remainingWork.Any())
                    _throttleSignalsByBamChr[GetThrottleKey(workRequest, remainingWork.First())].Set();
            }
        }

        private void EnforceChrLimit(string chrName)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_currentlyProcessingChrs.ContainsKey(chrName) ||
                        _currentlyProcessingChrs.Count < _maxChrsProcessingAllowed) return;
                }

                Thread.Sleep(5000);
            }
        }
    }
}
