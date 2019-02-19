using System.Collections.Generic;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Alignment.IO;
using Alignment.Logic;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Types;

namespace Gemini.ClassificationAndEvidenceCollection
{
    public class ClassificationAndCollectionExtractReadsTask : WaitForFinishTask
    {
        private List<IReadPairHandler> _pairHandlers;
        private readonly List<IReadPairClassifierAndExtractor> _classifiers;
        private readonly List<IndelTargetFinder> _targetFinders;
        private readonly List<ReadPair> _readPairs;
        private readonly Dictionary<string, Dictionary<PairClassification, List<IBamWriterHandle>>> _bamWriterHandles;
        private Dictionary<PairClassification, int[]> _classificationDict;
        private readonly List<Dictionary<string, int[]>> _indelCounts;
        private readonly Dictionary<int, string> _refIdMapping;
        private const int MinMapQualityForEvidence = 10; //TODO

        public ClassificationAndCollectionExtractReadsTask(List<IReadPairHandler> pairHandlers, List<IReadPairClassifierAndExtractor> classifiers, List<IndelTargetFinder> targetFinders, List<ReadPair> readPairs,
            Dictionary<PairClassification, int[]> classificationDict,
            Dictionary<string, Dictionary<PairClassification, List<IBamWriterHandle>>> bamWriterHandles, List<Dictionary<string, int[]>> indelCounts, Dictionary<int,string> refIdMapping, bool trustSoftclips = false) : base()
        {
            _pairHandlers = pairHandlers;
            _readPairs = readPairs;
            _bamWriterHandles = bamWriterHandles;
            _indelCounts = indelCounts;
            _refIdMapping = refIdMapping;
            _classificationDict = classificationDict;
            _classifiers = classifiers;
            _targetFinders = targetFinders;
        }

        public override void ExecuteImpl(int threadNum)
        {
            var pairHandler = _pairHandlers[threadNum];
            var classifier = _classifiers[threadNum];
            var targetFinder = _targetFinders[threadNum];

            foreach (ReadPair readPair in _readPairs)
            {
                var bamAlignmentList = classifier.GetBamAlignmentsAndClassification(readPair, pairHandler,
                    out PairClassification classification, out bool hasIndels, out int numMismatchesInSingleton, out bool isSplit);

                _classificationDict[classification][threadNum]++;


                // TODO make this optional
                if (classification == PairClassification.Unusable)
                {
                    // Don't do anything with these reads (TBD, maybe we want to output them somewhere)
                    continue;
                }


                if (hasIndels)
                {

                    foreach (var bamAlignment in bamAlignmentList)
                    {

                        var chrom = _refIdMapping[bamAlignment.RefID];

                        var lookup = _indelCounts[threadNum];
                        var isReputable = classification == PairClassification.PerfectStitched ||
                                          classification == PairClassification.ImperfectStitched ||
                                          classification == PairClassification.UnstitchIndel;

                        if (!bamAlignment.IsMapped() || bamAlignment.RefID < 0)
                        {
                            // TODO maybe output to unmapped instead
                            continue;
                        }

                        IndelEvidenceHelper.FindIndelsAndRecordEvidence(bamAlignment, targetFinder, lookup, isReputable, chrom, MinMapQualityForEvidence, classification == PairClassification.ImperfectStitched || classification == PairClassification.MessyStitched || classification == PairClassification.PerfectStitched);
                    }
                }

                foreach (var bamAlignment in bamAlignmentList)
                {

                    if (classification == PairClassification.UnusableSplit)
                    {
                        bamAlignment.MapQuality = (uint) (bamAlignment.MapQuality - numMismatchesInSingleton);
                    }
                    var chrom = _refIdMapping[bamAlignment.RefID];

                    TagUtils.ReplaceOrAddStringTag(ref bamAlignment.TagData, "RC", classification.ToString());

                    // Note the treatment of fusion reads - need to come back to this
                    _bamWriterHandles[isSplit ? "Split" : chrom][classification][threadNum].WriteAlignment(bamAlignment);

                }
            }
        }


    }

}