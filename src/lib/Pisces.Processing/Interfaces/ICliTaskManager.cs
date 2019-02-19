using System.Collections.Generic;

namespace Pisces.Processing.Utility
{
    public interface ICliTaskManager
    {
        /// <summary>
        /// Execute tasks in parallel (bounded by maxDegreeOfParallelism)
        /// </summary>
        /// <param name="tasks"></param>
        /// <remarks>
        /// returns after all tasks exit (blocking call).
        /// </remarks>
        void Process(List<ICliTask> tasks);
    }
}