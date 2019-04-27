using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Gemini.Interfaces;
using Gemini.Models;
using Gemini.Types;
using Gemini.Utility;
using ReadRealignmentLogic.Models;

namespace Gemini.Realignment
{
    public interface IChromosomeIndelSource
    {
        IEnumerable<KeyValuePair<HashableIndel, GenomeSnippet>> GetRelevantIndels(int position,
            List<PreIndel> preSelectedIndels = null, List<HashableIndel> confirmedIndels = null, List<PreIndel> existingIndels = null, List<PreIndel> mateIndels = null);

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

        // Copy constructor
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

            var partnerIndelsLookup = new Dictionary<HashableIndel, IEnumerable<HashableIndel>>();
            var positionalBuckets = new Dictionary<int, List<HashableIndel>>();
            var snippetsLookup = new Dictionary<int, GenomeSnippet>();

            if (!indels.Any())
            {
                _partnerIndels = new ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>>(partnerIndelsLookup);
                _genomeSnippetsLookup = new ReadOnlyDictionary<int, GenomeSnippet>(snippetsLookup);
                _positionalBucketsOfIndels = new ReadOnlyDictionary<int, List<HashableIndel>>(positionalBuckets);

                return;
            }
            LowestPosition = indels.Min(x => x.ReferencePosition);
            HighestPosition = indels.Max(x=>x.ReferencePosition);


            foreach (var indel in indels)
            {
                var bucketNum = ((indel.ReferencePosition - LowestPosition) / bucketSize);

                if (!positionalBuckets.TryGetValue(bucketNum, out var indelsForbucket))
                {
                    indelsForbucket = new List<HashableIndel>();
                    positionalBuckets.Add(bucketNum, indelsForbucket);
                }

                // TODO come back to this if needed - was thinking I could link indels to each other so that if multi-indels contain indels in multiple buckets we would make sure to grab all
                // Maybe add start and end pos of hashables, and this could take into account the main and the other
                //var indelString = Helper.HashableToString(indel);
                var indelString = indel.StringRepresentation;
                var partnerIndels = indels.Where(x =>
                    x.StringRepresentation == indelString || x.OtherIndel == indelString);
                partnerIndelsLookup.Add(indel, partnerIndels);
                indelsForbucket.Add(indel);
                _numIndels++;
            }

            _partnerIndels = new ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>>(partnerIndelsLookup);

            _positionalBucketsOfIndels = new ReadOnlyDictionary<int, List<HashableIndel>>(positionalBuckets);

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

        private bool IsMultiMatch(HashableIndel hashable, HashableIndel indel)
        {
            // TODO shouldn't this also check the normal indel?
            return hashable.InMulti && Helper.HashableToString(indel) == hashable.OtherIndel;
        }

        private bool IsMultiMatch(HashableIndel hashable, PreIndel indel)
        {
            // TODO shouldn't this also check the normal indel?
            return hashable.InMulti && Helper.CandidateToString(indel) == hashable.OtherIndel;
        }


        public IEnumerable<KeyValuePair<HashableIndel, GenomeSnippet>> GetRelevantIndels(int position, List<PreIndel> preSelectedIndels = null, List<HashableIndel> confirmedIndels = null, List<PreIndel> existingIndels = null, List<PreIndel> mateIndels = null)
        {
            // TODO make this calculation right
            // TODO figure out what that ^ means. I don't see what's not "right" about this but I'll leave the comment til I figure it out or determine that it is not meant to be there
            if (_numIndels == 0 || position > HighestPosition + _bucketSize || position < LowestPosition - _bucketSize)
            {
                return _emptyHashablesList;
            }

            var indelsToReturn = new Dictionary<HashableIndel, GenomeSnippet>();

            const int maxDistance = 250;

            var indelExactBucketNum = (position - LowestPosition) / _bucketSize;

            // TODO see how many are actually being used
            const int maxNumTopScorersToReturn = 5;
            const int maxNumExtraTopScorerMultisToReturn = 3;
            for (int i = 0; i <= 2; i++)
            {
                var peripheralBucketNum = indelExactBucketNum - 1 + i;
                if (_positionalBucketsOfIndels.TryGetValue(peripheralBucketNum, out var bucket))
                {
                    var addedForBucket = 0;
                    GenomeSnippet snippetForBucket = null;
                    foreach (var item in bucket.OrderByDescending(v=>v.Score))
                    {
                        if ((addedForBucket >= maxNumTopScorersToReturn && !item.InMulti) || (addedForBucket >= maxNumTopScorersToReturn + maxNumExtraTopScorerMultisToReturn))
                        {
                            continue;
                        }
                        if (Math.Abs(item.ReferencePosition - position) <= maxDistance)
                        {
                            addedForBucket++;
                            if (snippetForBucket == null)
                            {
                                snippetForBucket = _genomeSnippetsLookup[peripheralBucketNum];
                            }
                            indelsToReturn[item] = snippetForBucket;
                        }
                    }
                }
            }
            
            var filteredIndelsRaw = indelsToReturn.OrderByDescending(x=> IsFavored(preSelectedIndels, confirmedIndels, x)).
                ThenByDescending(x => x.Key.Score).ThenByDescending(x=> IsPreSelected(preSelectedIndels, x)).ThenBy(x=>x.Key.StringRepresentation).ToList();

            var filteredIndels = FilterIndels(preSelectedIndels, filteredIndelsRaw, maxNumTopScorersToReturn, 
                maxNumExtraTopScorerMultisToReturn, confirmedIndels, position);

            return filteredIndels;

        }

        private IEnumerable<KeyValuePair<HashableIndel, GenomeSnippet>> FilterIndels(List<PreIndel> preSelectedIndels,
            List<KeyValuePair<HashableIndel, GenomeSnippet>> filteredIndelsRaw, int maxNumTopScorersToReturn,
            int maxNumExtraTopScorerMultisToReturn, List<HashableIndel> confirmedIndels, int position)
        {
            var filteredIndels = filteredIndelsRaw.Count > maxNumTopScorersToReturn
                ? filteredIndelsRaw.Take(maxNumTopScorersToReturn)
                    .Concat(filteredIndelsRaw.Where(x => x.Key.InMulti).Take(maxNumExtraTopScorerMultisToReturn)).Concat(filteredIndelsRaw.Where(x=>IsPreSelected(preSelectedIndels,x))).Distinct()
                : filteredIndelsRaw;

            // TODO why only specially treat duplications in pair-specific case?
            // ^^ I think the reasoning behind this was that you may have only seen the smaller version of a dup insertion... but this needs some revision.
            if (confirmedIndels != null && confirmedIndels.Any())
            {
                if (confirmedIndels.Any(x =>
                    (x.ReferencePosition >= position && x.ReferencePosition - position < 100) ||
                    (x.ReferencePosition <= position && position - x.ReferencePosition < 50)))
                {
                    // The confirmed indels are nearby
                    // We should only realign around that (and select few others ie multis that contain it)
                    var filteredToConfirmed = filteredIndels.Where(x => ShouldTakeIndelWithPreSelected(confirmedIndels, x));
                    filteredIndels = filteredToConfirmed.Any() ? filteredToConfirmed : filteredIndels;
                }
                else
                {
                    // Keep all filtered indels.
                }
            }
  
            return filteredIndels;
        }

        private bool ShouldTakeIndelWithPreSelected(List<HashableIndel> preSelectedIndels, KeyValuePair<HashableIndel, GenomeSnippet> x)
        {
            // TODO should we really be nicer to duplications here...
            return x.Key.IsDuplication || preSelectedIndels.Any(
                       p => Helper.IsMatch(p, x.Key)) || x.Key.InMulti && preSelectedIndels.Any(p => IsMultiMatch(x.Key, p));
        }

        private bool IsFavored(List<PreIndel> preSelectedIndels, List<HashableIndel> confirmedIndels, KeyValuePair<HashableIndel, GenomeSnippet> x)
        {
            if (x.Key.HardToCall && preSelectedIndels != null && (preSelectedIndels.Any(
                    p => Helper.IsMatch(p, x.Key)) ||
                x.Key.InMulti && preSelectedIndels.Any(p => IsMultiMatch(x.Key, p))))
            {
                return true;
            }
            if (confirmedIndels != null && (confirmedIndels.Any(
                    p => Helper.IsMatch(p, x.Key)) ||
                x.Key.InMulti && confirmedIndels.Any(p => IsMultiMatch(x.Key, p))))
            {
                return true;
            }

            return false;
        }

        private bool IsPreSelected(List<PreIndel> preSelectedIndels, KeyValuePair<HashableIndel, GenomeSnippet> x)
        {
            if (preSelectedIndels != null && (preSelectedIndels.Any(
                    p => Helper.IsMatch(p, x.Key)) ||
                x.Key.InMulti && preSelectedIndels.Any(p => IsMultiMatch(x.Key, p))))
            {
                return true;
            }

            return false;
        }


        public IChromosomeIndelSource DeepCopy()
        {
            return new ChromosomeIndelSource(Indels, new ReadOnlyDictionary<int, GenomeSnippet>(_genomeSnippetsLookup), new ReadOnlyDictionary<int, List<HashableIndel>>(_positionalBucketsOfIndels), new ReadOnlyDictionary<HashableIndel, IEnumerable<HashableIndel>>(_partnerIndels), (_bucketSize));
        }
    }
}