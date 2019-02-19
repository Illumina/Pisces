using System.Collections.Generic;
using Gemini.Interfaces;
using Pisces.Processing.Utility;
using Moq;
using Xunit;

namespace GeminiMulti.Tests
{
    public class GeminiMultiProcessorTests
    {
        [Fact]
        public void Execute()
        {
            var options = new GeminiMultiApplicationOptions(){OutputDirectory = "Output", ExePath = "myexe"};

            var mockSamtools = new Mock<ISamtoolsWrapper>();
            var mockCliTaskManager = new Mock<ICliTaskManager>();
            var executedTasks = new List<ICliTask>();
            var cliTaskCreator = new Mock<CliTaskCreator>();

            var mockTask1 = new Mock<ICliTask>();
            var mockTask2 = new Mock<ICliTask>();
            mockTask1.SetupGet(x => x.ExitCode).Returns(0);
            mockTask2.SetupGet(x => x.ExitCode).Returns(0);

            var tasks = new Dictionary<int,ICliTask>() {{1,mockTask1.Object}, {3,mockTask2.Object}};
            var commandsPassed = new List<IEnumerable<string>>();

            cliTaskCreator.Setup(x => x.GetCliTask(It.IsAny<string[]>(),
              It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
              .Returns<string[], string, string, string, int>((args, chrom, e, outDir, chrRefId) => tasks[chrRefId]).Callback<string[], string, string, string, int>(
                  (args, chrom, e, outDir, chrRefId) => { commandsPassed.Add(args); });



            mockCliTaskManager.Setup(x => x.Process(It.IsAny<List<ICliTask>>()))
                .Callback<List<ICliTask>>(x =>
                {
                    foreach (var task in x)
                    {
                        executedTasks.Add(task);
                    }
                });

            var chroms = new Dictionary<string, int>()
            {
                {"chr1", 1},
                {"chr3", 3}
            };

            var processor = new GeminiMultiProcessor(options, cliTaskCreator.Object);

            var cmdLineList = new List<string>() { "--command", "line", "-options", "tada" };
            processor.Execute(mockCliTaskManager.Object, chroms, cmdLineList, mockSamtools.Object);
            Assert.Equal(2, executedTasks.Count);
        }
    }
}
