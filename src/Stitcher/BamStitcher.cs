using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Alignment.IO;
using Alignment.Logic;
using Common.IO.Utility;
using Alignment.IO.Sequencing;
using StitchingLogic;
using Common.IO.Sequencing;
using Common.IO;

namespace Stitcher
{
    public class BamStitcher
    {
        private readonly string _inBam;
        private readonly string _outBam;
        private readonly StitcherOptions _options;
        private readonly string _chrFilter;

        public BamStitcher(string inBam, string outBam, StitcherOptions options, string chrFilter = null)
        {
            _inBam = inBam;
            _outBam = outBam;
            _options = options;
            _chrFilter = chrFilter;
        }

        public void Execute()
        {
            var readStatuses = new ReadStatusCounter();
            var pairHandlers = CreatePairHandlers(readStatuses, _options.NumThreads);
            var stitcherPairFilter = new StitcherPairFilter(_options.FilterDuplicates,
                _options.FilterForProperPairs, CreateDuplicateIdentifier(), readStatuses,
                minMapQuality: _options.FilterMinMapQuality);

            BlockingCollection<Task> taskQueue = null;
            ThreadPool threadPool = null;

            if (_options.NumThreads > 1)
            {
                taskQueue = new BlockingCollection<Task>(4 * _options.NumThreads);
                threadPool = new ThreadPool(taskQueue, _options.NumThreads);
            }

            Logger.WriteToLog(string.Format("Beginning execution of {0}.", _inBam + (_chrFilter !=null ? ":"+_chrFilter : "")));

            using (var bamWriter = CreateBamWriter())
            {
                using (var bamReader = CreateBamReader())
                {
                    var rewriter = new BamRewriter(bamReader, bamWriter, stitcherPairFilter, 
                        pairHandlers, taskQueue, getUnpaired: _options.KeepUnpairedReads, chrFilter: _chrFilter);
                    rewriter.Execute();
                }

                threadPool?.RunToCompletion();

                foreach (var pairHandler in pairHandlers)
                {
                    pairHandler.Finish();
                }

                Logger.WriteToLog("Finished stitching. Starting sort and write.");
                bamWriter.Flush();
            }

            foreach (var readStatus in readStatuses.GetReadStatuses())
            {
                Logger.WriteToLog((_chrFilter ?? "") + " STATUSCOUNT " +  readStatus.Key + ": " + readStatus.Value);
            }

            if (_options.Debug || _options.DebugSummary)
            {
                foreach (var readStatus in readStatuses.GetDebugReadStatuses())
                {
                    Logger.WriteToLog((_chrFilter ?? "") + " STATUSCOUNT " + readStatus.Key + ": " + readStatus.Value);
                }
            }

            Logger.WriteToLog(string.Format("Done writing filtered bam at '{0}'.", _outBam));
        }

        private IBamWriterMultithreaded CreateBamWriter()
        {
            string bamHeader;
            List<GenomeMetadata.SequenceMetadata> bamReferences;
            var refIdMapping = new Dictionary<int, string>();

            using (var reader = new BamReader(_inBam))
            {
                bamReferences = reader.GetReferences();
                var oldBamHeader = reader.GetHeader();
                bamHeader = UpdateBamHeader(oldBamHeader);
                foreach (var referenceName in reader.GetReferenceNames())
                {
                    refIdMapping.Add(reader.GetReferenceIndex(referenceName), referenceName);
                }
            }

            if (_options.SortMemoryGB == 0)
            {
                return new BamWriterMultithreaded(_outBam, bamHeader, bamReferences, _options.NumThreads, 1);
            }
            
            return new BamWriterInMem(_outBam, bamHeader, bamReferences, _options.SortMemoryGB, _options.NumThreads, 1);
        }

	    public static string UpdateBamHeader(string bamHeader)
	    {
		    var headers = bamHeader.Split('\n').ToList();

		    var lastPgHeaderIndex = 0;
		    var headerLen = headers.Count;
		    for(int i =0; i< headerLen;i++)
		    {
			    if (headers[i].StartsWith("@PG")) lastPgHeaderIndex = i;
		    }

		    var piscesVersion = FileUtilities.LocalAssemblyVersion<BamStitcher>();

            headers[lastPgHeaderIndex] += ("\n@PG\tID:Pisces PN:Stitcher VN:" + piscesVersion + " CL:" + string.Join("",Environment.GetCommandLineArgs()));
	     
		    return string.Join("\n", headers);

	    }

	    private IBamReader CreateBamReader()
        {
            return new BamReader(_inBam);
        }

        private List<IReadPairHandler> CreatePairHandlers(ReadStatusCounter readStatuses, int numThreads)
        {
            var handlers = new List<IReadPairHandler>(numThreads);

            var refIdMapping = new Dictionary<int, string>();

            using (var reader = new BamReader(_inBam))
            {
                foreach (var referenceName in reader.GetReferenceNames())
                {
                    refIdMapping.Add(reader.GetReferenceIndex(referenceName), referenceName);
                }
            }

            for (int i = 0; i < numThreads; ++i)
            {
                var stitcher = new BasicStitcher(_options.MinBaseCallQuality, useSoftclippedBases: _options.UseSoftClippedBases,
                    nifyDisagreements: _options.NifyDisagreements, debug: _options.Debug, nifyUnstitchablePairs: _options.NifyUnstitchablePairs, ignoreProbeSoftclips: !_options.StitchProbeSoftclips, maxReadLength: _options.MaxReadLength);


                handlers.Add(new PairHandler(refIdMapping, stitcher, _options.FilterUnstitchablePairs, readStatuses));
            }

            return handlers;
        }

        private IDuplicateIdentifier CreateDuplicateIdentifier()
        {
            if (_options.IdentifyDuplicates)
            {
                return new PositionSequenceDuplicateIdentifier();
            }
            else return new AlignmentFlagDuplicateIdentifier();
        }

    }
}