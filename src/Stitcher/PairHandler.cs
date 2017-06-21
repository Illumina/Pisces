using System.IO;
using System.Collections.Generic;
using Alignment.Domain;
using Alignment.IO;
using Pisces.Domain.Models;
using Alignment.Domain.Sequencing;
using StitchingLogic;

namespace Stitcher
{
    public class PairHandler : IReadPairHandler
    {
        private readonly Dictionary<int, string> _refIdMapping;
        private readonly IAlignmentStitcher _stitcher;
        private readonly bool _filterUnstitchablePairs;
        private readonly ReadStatusCounter _masterStatusCounter;
        private readonly ReadStatusCounter _statusCounter;

        public PairHandler(Dictionary<int, string> refIdMapping, IAlignmentStitcher stitcher, bool filterUnstitchablePairs, ReadStatusCounter statusCounter)
        {
            _refIdMapping = refIdMapping;
            _stitcher = stitcher;
            _filterUnstitchablePairs = filterUnstitchablePairs;
            _masterStatusCounter = statusCounter;
            _statusCounter = new ReadStatusCounter();
            _stitcher.SetStatusCounter(_statusCounter);
        }

        public List<BamAlignment> ExtractReads(ReadPair pair)
        {
            var reads = new List<BamAlignment>();

            var chrom1 = _refIdMapping[pair.Read1.RefID];
            var chrom2 = _refIdMapping[pair.Read2.RefID];

            var alignmentSet = new AlignmentSet(
                new Read(chrom1, pair.Read1),
                new Read(chrom2, pair.Read2),
                false);

            var stitched = _stitcher.TryStitch(alignmentSet);

            if (stitched)
            {
                if (alignmentSet.ReadsForProcessing.Count > 1)
                {
                    throw new InvalidDataException("AlignmentSets for stitched reads should only have one ReadsForProcessing.");
                }
                foreach (var read in alignmentSet.ReadsForProcessing)
                {
                    var alignment = new BamAlignment(read.BamAlignment);

                    alignment.SetIsFirstMate(false);
                    alignment.SetIsProperPair(false);

                    var tagUtils = new TagUtils();
                    if (read.StitchedCigar != null)
                    {
                        alignment.CigarData = read.StitchedCigar;
                    }
                    if (read.CigarDirections != null)
                    {
                        tagUtils.AddStringTag("XD", read.CigarDirections.ToString());
                    }
                    // if the original reads had UMIs and were collapsed, they will have XU(Z), XV(i), XW(i)
                    // these need to be copied to correctly populate some fields in the called variants
                    if (pair.Read1.TagData != null && pair.Read1.TagData.Length > 0)
                    {
                        var xu = pair.Read1.GetStringTag("XU");
                        if (xu != null)
                            tagUtils.AddStringTag("XU", xu);
                        var xv = pair.Read1.GetIntTag("XV");
                        if (xv.HasValue)
                            tagUtils.AddIntTag("XV", xv.Value);
                        var xw = pair.Read1.GetIntTag("XW");
                        if (xw.HasValue)
                            tagUtils.AddIntTag("XW", xw.Value);
                    }

                    var tagData = tagUtils.ToBytes();

                    var existingTags = alignment.TagData;
                    if (existingTags == null)
                        alignment.TagData = tagData;
                    else
                        alignment.AppendTagData(tagData);
                    
                    reads.Add(alignment);
                }
            }
            else
            {
                if (!_filterUnstitchablePairs)
                {
                    _statusCounter.AddStatusCount("Unstitchable Pairs Kept");
                    reads.Add(new BamAlignment(alignmentSet.PartnerRead1.BamAlignment));
                    reads.Add(new BamAlignment(alignmentSet.PartnerRead2.BamAlignment));
                }
                else
                {
                    _statusCounter.AddStatusCount("Unstitchable Pairs Filtered");
                }
            }

            return reads;
        }

        public void Finish()
        {
            _masterStatusCounter.Merge(_statusCounter);
        }
    }
}