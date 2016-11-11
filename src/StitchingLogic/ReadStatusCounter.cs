using System.Collections.Generic;

namespace StitchingLogic
{
    public class ReadStatusCounter : IReadStatusCounter
    {
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
    }
}