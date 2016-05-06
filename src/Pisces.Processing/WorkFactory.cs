using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SequencingFiles;
using Pisces.IO;
using Pisces.Processing.Logic;

namespace Pisces.Processing
{
    public abstract class WorkFactory
    {
        private readonly BaseApplicationOptions _baseOptions;
        public List<BamWorkRequest> WorkRequests { get; private set; }

        public WorkFactory(BaseApplicationOptions options)
        {
            _baseOptions = options;
        }

        public Genome GetReferenceGenome(string genomePath)
        {
            var chromosomeNames = new List<string>();
            var bamWorkRequests =
                WorkRequests.Where(
                    w => w.GenomeDirectory.Equals(genomePath, StringComparison.InvariantCultureIgnoreCase)).ToList();

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

            var genome = new Genome(genomePath, chromosomeNames.Distinct().ToList(), customOrderChromosomes: true);

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
