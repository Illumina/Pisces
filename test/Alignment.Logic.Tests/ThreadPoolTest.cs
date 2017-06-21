using System.Collections.Generic;
using System.Collections.Concurrent;
using Xunit;


namespace Alignment.Logic.Tests
{
    // Simple test class that stores an item in a list.
    class TaskTest<T> : Task
    {
        public TaskTest(List<T> intList, T i) : base()
        {
            _intList = intList;
            _i = i;
        }

        public override void Execute(int threadNum)
        {
            lock (_intList)
            {
                _intList.Add(_i);
            }
        }

        private List<T> _intList;
        private T _i;
    }


    public class ThreadPoolTest
    {
        [Fact]
        public void ExecuteTest()
        {
            var taskQueue = new BlockingCollection<Task>();

            // Add some tasks to the queue before the thread pool is created.
            var intList = new List<int>();
            var floatList = new List<float>();
            for (int i = 0; i < 50; ++i)
            {
                // The thread pool will pull these tasks off the queue and execute them asynchronously.
                taskQueue.Add(new TaskTest<int>(intList, i));
                taskQueue.Add(new TaskTest<float>(floatList, 2.5f * i));
            }

            var threadPool = new ThreadPool(taskQueue, numThreads: 2);

            // Add some more tasks after the thread pool is created. These will be added
            // at the same time as some tasks are being pulled of the queue and executed.
            for (int i = 50; i < 100; ++i)
            {
                // The thread pool will pull these tasks off the queue and execute them asynchronously.
                taskQueue.Add(new TaskTest<int>(intList, i));
                taskQueue.Add(new TaskTest<float>(floatList, 2.5f * i));
            }

            // Wait for all tasks to complete.
            threadPool.RunToCompletion();

            // The lists are out of order because 2 threads were operating.
            intList.Sort();
            floatList.Sort();

            for (int i = 0; i < 100; ++i)
            {
                Assert.Equal(i, intList[i]);
                Assert.Equal(2.5f * i, floatList[i]);
            }
        }
    }
}
