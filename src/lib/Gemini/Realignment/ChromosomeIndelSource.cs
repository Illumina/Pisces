using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using Pisces.Domain.Types;

namespace Gemini.Realignment
{
    public interface IChromosomeIndelSource
    {
        IEnumerable<KeyValuePair<HashableIndel, GenomeSnippet>> GetRelevantIndels(int position,
            List<PreIndel> preSelectedIndels = null);

        IChromosomeIndelSource DeepCopy();
        List<HashableIndel> Indels { get; }
    }

    public class ChromosomeIndelSource : IChromosomeIndelSource
    {
        public List<HashableIndel> Indels { get; private set; }
        private readonly int _bucketSize;
        public int LowestPosition { get; private set; }
        public int HighestPosition { get; private set; }
        private readonly int _numIndels;
        private readonly List<KeyValuePair<HashableIndel, GenomeSnippet>> _emptyHashablesList = new List<KeyValuePair<HashableIndel, GenomeSnippet>>();
        private readonly ReadOnlyDictionary<int, List<HashableIndel>> _positionalBucketsOfIndels;
        private readonly ReadOnlyDictionary<int, GenomeSnippet> _genomeSnippetsLookup;
        private readonly ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>> _partnerIndels;

        public ChromosomeIndelSource(List<HashableIndel> indels, ReadOnlyDictionary<int, GenomeSnippet> genomeSnippetsLookup,
            ReadOnlyDictionary<int, List<HashableIndel>> positionalBucketsOfIndels, ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>> partnerIndels, int bucketSize)
        {
            _bucketSize = bucketSize;
            Indels = indels;

            if (!indels.Any())
            {
                return;
            }
            LowestPosition = indels.Min(x => x.ReferencePosition);
            HighestPosition = indels.Max(x => x.ReferencePosition);

            _genomeSnippetsLookup = genomeSnippetsLookup;
            _positionalBucketsOfIndels = positionalBucketsOfIndels;
            _numIndels = indels.Count();
            _partnerIndels = partnerIndels;
        }

        public ChromosomeIndelSource(List<HashableIndel> indels, IGenomeSnippetSource snippetSource, int bucketSize = 1000)
        {
            _bucketSize = bucketSize;
            Indels = indels;

            if (!indels.Any())
            {
                return;
            }
            LowestPosition = indels.Min(x => x.ReferencePosition);
            HighestPosition = indels.Max(x=>x.ReferencePosition);

            var partnerIndelsLookup = new Dictionary<HashableIndel, IEnumerable<HashableIndel>>();
            var positionalBuckets = new Dictionary<int, List<HashableIndel>>();

            foreach (var indel in indels)
            {
                var bucketNum = ((indel.ReferencePosition - LowestPosition) / bucketSize);

                if (!positionalBuckets.ContainsKey(bucketNum))
                {
                    positionalBuckets.Add(bucketNum, new List<HashableIndel>());
                }

                // TODO come back to this if needed - was thinking I could link indels to each other so that if multi-indels contain indels in multiple buckets we would make sure to grab all
                // Maybe add start and end pos of hashables, and this could take into account the main and the other
                var indelString = Helper.HashableToString(indel);
                var partnerIndels = indels.Where(x =>
                    Helper.HashableToString(x) == indelString || x.OtherIndel == indelString);
                partnerIndelsLookup.Add(indel, partnerIndels);
                positionalBuckets[bucketNum].Add(indel);
                _numIndels++;
            }

            _partnerIndels = new ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>>(partnerIndelsLookup);

            _positionalBucketsOfIndels = new ReadOnlyDictionary<int, List<HashableIndel>>(positionalBuckets);

            var snippetsLookup = new Dictionary<int, GenomeSnippet>();
            foreach (var kvp in _positionalBucketsOfIndels)
            {
                var bucket = kvp.Value;
                if (bucket.Any())
                {
                    var firstIndel = bucket.First();
                    var snippet = snippetSource.GetGenomeSnippet(firstIndel.ReferencePosition);

                    snippetsLookup[kvp.Key] = snippet;
                }

            }

            _genomeSnippetsLookup = new ReadOnlyDictionary<int, GenomeSnippet>(snippetsLookup);
        }

        private bool IsMatch(PreIndel pre, HashableIndel hashable)
        {
            var equivPosition = pre.Chromosome == hashable.Chromosome &&
                                pre.ReferencePosition == hashable.ReferencePosition;

            if (!equivPosition)
            {
                return false;

            }

            var equivAlleles = pre.Type == AlleleCategory.Insertion ? InsertionsAreMatch(pre, hashable):
                pre.ReferenceAllele.Length == hashable.ReferenceAllele.Length;
            return equivAlleles;
        }

        private bool InsertionsAreMatch(PreIndel pre, HashableIndel hashable)
        {
            if (pre.AlternateAllele.Substring(1) == hashable.AlternateAllele.Substring(1))
            {
                return true;
            }

            if (pre.AlternateAllele.Length != hashable.AlternateAllele.Length)
            {
                return false;
            }

            for (int i = 0; i < pre.AlternateAllele.Length; i++)
            {
                var candidateBase = pre.AlternateAllele[i];
                var hashableBase = hashable.AlternateAllele[i];
                if (candidateBase != 'N' && hashableBase != 'N' && candidateBase != hashableBase)
                {
                    return false;
                }    
            }

            return true;
        }

        private bool IsMultiMatch(HashableIndel hashable, PreIndel indel)
        {
            // TODO shouldn't this also check the normal indel?
            return hashable.InMulti && Helper.CandidateToString(indel) == hashable.OtherIndel;
        }


        public IEnumerable<KeyValuePair<HashableIndel, GenomeSnippet>> GetRelevantIndels(int position, List<PreIndel> preSelectedIndels = null)
        {
            var indelsToReturn = new Dictionary<HashableIndel, GenomeSnippet>();

            // TODO make this calculation right
            if (_numIndels == 0 || position > HighestPosition + _bucketSize || position < LowestPosition - _bucketSize)
            {
                return _emptyHashablesList;
            }

            const int maxDistance = 250;

            var indelExactBucketNum = (position - LowestPosition) / _bucketSize;

            const int maxNumTopScorersToReturn = 5;
            const int maxNumExtraTopScorerMultisToReturn = 3;
            for (int i = 0; i <= 2; i++)
            {
                var peripheralBucketNum = indelExactBucketNum - 1 + i;
                if (_positionalBucketsOfIndels.ContainsKey(peripheralBucketNum))
                {
                    var addedForBucket = 0;
                    var bucket = _positionalBucketsOfIndels[peripheralBucketNum];
                    var snippetForBucket = _genomeSnippetsLookup[peripheralBucketNum];
                    foreach (var item in bucket.OrderByDescending(v=>v.Score))
                    {
                        if ((addedForBucket >= maxNumTopScorersToReturn && !item.InMulti) || (addedForBucket >= maxNumTopScorersToReturn + maxNumExtraTopScorerMultisToReturn))
                        {
                            continue;
                        }
                        if (Math.Abs(item.ReferencePosition - position) <= maxDistance)
                        {
                            addedForBucket++;
                            indelsToReturn[item] = snippetForBucket;
                        }
                    }
                }
            }
            
            var filteredIndelsRaw = indelsToReturn.OrderByDescending(x => x.Key.Score).ToList();
            var filteredIndels =
                filteredIndelsRaw.Take(maxNumTopScorersToReturn).Concat(filteredIndelsRaw.Where(x => x.Key.InMulti).Take(maxNumExtraTopScorerMultisToReturn)).Distinct();
            
            if (preSelectedIndels != null)
            {
                filteredIndels = filteredIndels.Where(x => preSelectedIndels.Any(p => IsMatch(p, x.Key)) || x.Key.InMulti && preSelectedIndels.Any(p=> IsMultiMatch(x.Key,p)));
            }
            
            return filteredIndels;

        }

        public IChromosomeIndelSource DeepCopy()
        {
            return new ChromosomeIndelSource(Indels, new ReadOnlyDictionary<int, GenomeSnippet>(_genomeSnippetsLookup), new ReadOnlyDictionary<int, List<HashableIndel>>(_positionalBucketsOfIndels), new ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>>(_partnerIndels), (_bucketSize));
        }
    }
}