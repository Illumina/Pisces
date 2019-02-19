using System.IO;
using System.Linq;
using Common.IO.Utility;
using StitchingLogic;

namespace Gemini.Utility
{
    public static class StatusLogger
    {
        public static void LogStatuses(string inBam, ReadStatusCounter _statusCounter, bool showDebug = true)
        {
            foreach (var readStatus in _statusCounter.GetReadStatuses().OrderBy(x => x.Key))
            {
                Logger.WriteToLog(Path.GetFileName(inBam) + " | STATUSCOUNT " + readStatus.Key + " | " + readStatus.Value);
            }

            if (showDebug)
            {
                foreach (var readStatus in _statusCounter.GetDebugReadStatuses().OrderBy(x => x.Key))
                {
                    Logger.WriteToLog(Path.GetFileName(inBam) + " | STATUSCOUNT " + readStatus.Key + " | " + readStatus.Value);
                }
            }

        }
    }
}