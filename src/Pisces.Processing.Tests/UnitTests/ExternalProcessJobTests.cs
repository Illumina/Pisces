using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pisces.Processing.Utility;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    public class ExternalProcessJobTests
    {
        [Fact]
        public void CancellingKillsTheProcess()
        {
            var jobManager = new JobManager(3, JobErrorHandlingMode.Terminate);
            var externalJob = new ExternalProcessJob("foo")
            {
                ExecutablePath = "notepad.exe"
            };
            var jobs = new List<IJob>
            {
                externalJob,
                new GenericJob(() =>
                {
                    while (!externalJob.Started)
                        Thread.Sleep(100);
                    // the program should have started by now - trigger the error
                    throw new ApplicationException("Bad monkey");
                })
            };
            Assert.Throws<ApplicationException>(() => jobManager.Process(jobs));
            Assert.True(externalJob.Terminated);
        }
    }
}
