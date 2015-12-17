using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants.Logic.Processing
{
    public class GenomeProcessor : BaseProcessor
    {
        private readonly List<BamWorkRequest> _workRequests;
        private readonly Dictionary<BamWorkRequest, VcfFileWriter> _writerLookup = new Dictionary<BamWorkRequest, VcfFileWriter>();
        private ChrReference _currentlyProcessingChr;
        private readonly Dictionary<BamWorkRequest, StrandBiasFileWriter> _biasWriterLookup = new Dictionary<BamWorkRequest, StrandBiasFileWriter>();

        public GenomeProcessor(Factory factory, IGenome genome)
        {
            Factory = factory;
            Genome = genome;
            _workRequests =
                factory.WorkRequests.Where(
                    w => w.GenomeDirectory.Equals(genome.Directory, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
        }

        public override void InternalExecute(int maxThreads)
        {
            try
            {
                Logger.WriteToLog("Processing genome '{0}'", Genome.Directory);

                InitializeWriters();

                var jobManager = new JobManager(maxThreads);

                // process each chr sequentially
                // for each chr, process bams in parallel
                foreach (var chrName in Genome.ChromosomesToProcess)
                {
                    Logger.WriteToLog("Processing chromosome '{0}'", chrName);

                    var startTime = DateTime.Now;
                    _currentlyProcessingChr = Genome.GetChrReference(chrName);

                    Logger.WriteToLog("Loaded chromosome '{0}' in {1} secs", chrName, DateTime.Now.Subtract(startTime).TotalSeconds);

                    if (_currentlyProcessingChr == null)
                    {
                        Logger.WriteToLog("Unable to find {0} in the reference genome.", chrName);
                        Logger.WriteToLog("Skipping {0}.", chrName);
                        continue;
                    }

                    var jobs = new List<IJob>();
                    for (var i = 0; i < _workRequests.Count(); i ++)
                    {
                        var workRequest = _workRequests[i];
                        jobs.Add(new GenericJob(() => ProcessByBam(workRequest)));
                    }

                    jobManager.Process(jobs);

                    _currentlyProcessingChr.Sequence = null; // clear sequence so we dont hold it in memory
                }
            }
            finally
            {
                CloseWriters();
            }
        }

        private void InitializeWriters()
        {
            foreach (var workRequest in _workRequests)
            {
                var vcfWriter = Factory.CreateVcfWriter(workRequest.VcfFilePath, new VcfWriterInputContext
                {
                    ReferenceName = Genome.Directory,
                    CommandLine = Factory.GetCommandLine(),
                    SampleName =  Path.GetFileName(workRequest.BamFilePath),
                    ContigsByChr = Genome.ChromosomeLengths
                });

                vcfWriter.WriteHeader();

                _writerLookup[workRequest] = vcfWriter;

                var biasFileWriter = Factory.CreateBiasFileWriter(workRequest.VcfFilePath);

                _biasWriterLookup[workRequest] = biasFileWriter;
            }
        }

        private void CloseWriters()
        {
            foreach(var writer in _writerLookup.Values)
                writer.Dispose();
            foreach (var writer in _biasWriterLookup.Values.Where(writer => writer!=null))
            {
                writer.Dispose();
            }
        }

        private void ProcessByBam(BamWorkRequest workRequest)
        {
            var bamFileName = workRequest.BamFileName;

            try
            {
                Logger.WriteToLog(string.Format("{0}: Start processing chr '{1}'.", bamFileName, _currentlyProcessingChr.Name));
                var startTime = DateTime.UtcNow;

                var caller = Factory.CreateSomaticVariantCaller(_currentlyProcessingChr, workRequest.BamFilePath, _writerLookup[workRequest], _biasWriterLookup[workRequest]);
                caller.Execute();

                var processingTime = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                Logger.WriteToLog(string.Format("{0}: Completed processing chr '{2}' in {1}s", bamFileName, processingTime, _currentlyProcessingChr.Name));
                TrackTime(workRequest.BamFilePath, processingTime);
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception(string.Format("{1}: Error processing chr '{0}': {2}", _currentlyProcessingChr.Name, bamFileName, ex.Message), ex);
                Logger.WriteExceptionToLog(wrappedException);

                lock (this)
                    Exceptions.Add(ex);
            }
        }
    }
}
