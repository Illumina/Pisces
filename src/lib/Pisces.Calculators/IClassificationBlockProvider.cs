using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gemini.ClassificationAndEvidenceCollection;

namespace Gemini
{
    public interface IClassificationBlockProvider
    {
        Task[] GetAndLinkAllClassificationBlocksWithEcFinalization(
            ISourceBlock<PairResult> pairClassifierBlock,
            int startPosition, int endPosition, ConcurrentDictionary<int, EdgeState> edgeStates,
            ConcurrentDictionary<int, Task> edgeToWaitOn, int prevBlockStart,
            bool isFinalTask = false);

    }
}