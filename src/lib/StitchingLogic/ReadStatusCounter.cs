using System.Collections.Generic;

namespace StitchingLogic
{
    public class ReadStatusCounter : IReadStatusCounter
    {
        // Using dictionaries here is very slow.
        // Hopefully this isn't used too often...
        private Dictionary<string, int> _readStatuses;
        private Dictionary<string, int> _debugReadStatuses;

        public ReadStatusCounter()
        {
            _readStatuses = new Dictionary<string, int>();
            _debugReadStatuses = new Dictionary<string, int>();
        }

        public Dictionary<string, int> GetReadStatuses()
        {
            return _readStatuses;
        }

        public Dictionary<string, int> GetDebugReadStatuses()
        {
            return _debugReadStatuses;
        }

        public void AddDebugStatusCount(string status)
        {
            if (!_debugReadStatuses.ContainsKey(status))
            {
                _debugReadStatuses.Add(status, 0);
            }
            _debugReadStatuses[status]++;
        }

        public void AddStatusCount(string status)
        {
            if (!_readStatuses.ContainsKey(status))
            {
                _readStatuses.Add(status, 0);
            }
            _readStatuses[status]++;
        }

        public void Merge(ReadStatusCounter other)
        {
            foreach (var readStatus in other._readStatuses)
            {
                if (!_readStatuses.ContainsKey(readStatus.Key))
                {
                    _readStatuses.Add(readStatus.Key, readStatus.Value);
                }
                else
                {
                    _readStatuses[readStatus.Key] += readStatus.Value;
                }
            }

            foreach (var debugReadStatus in other._debugReadStatuses)
            {
                if (!_debugReadStatuses.ContainsKey(debugReadStatus.Key))
                {
                    _debugReadStatuses.Add(debugReadStatus.Key, debugReadStatus.Value);
                }
                else
                {
                    _debugReadStatuses[debugReadStatus.Key] += debugReadStatus.Value;
                }
            }
        }
    }
}