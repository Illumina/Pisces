using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CallSomaticVariants.Logic.Processing
{
    public interface IJobManager
    {
        void Process(List<IJob> jobs);
    }

    public interface IJob
    {
        void Execute();

        string Name { get; }
    }

    public class JobManager : IJobManager
    {
        private Semaphore _jobPool;
        private AutoResetEvent _doneEvent;
        private int _jobsRemaining;
        private int _maxThreads;

        public JobManager(int maxThreads)
        {
            _maxThreads = maxThreads;
        }

        public void Process(List<IJob> jobs)
        {
            if (jobs.Count == 0) return;

            // we originally used TPL, but we encountered unexpected behavior from MaxDegreeOfParallelism
            // With MaxDegreeOfParallelism, it seemed like if it was set to 10 and each job used 50% CPU,
            // it would launch around 20 threads. ThreadPool was better, but also tried to be smart about
            // launching threads. Using standard threads and a semaphore yielded the desired behavior.

            // run our jobs
            _jobPool = new Semaphore(_maxThreads, _maxThreads);
            _doneEvent = new AutoResetEvent(false);
            _jobsRemaining = jobs.Count;

            for (var jobIndex = 0; jobIndex < jobs.Count; ++jobIndex)
            {
                _jobPool.WaitOne();

                if (_doneEvent.WaitOne(0)) // got the signal to quit
                {
                    _jobPool.Release();
                    break;
                }

                var job = jobs[jobIndex];
                var jobThread = new Thread(o => ExecuteJob(job));
                jobThread.Name = string.Format("Job thread '{0}'", job.Name);
                jobThread.Start();
            }

            _doneEvent.WaitOne();
        }

        public void ExecuteJob(IJob job)
        {
            try
            {
                // this should be a blocking call
                job.Execute();
            }
            catch (Exception ex)
            {
                // todo log something?
                _doneEvent.Set(); // stop 
            }
            finally
            {
                if (Interlocked.Decrement(ref _jobsRemaining) <= 0) _doneEvent.Set();
                _jobPool.Release();
            }
        }
    }

    /// <summary>
    /// Job to perform some generic action internal to the program
    /// </summary>
    public class GenericJob : IJob
    {
        public Action Action { get; set; }

        public string Name { get; set; }

        public void Execute()
        {
            Action();
        }

        public GenericJob(Action action)
        {
            Action = action;
        }
    }
}
