using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Common.IO.Utility;

namespace Pisces.Processing.Utility
{
    public class CliTaskManager : ICliTaskManager
    {

        private readonly int _maxDegreeOfParallelism;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="maxDegreeOfParallelism">Max number of tasks allowed running in parallel</param>
        /// <param name="logger"></param>
        public CliTaskManager(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism <= 0)
            {
                Logger.WriteToLog($"maxDegreeOfParallelism {maxDegreeOfParallelism} must be greater than 0.");     

                throw new ArgumentException($"maxDegreeOfParallelism {maxDegreeOfParallelism} must be greater than 0.");
            }

            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <inheritdoc />
        /// <summary>
        /// Execute tasks in parallel (bounded by MaxDegreeOfParallelism)
        ///  </summary>
        /// <param name="tasks"></param>
        /// <remarks>
        /// returns after all tasks exit (blocking call).
        /// </remarks>
        public void Process(List<ICliTask> tasks)
        {
            var actionBlock = new ActionBlock<ICliTask>(
                x => x.Execute(),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism
                });

            foreach (var task in tasks)
            {
                actionBlock.Post(task);
            }
            actionBlock.Complete();

            try
            {
                actionBlock.Completion.Wait();
            }
            catch (AggregateException e)
            {
                Console.WriteLine(e);

                Logger.WriteToLog("Failed in CliTaskManager");
                Logger.WriteExceptionToLog(e);
            }
        }
    }
}