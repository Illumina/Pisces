using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO.Sequencing;
using Gemini.CandidateIndelSelection;
using Gemini.Realignment;
using Gemini.Types;
using StitchingLogic;

namespace Gemini.Interfaces
{
    public interface IGeminiDataSourceFactory
    {
        IDataSource<ReadPair> CreateReadPairSource(IBamReader bamReader, ReadStatusCounter statusCounter);

        IBamReader CreateBamReader(string bamFilePath);
        IGenomeSnippetSource CreateGenomeSnippetSource(string chrom);
        IHashableIndelSource GetHashableIndelSource();

        IChromosomeIndelSource GetChromosomeIndelSource(List<HashableIndel> chromIndels, IGenomeSnippetSource snippetSource);
        Dictionary<int, string> GetRefIdMapping(string bam);

    }
}