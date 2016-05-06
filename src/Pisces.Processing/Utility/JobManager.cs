using System;
using System.Collections.Generic;
using System.Threading;

namespace Pisces.Processing.Utility
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
        public int MaxThreads { get; private set; }

        public JobManager(int maxThreads)
        {
            MaxThreads = maxThreads;
        }

        public void Process(List<IJob> jobs)
        {
            if (jobs == null || jobs.Count == 0) return;

            // we originally used TPL, but we encountered unexpected behavior from MaxDegreeOfParallelism
            // With MaxDegreeOfParallelism, it seemed like if it was set to 10 and each job used 50% CPU,
            // it would launch around 20 threads. ThreadPool was better, but also tried to be smart about
            // launching threads. Using standard threads and a semaphore yielded the desired behavior.

            // run our jobs
            var jobPool = new Semaphore(MaxThreads, MaxThreads);
            var doneEvent = new ManualResetEvent(false);
            var jobsRemaining = jobs.Count;

            for (var jobIndex = 0; jobIndex < jobs.Count; ++jobIndex)
            {
                Thread.Sleep(10);
                jobPool.WaitOne();

                if (doneEvent.WaitOne(0)) // got the signal to quit
                {
                    Release(jobPool);
                    break;
                }

                var job = jobs[jobIndex];
                var jobThread = new Thread(o => ExecuteJob(job, jobPool, doneEvent, ref jobsRemaining));
                if (!string.IsNullOrEmpty(job.Name))
                    jobThread.Name = job.Name;
                jobThread.Start();
            }

            doneEvent.WaitOne();
        }

        private void ExecuteJob(IJob job, Semaphore jobPool, ManualResetEvent doneEvent, ref int jobsRemaining)
        {
            try
            {
                // this should be a blocking call
                job.Execute();
            }
            catch (Exception ex)
            {
                // todo log something?
                doneEvent.Set(); // stop 
            }
            finally
            {
                if (Interlocked.Decrement(ref jobsRemaining) <= 0) 
                    doneEvent.Set();

                Release(jobPool);
            }
        }

        private void Release(Semaphore jobPool)
        {
            try
            {
                jobPool.Release();
            }
            catch (Exception)
            {
                // in case we get an exception releasing, just swallow it
                // this happens if you release more than max count
                // unclear how this might occur in this class, but adding just in case
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
