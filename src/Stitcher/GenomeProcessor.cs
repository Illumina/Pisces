using System;
using System.Collections.Generic;
using System.IO;
using Pisces.Processing.Utility;
using Alignment.IO.Sequencing;
using Alignment.Domain.Sequencing;
using Common.IO.Sequencing;

namespace Stitcher
{
    public class GenomeProcessor : IStitcherProcessor
    {
        private readonly string _inputBam;
        private List<string> _chroms;
        private string _header;
        private List<GenomeMetadata.SequenceMetadata> _references;


        public GenomeProcessor(string inputBam)
        {
            _inputBam = inputBam;
            Initialize();
        }

        private void Initialize()
        {
            var baseReader = new BamReader(_inputBam);
            _chroms = baseReader.GetReferenceNames();
            _header = baseReader.GetHeader();
            _references = baseReader.GetReferences();

        }

        public void Process(string inputBam, string outFolder, StitcherOptions stitcherOptions)
        {

            var jobManager = new JobManager(10);
            var jobs = new List<IJob>();
            var perChromBams = new List<string>();

            // Process each of the chromosomes separately
            foreach (var chrom in _chroms)
            {
                var intermediateOutput = Path.Combine(outFolder, Path.GetFileNameWithoutExtension(inputBam) + "." + chrom + ".stitched.bam");
                perChromBams.Add(intermediateOutput);
                var stitcher = new BamStitcher(inputBam, intermediateOutput, stitcherOptions, chrFilter: chrom);
                jobs.Add(new GenericJob(() => stitcher.Execute()));
            }

            jobManager.Process(jobs);

            // Combine the per-chromosome bams
            Logger.WriteToLog("Writing final bam.");

            var outputBam = Path.Combine(outFolder, Path.GetFileNameWithoutExtension(inputBam) + ".final.stitched.bam");
            using (var finalOutput = new BamWriter(outputBam, _header, _references))
            {
                foreach (var bam in perChromBams)
                {
                    Logger.WriteToLog("Adding " + bam + " to final bam.");
                    var bamAlignment = new BamAlignment();

                    using (var bamReader = new BamReader(bam))
                    {
                        while (true)
                        {
                            var hasMoreReads = bamReader.GetNextAlignment(ref bamAlignment, false);
                            if (!hasMoreReads) break;
                            finalOutput.WriteAlignment(bamAlignment);
                        }

                    }

                    File.Delete(bam);
                }
            }

            Logger.WriteToLog("Finished combining per-chromosome bams into final bam at " + outputBam);

        }
    }
}