using System;
using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.IO.Sequencing;
using BamStitchingLogic;
using Common.IO.Utility;
using Gemini.CandidateIndelSelection;
using Gemini.Interfaces;
using Gemini.Realignment;
using Gemini.Types;
using Pisces.IO;
using StitchingLogic;

namespace Gemini.IO
{
    public class GeminiDataSourceFactory : IGeminiDataSourceFactory
    {
        private readonly StitcherOptions _stitcherOptions;
        private readonly string _genomePath;
        private readonly bool _skipAndRemoveDuplicates;
        private readonly int? _refId;

        public GeminiDataSourceFactory(StitcherOptions stitcherOptions, string genomePath, bool skipAndRemoveDuplicates, int? refId = null)
        {
            _stitcherOptions = stitcherOptions;
            _genomePath = genomePath;
            _skipAndRemoveDuplicates = skipAndRemoveDuplicates;
            _refId = refId;
        }

        public IDataSource<ReadPair> CreateReadPairSource(IBamReader bamReader, ReadStatusCounter statusCounter)
        {
            var filter = new StitcherPairFilter(_stitcherOptions.FilterDuplicates,
                _stitcherOptions.FilterForProperPairs, new AlignmentFlagDuplicateIdentifier(), statusCounter,
                minMapQuality: _stitcherOptions.FilterMinMapQuality);

            return new PairFilterReadPairSource(bamReader, statusCounter, _skipAndRemoveDuplicates, filter, _refId);
        }

        public IBamReader CreateBamReader(string bamFilePath)
        {
            return new BamReader(bamFilePath);
        }

        public IGenomeSnippetSource CreateGenomeSnippetSource(string chrom)
        {
            Logger.WriteToLog($"Getting chromosome {chrom} from genome {_genomePath}");
            var genome = new Genome(_genomePath, new List<string> { chrom });
            return new GenomeSnippetSource(chrom, genome, 2000);
        }

        public IHashableIndelSource GetHashableIndelSource()
        {
            return new HashableIndelSource(_genomePath);
        }

        public IChromosomeIndelSource GetChromosomeIndelSource(List<HashableIndel> chromIndels, IGenomeSnippetSource snippetSource)
        {
            return new ChromosomeIndelSource(chromIndels, snippetSource);
        }

        public Dictionary<int, string> GetRefIdMapping(string bam)
        {
            var refIdMapping = new Dictionary<int, string>();

            using (var reader = CreateBamReader(bam))
            {
                foreach (var referenceName in reader.GetReferenceNames())
                {
                    refIdMapping.Add(reader.GetReferenceIndex(referenceName), referenceName);
                }
            }

            if (!refIdMapping.ContainsKey(-1))
            {
                if (refIdMapping.Values.Any(x => x.Equals("Unmapped")))
                {
                    throw new Exception("There is an unmapped chromosome with refId not equal to -1.");
                }
                refIdMapping.Add(-1, "Unmapped");
            }

            return refIdMapping;
        }

    }
}