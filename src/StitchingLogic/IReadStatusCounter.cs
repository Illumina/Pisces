using System.Collections.Generic;

namespace StitchingLogic
{
    public interface IReadStatusCounter
    {
        Dictionary<string, int> GetReadStatuses();
        Dictionary<string, int> GetDebugReadStatuses();
    }
}