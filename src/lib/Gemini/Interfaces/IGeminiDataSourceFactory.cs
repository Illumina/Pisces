using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO.Sequencing;
using Gemini.CandidateIndelSelection;
using Gemini.Realignment;
using Pisces.Domain.Models;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini.Interfaces
{
    public interface IGeminiDataSourceFactory
    {
        ChrReference GetChrReference(string chrom);

        IDataSource<ReadPair> CreateReadPairSource(IBamReader bamReader, ReadStatusCounter statusCounter);

        IBamReader CreateBamReader(string bamFilePath);
        IGenomeSnippetSource CreateGenomeSnippetSource(string chrom, ChrReference chrReference = null, int snippetSize = 2000);
        IHashableIndelSource GetHashableIndelSource();

        IChromosomeIndelSource GetChromosomeIndelSource(List<HashableIndel> chromIndels, IGenomeSnippetSource snippetSource);
        Dictionary<int, string> GetRefIdMapping(string bam);
    }
}