using System;
using System.Collections.Generic;
using System.Linq;
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

    public enum JobErrorHandlingMode
    {
        None, // do nothing to running jobs if one of them fails
        Wait, // wait for running jobs to complete before returning
        Terminate // terminate running jobs if one has an error
    }

    public class JobManager : IJobManager
    {
        public int MaxThreads { get; private set; }
        public JobErrorHandlingMode ErrorHandlingMode { get; private set; }

        public JobManager(int maxThreads, JobErrorHandlingMode errorHandlingMode = JobErrorHandlingMode.None)
        {
            MaxThreads = maxThreads;
            ErrorHandlingMode = errorHandlingMode;
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
            var failures = 0L;
            var jobsRemaining = jobs.Count;
            var exceptions = new List<Exception>();
            var threads = ErrorHandlingMode == JobErrorHandlingMode.None ? null : new List<Thread>(jobs.Count);

            for (var jobIndex = 0; jobIndex < jobs.Count; ++jobIndex)
            {
                Thread.Sleep(10);

                //this looks like its hanging and preventing any other threads from
                //starting. maybe we are supposed to raise an event from the job when the thread is started..?
                jobPool.WaitOne(); //i am just not sure why this is here...
              
                if (doneEvent.WaitOne(0)) // got the signal to quit
                {
                    Release(jobPool);
                    break;
                }

                var job = jobs[jobIndex];
                var jobThread = new Thread(o => ExecuteJob(job, jobPool, doneEvent, ref failures, ref jobsRemaining, exceptions));
                if (!string.IsNullOrEmpty(job.Name))
                    jobThread.Name = job.Name;
                jobThread.Start();
                if (threads != null)
                    threads.Add(jobThread);
            }

            doneEvent.WaitOne();
            if (threads != null && Interlocked.Read(ref failures) > 0)
            {
                // copy the exceptions in case we are terminating jobs to prevent ThreadAbortExceptions ending up in there
                if (ErrorHandlingMode == JobErrorHandlingMode.Terminate)
                    exceptions = new List<Exception>(exceptions);

                foreach (var thread in threads)
                {
                    if (thread.IsAlive)
                    {
                        try
                        {
                            if (ErrorHandlingMode == JobErrorHandlingMode.Terminate)
                            {
                                throw new ThreadStateException("Terminating the thread. Thread.abort is missing for .net core..");
                                 //   https://msdn.microsoft.com/en-us/library/dd997364(v=vs.110).aspx //maybe replace with cancellation token
                            }
                               
                            thread.Join();
                        }
                        catch
                        {
                            // eating errors on purpose
                        }
                    }
                }
            }
            if (exceptions.Any())
                throw exceptions[0];
        }

        private void ExecuteJob(IJob job, Semaphore jobPool, ManualResetEvent doneEvent, ref long failures, ref int jobsRemaining, List<Exception> exceptions)
        {
            try
            {
                // this should be a blocking call
                job.Execute();
            }
            catch (Exception ex)
            {
                // todo log something?
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
                Interlocked.Increment(ref failures);
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

        public GenericJob(Action action, string name)
        {
            Action = action;
            Name = name;
        }
    }
}
