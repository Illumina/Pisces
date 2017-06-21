using System;
using Common.IO.Utility;

namespace Pisces.Processing.Utility
{
    public class ElapsedTimeLogger : IDisposable
    {
        private readonly string _message;
        private readonly DateTime _startTime;
        private DateTime _incrementStartTime;
        
        public ElapsedTimeLogger(string message)
        {
            _message = message;
            _incrementStartTime = _startTime = DateTime.Now;
            Logger.WriteToLog($"Starting: {_message}");
        }

        public void LogIncrement(string message, TimeSpan minLogThreshold)
        {
            var now = DateTime.Now;
            var timeSpan = now - _incrementStartTime;
            if (minLogThreshold.Seconds <= timeSpan.Seconds)
                Logger.WriteToLog($"Increment: {message}:{timeSpan.Seconds}s");
            _incrementStartTime = now;
        }

        public void Dispose()
        {
            var timeSpan = DateTime.Now - _startTime;
            Logger.WriteToLog($"Completed: {_message}:{timeSpan.Seconds}s");
        }
    }
}
