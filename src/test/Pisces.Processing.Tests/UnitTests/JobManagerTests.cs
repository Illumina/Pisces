using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Pisces.Domain.Utility;
using Pisces.Processing.Utility;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class JobManagerTests
    {
        private object _sync = new object();

        [Fact]
        public void HappyPath()
        {
            ExecuteTest(1, 1);
            ExecuteTest(1, 5);
            ExecuteTest(5, 5);
            ExecuteTest(5, 17);
        }

        private void ExecuteTest(int maxThreads, int numJobs)
        {
            var log = new List<int>();
            var jobNumber = 1;
            var jobManager = new JobManager(maxThreads);
            
            var jobs = new List<IJob>();

            var startTime = DateTime.Now;

            var allJobsStarted = new List<int>();

            for (var i = 0; i < numJobs; i ++)
            {
                jobs.Add(new GenericJob(() =>
                {
                    var myJobNumber = 0;

                    // get job number
                    lock (_sync)
                    {
                        myJobNumber = jobNumber;
                        allJobsStarted.Add(myJobNumber);

                        Interlocked.Increment(ref jobNumber);

                        log.Add((int)DateTime.Now.Subtract(startTime).TotalSeconds);
                    }

                    // hang out until all threads in round have been picked up
                    var membersOfMyRound = new List<int>();
                    var lowerBound = Math.Floor((myJobNumber - 1)/(float) maxThreads) * maxThreads;
                    for (var j = lowerBound + 1; j <= lowerBound + maxThreads; j ++)
                        if (j <= numJobs)
                            membersOfMyRound.Add((int)j);

                    var myRoundClear = false;

                    do
                    {
                        Thread.Sleep(100);
                        lock (_sync)
                        {
                            myRoundClear = membersOfMyRound.TrueForAll(allJobsStarted.Contains);
                        }
                    } while (!myRoundClear);
                }, "JobManagerTest_Thread_" + i));
            }

            jobManager.Process(jobs);
        }

        [Fact]
        public void NoJobs()
        {
            var jobManager = new JobManager(5);
            jobManager.Process(new List<IJob>());  // empty job list ok
            jobManager.Process(null);  // null job list ok
        }

        [Fact]
        public void Error()
        {
            foreach (var mode in new[] { JobErrorHandlingMode.None,  JobErrorHandlingMode.Terminate, JobErrorHandlingMode.Wait})
            {
                var log = new List<int>();
                var jobNumber = 1;
                var runningJobs = 0L;
                var jobManager = new JobManager(3, mode);

                var jobs = new List<IJob>();
                
                for (var i = 0; i < 15; i++)
                {
                    jobs.Add(new GenericJob(() =>
                    {
                        var myJobNumber = 0;

                        // get job number
                        lock (_sync)
                        {
                            myJobNumber = jobNumber++;
                            log.Add(myJobNumber);
                        }
                        Interlocked.Increment(ref runningJobs);

                        if (myJobNumber == 2)
                        {
                            Thread.Sleep(1000);
                            Interlocked.Decrement(ref runningJobs);

                            throw new Exception("Help");
                        }
                        Thread.Sleep(3000); // pretend to do some work
                        Interlocked.Decrement(ref runningJobs);
                    }, "JobManagerErrorTest_Thread_" + i));
                }

                var timeStarted = DateTime.Now;
                Assert.Throws<Exception>(() => jobManager.Process(jobs));
                var timeTaken = DateTime.Now - timeStarted;

                // make sure no jobs after the initially launched jobs are executed
                Assert.Equal(3, log.Count);
                Assert.True(log.Contains(1));
                Assert.True(log.Contains(2));
                Assert.True(log.Contains(3));
                switch (mode)
                {
                    case JobErrorHandlingMode.None:
                        Assert.True(timeTaken.TotalSeconds < 3);
                        Thread.Sleep(3000);
                        break;
                    case JobErrorHandlingMode.Wait:
                        Assert.Equal(0, Interlocked.Read(ref runningJobs));
                        Assert.True(timeTaken.TotalSeconds >= 3);
                        break;
                    case JobErrorHandlingMode.Terminate:
                        Assert.Equal(2, Interlocked.Read(ref runningJobs));
                        Assert.True(timeTaken.TotalSeconds < 3);
                        break;

                }
            }
        }
    }
}
