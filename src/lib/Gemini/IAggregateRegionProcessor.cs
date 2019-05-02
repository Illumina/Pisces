using System.Collections.Concurrent;
using Gemini.IndelCollection;

namespace Gemini
{
    public interface IAggregateRegionProcessor
    {
        AggregateRegionResults GetAggregateRegionResults(ConcurrentDictionary<string, IndelEvidence> indelLookup, int startPosition, int endPosition, bool isFinalTask, RegionDataForAggregation regionData);
    }
}