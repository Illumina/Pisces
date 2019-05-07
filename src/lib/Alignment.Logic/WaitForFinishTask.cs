using System;
using System.Threading;

namespace Alignment.Logic
{
    public abstract class WaitForFinishTask : Task
    {
        private static int _numTasks = 0;
        private static AutoResetEvent _event = new AutoResetEvent(false);

        public WaitForFinishTask()
        {
            Interlocked.Increment(ref _numTasks);
        }

        public override void Execute(int threadNum)
        {
            try
            {
                ExecuteImpl(threadNum);

                Interlocked.Decrement(ref _numTasks);
                _event.Set();
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to execute task: {e.Message}", e);
            }
        }

        public static void WaitUntilZeroTasks()
        {
            while (_numTasks > 0)
            {
                _event.WaitOne();
            }
        }

        public abstract void ExecuteImpl(int threadNum);
    }
}