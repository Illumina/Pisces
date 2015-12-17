using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallSomaticVariants.Interfaces;
using SequencingFiles;

namespace CallSomaticVariants.Models
{
    public class Genome : IGenome
    {
        private List<GenomeMetadata.SequenceMetadata> _chrToProcess = new List<GenomeMetadata.SequenceMetadata>();
        private readonly GenomeMetadata _genomeSource;

        public string Directory { get; private set; }

        public List<string> ChromosomesToProcess {
            get { return _chrToProcess.Select(s => s.Name).ToList(); }
            private set
            {
                var inputList = value.ToList();

                // filter from source to make sure these get added in the right order
                _chrToProcess = _genomeSource.Sequences.Where(
                    s => inputList.Contains(s.Name))
                    .ToList();
            }
        }

        public IEnumerable<Tuple<string, long>> ChromosomeLengths
        {
            get { return _chrToProcess.Select(s => new Tuple<string, long>(s.Name, s.Length)); }
        }

        public Genome(string directory, List<string> chrsToProcess)
        {
            Directory = directory;

            // import the genome metadata from the genome folder
            var genomeSizePath = Path.Combine(Directory, "GenomeSize.xml");

            if (!File.Exists(genomeSizePath))
                throw new ArgumentException(string.Format("Cannot load genome '{0}': GenomeSize.xml is missing", Directory));

            try
            {
                _genomeSource = new GenomeMetadata();
                _genomeSource.Deserialize(genomeSizePath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("Cannot load genome '{0}': Unable to read GenomeSize.xml: {1}", Directory, ex.Message));
            }

            foreach (var sequenceMetadata in _genomeSource.Sequences)
            {
                if (!File.Exists(sequenceMetadata.FastaPath))
                {
                    throw new ArgumentException(string.Format("Cannot load genome '{0}': Sequence file '{1}' specified in GenomeSize.xml does not exist.", Directory, sequenceMetadata.FastaPath));                    
                }
                if (!File.Exists(sequenceMetadata.FastaPath + ".fai"))
                {
                    throw new ArgumentException(string.Format("Cannot load genome '{0}': Sequence file '{1}' specified in GenomeSize.xml does not have an index file.", Directory, sequenceMetadata.FastaPath));
                }
            }

            ChromosomesToProcess = chrsToProcess;
        }

        public ChrReference GetChrReference(string chrName)
        {
            var sequenceSource = _genomeSource.GetSequence(chrName);

            if (sequenceSource == null)
            {
                return null;
            }

            return new ChrReference()
            {
                Name = chrName,
                Sequence = sequenceSource.GetBases(true),
                FastaPath = sequenceSource.FastaPath
            };
        }
    }
}
