using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Alignment.Domain;
using Gemini.ClassificationAndEvidenceCollection;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;

namespace Gemini
{
    public interface IBlockFactorySource
    {
        IBatchBlockFactory<BatchBlock<ReadPair>,ReadPair> GetBatchBlockFactory();
        ITransformerBlockFactory<TransformManyBlock<IEnumerable<ReadPair>, PairResult>> GetClassifierBlockFactory();

        IClassificationBlockProvider GetBlockProvider(Dictionary<int, string> refIdMapping, string chrom,
            IWriterSource writerSource, ConcurrentDictionary<string, int> progressTracker,
            ConcurrentDictionary<PairClassification, int> categoryLookup, ConcurrentDictionary<string, IndelEvidence> masterIndelLookup,
            ConcurrentDictionary<HashableIndel, int[]> masterOutcomesLookup,
            ConcurrentDictionary<HashableIndel, int> masterFinalIndels, ChrReference chrReference);

        //IActionBlockFactory<ActionBlock<PairResult[]>> GetActionBlockFactory();
    }
}