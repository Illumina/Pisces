using System.IO;
using System.Threading.Tasks;

namespace Pisces.Processing.Utility
{

    /// <summary>
    /// Interface of Cli Task
    /// </summary>
    public interface ICliTask
    {
        /// <summary>
        /// Execute the task
        ///  </summary>
        /// <remarks>
        /// returns after the task is completed (blocking call).
        /// </remarks>
        void Execute();
        /// <summary>
        /// Execute the task async
        /// </summary>
        /// <returns>awaitable task with exit code</returns>
        Task<int> ExecuteAsync();
        /// <summary>
        /// Execute
        /// </summary>
        /// <param name="stdout"></param>
        /// <param name="stderr"></param>
        /// <remarks>
        /// returns after the task is completed (blocking call).
        /// remember to dispose the redirected stream after done.
        /// </remarks>
        void Execute(out StreamReader stdout, out StreamReader stderr);
        /// <summary>
        /// Task name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// cli arguments
        /// </summary>
        string CommandLineArguments { get; set; }
        /// <summary>
        /// cli ExecutablePath
        /// </summary>
        string ExecutablePath { get; set; }
        /// <summary>
        /// ExitCode, 0 if succeed, non-zero otherwise 
        /// </summary>
        int ExitCode { get; }
    }
}