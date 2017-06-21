using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO;
using Pisces.Domain.Options;
using Pisces.Processing.Logic;
using Pisces.Processing.Utility;
using Alignment.IO.Sequencing;
using Common.IO.Utility;

namespace Pisces.Processing
{
    public abstract class WorkFactory
    {
        private readonly BamProcessorOptions _baseOptions;
        public List<BamWorkRequest> WorkRequests { get; private set; }

        public WorkFactory(BamProcessorOptions options)
        {
            _baseOptions = options;
        }

        public Genome GetReferenceGenome(string genomePath)
        {
            var chromosomeNames = new List<string>();
            var bamWorkRequests =
                WorkRequests.Where(
                    w => w.GenomeDirectory.Equals(genomePath, StringComparison.CurrentCultureIgnoreCase)).ToList();

            for (var i = 0; i < bamWorkRequests.Count(); i++)
            {
                var bamFilePath = bamWorkRequests[i].BamFilePath;
                List<string> bamChromosomes;
                using (var reader = new BamReader(bamFilePath))
                {
                    bamChromosomes = reader.GetReferenceNames();
                }

                var filteredChromosomes = FilterBamChromosomes(bamChromosomes, bamFilePath);

                if (!string.IsNullOrEmpty(_baseOptions.ChromosomeFilter))
                {
                    filteredChromosomes.RemoveAll(c => c != _baseOptions.ChromosomeFilter);
                }

                chromosomeNames.AddRange(filteredChromosomes);
            }

            var genome = new Genome(genomePath, chromosomeNames.Distinct().ToList());

            if (genome.ChromosomesToProcess.Count() < chromosomeNames.Distinct().Count())
            {
                Logger.WriteToLog("Warning: Not all requested sequences were found in {0} to process.", genome.GetGenomeBuild());
                Logger.WriteToLog("Check BAM file matches reference genome.");

                if (string.IsNullOrEmpty(_baseOptions.ChromosomeFilter))
                    Logger.WriteToLog("Requested sequences: {0}", (string.Join(",", chromosomeNames.Distinct().ToList())));
                else
                    Logger.WriteToLog("Requested sequences: {0}", _baseOptions.ChromosomeFilter);

            }    
            
            return genome;
        }

        protected virtual List<string> FilterBamChromosomes(List<string> bamChromosomes, string bamFilePath)
        {
            return bamChromosomes;
        }

        protected void UpdateWorkRequests()
        {
            if (_baseOptions.BAMPaths == null)
                return;

            WorkRequests = new List<BamWorkRequest>();

            for (var i = 0; i < _baseOptions.BAMPaths.Length; i++)
            {
                WorkRequests.Add(new BamWorkRequest
                {
                    BamFilePath = _baseOptions.BAMPaths[i],
                    OutputFilePath = GetOutputFile(_baseOptions.BAMPaths[i]),
                    GenomeDirectory = _baseOptions.GenomePaths.Length == 1 ? _baseOptions.GenomePaths[0] : _baseOptions.GenomePaths[i],
                });
            }
        }

        public abstract string GetOutputFile(string inputBamPath);
    }
}
