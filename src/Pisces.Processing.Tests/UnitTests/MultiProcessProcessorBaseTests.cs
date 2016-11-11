using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;
using TestUtilities.MockBehaviors;
using Xunit;

namespace Pisces.Processing.Tests.UnitTests
{
    class MyProcessor : MultiProcessProcessorBase
    {
        public MyProcessor(IGenome genome, IJobManager jobManager, IEnumerable<string> inputFiles,
            string[] commandLineArgs, string outputFolder, string logFolder) : base(genome, jobManager, inputFiles, commandLineArgs, outputFolder, logFolder, exePath:"foo.exe")
        {
            
        }

        protected override void Finish()
        {
            
        }
    }
    public class MultiProcessProcessorBaseTests
    {
        [Fact]
        public void TestCommandLineGeneration()
        {
            var jobManager = new Mock<IJobManager>();
            List<IJob> jobs = null;
            jobManager.Setup(x => x.Process(It.IsAny<List<IJob>>()))
                .Callback<List<IJob>>(x => jobs = x);
            List<ChrReference> chrRef = new List<ChrReference>()
            {
                new ChrReference() { Name = "chr19", Sequence = "AAA" },
                new ChrReference() { Name = "chrX", Sequence = "AAA" },
            };
            var genome = new MockGenome(chrRef);
            var files = new[] { @"C:\foo\foo.bam", @"C:\foo\bar.bam"};

            var processor = new MyProcessor(genome, jobManager.Object, files,
                new[] {"-bamFoLder", @"C:\foo", "-maxNumThreads", "5", "-outFOLDER", @"C:\blee" },
                @"C:\blee", "");
            processor.Execute(0);
            // should be 1 job for each file x each chromosome
            Assert.NotNull(jobs);
            var realJobs = jobs.Cast<ExternalProcessJob>().ToArray();
            Assert.Equal(4, realJobs.Length);
            Assert.Equal(0, realJobs.Count(x => x.CommandLineArguments.Contains("outFOLDER")));
            Assert.Equal(0, realJobs.Count(x => x.CommandLineArguments.Contains("bamFoLder")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains(@"-OutFolder C:\blee\chr19")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains(@"-OutFolder C:\blee\chrX")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains(@"-bampaths C:\foo\foo.bam")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains(@"-bampaths C:\foo\bar.bam")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains("-chrfilter chr19")));
            Assert.Equal(2, realJobs.Count(x => x.CommandLineArguments.Contains("-chrfilter chrX")));
            Assert.Equal(4, realJobs.Count(x => x.CommandLineArguments.Contains("-InsideSubProcess true")));
            Assert.Equal(4, realJobs.Count(x => x.CommandLineArguments.Contains("-MaxNumThreads 1")));
        }
    }
}
