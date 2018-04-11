using System;
using System.Collections.Generic;
using System.Linq;
using RealignIndels.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;
using Common.IO.Utility;

namespace RealignIndels.Logic.Processing
{
    public class GenomeProcessor : BaseGenomeProcessor
    {
        private readonly Dictionary<BamWorkRequest, IRealignmentWriter> _writerLookup = new Dictionary<BamWorkRequest, IRealignmentWriter>();
        private readonly Factory _factory;
        private readonly string _chrFilter;

        public GenomeProcessor(Factory factory, IGenome genome, string chrFilter = null) :
            base(factory.WorkRequests.Where(
                    w => w.GenomeDirectory.Equals(genome.Directory, StringComparison.CurrentCultureIgnoreCase))
                    .ToList(), genome)
        {
            _factory = factory;
            Genome = genome;
            _chrFilter = chrFilter;
        }

        protected override void Initialize()
        {
            foreach (var workRequest in WorkRequests)
            {
                var writer = _factory.CreateWriter(workRequest.BamFilePath, workRequest.OutputFilePath);

                writer.Initialize();

                _writerLookup[workRequest] = writer;    
            }
        }

        protected override void Finish()
        {
            var jobs = new List<IJob>();

            foreach (var writerLookup in _writerLookup)
            {
                jobs.Add(new GenericJob(() =>
                {
                    Logger.WriteToLog("{0}: Finish writing bam", writerLookup.Key.BamFileName);
                    writerLookup.Value.FinishAll();
                }, writerLookup.Key.BamFileName));
            }

            JobManager.Process(jobs);
        }

        protected override void Process(BamWorkRequest workRequest, ChrReference chrReference)
        {
            var bamWriter = _writerLookup[workRequest];

            if (!string.IsNullOrEmpty(_chrFilter) && _chrFilter != chrReference.Name)
            {
                // just write out that chromosome's info as is
                WriteChromosomeReads(workRequest.BamFilePath, chrReference.Name, bamWriter);
            }
            else
            {
                // do realignment
                var caller = _factory.CreateRealigner(chrReference, workRequest.BamFilePath,
                    bamWriter);
                caller.Execute();

                bamWriter.FlushAllBufferedRecords();
            }
        }

        private void WriteChromosomeReads(string inputBamFile, string chrName, IRealignmentWriter writer)
        {
            using (var extractor = _factory.CreateAlignmentExtractor(inputBamFile, chrName))
            {
                var read = new Read();

                while (extractor.GetNextAlignment(read))
                {
                    var bamAlignment = read.BamAlignment;
                    writer.WriteRead(ref bamAlignment, false);
                }
            }

            writer.FlushAllBufferedRecords();
        }
    }
}
