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
using Pisces.Domain.Models;
using Pisces.IO;
using ReadRealignmentLogic.Models;
using StitchingLogic;

namespace Gemini.IO
{
    public class GeminiDataSourceFactory : IGeminiDataSourceFactory
    {
        private readonly StitcherOptions _stitcherOptions;
        private readonly string _genomePath;
        private readonly bool _skipAndRemoveDuplicates;
        private readonly int? _refId;
        private readonly bool _debug;

        public GeminiDataSourceFactory(StitcherOptions stitcherOptions, string genomePath, bool skipAndRemoveDuplicates, int? refId = null, string regionsFile = null, bool debug = false)
        {
            _stitcherOptions = stitcherOptions;
            _genomePath = genomePath;
            _skipAndRemoveDuplicates = skipAndRemoveDuplicates;
            _refId = refId;
            _debug = debug;
        }

        public ChrReference GetChrReference(string chrom)
        {
            var genome = new Genome(_genomePath, new List<string> { chrom });
            return genome.GetChrReference(chrom);
        }

        public IDataSource<ReadPair> CreateReadPairSource(IBamReader bamReader, ReadStatusCounter statusCounter)
        {
            //var pairSourceLevelFilterProperPairs = _stitcherOptions.FilterForProperPairs;
            var pairSourceLevelFilterProperPairs = false; // This gets taken care of at the Gemini level now.
            var filter = new StitcherPairFilter(_stitcherOptions.FilterDuplicates,
                pairSourceLevelFilterProperPairs, new AlignmentFlagDuplicateIdentifier(), statusCounter,
                minMapQuality: 0, treatImproperAsIncomplete: false);

            var readLength = 150;
            return new PairFilterReadPairSource(bamReader, statusCounter, _skipAndRemoveDuplicates, 
                filter, refId: _refId, 
                expectedFragmentLength: readLength, filterForProperPairs: pairSourceLevelFilterProperPairs);
        }

        public IBamReader CreateBamReader(string bamFilePath)
        {
            return new BamReader(bamFilePath);
        }

        public IGenomeSnippetSource CreateGenomeSnippetSource(string chrom, ChrReference chrReference = null, int snippetSize = 2000)
        {
            if (chrReference == null)
            {
                Logger.WriteToLog($"Getting chromosome {chrom} from genome {_genomePath}");
            }

            var genome = new Genome(_genomePath, new List<string> { chrom });
            return new GenomeSnippetSource(chrom, genome, snippetSize, chrReference: chrReference);
        }

        public IHashableIndelSource GetHashableIndelSource()
        {
            return new HashableIndelSource(_debug);
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