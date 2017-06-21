using System;
using System.Threading;
using System.Collections.Concurrent;

namespace Alignment.Logic
{
    abstract public class Task
    {
        protected Task()
        {
        }

        abstract public void Execute(int threadNum);
    }

    public class ThreadPool
    {
        private BlockingCollection<Task> _taskQueue;
        private Thread[] _threads;

        public ThreadPool(BlockingCollection<Task> taskQueue, int numThreads)
        {
            _taskQueue = taskQueue;
            _threads = new Thread[numThreads];

            for (int i = 0; i < numThreads; ++i)
            {
                int threadNum = i;
                _threads[i] = new Thread(() => Execute(threadNum));
                _threads[i].Name = string.Format("Thread {0}", i);
                _threads[i].Start();
            }
        }

        private void Execute(int threadNum)
        {
            try
            {
                while (true)
                {
                    _taskQueue.Take().Execute(threadNum);
                }
            }
            catch (InvalidOperationException)
            {
                // We're done. There are no more tasks to process.
            }
        }

        public void RunToCompletion()
        {
            _taskQueue.CompleteAdding();

            foreach (Thread thread in _threads)
            {
                thread.Join();
            }
        }
    }
}
