//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using Pisces.Domain.Interfaces;
//using Pisces.IO;
//using Pisces.Processing.Logic;
//using Pisces.Processing.Utility;
//using Alignment.IO.Sequencing;
//using Alignment.Domain.Sequencing;
//using Common.IO.Sequencing;

//namespace RealignIndels.Logic.Processing
//{
//    public class MultiProcessProcessor : MultiProcessProcessorBase
//    {
//        protected readonly List<BamWorkRequest> WorkRequests;
//        private Factory _factory;

//        public MultiProcessProcessor(Factory factory, IGenome genome, IJobManager jobManager,
//            string[] commandLineArgs, string outputFolder, string logFolder, string monoPath = null, string exePath = null)
//            : base(genome, jobManager, factory.WorkRequests.Select(x => x.BamFilePath).ToArray(), commandLineArgs,
//                  outputFolder ?? Path.GetDirectoryName(factory.WorkRequests[0].OutputFilePath),
//                logFolder, monoPath, exePath)
//        {
//            Genome = genome;
//            WorkRequests = factory.WorkRequests;

//            _factory = factory;
//        }


//        protected override void Finish()
//        {
//            var alignment = new BamAlignment();
//            foreach (var workRequest in WorkRequests)
//            {
//                // merge the BAMs
//                string header;
//                List<GenomeMetadata.SequenceMetadata> references;
//                using (var bamReader = new BamReader(workRequest.BamFilePath))
//                {
//                    header = bamReader.GetHeader();
//                    references = bamReader.GetReferences();
//                }
//                var resultFileName = _factory.GetOutputFile(workRequest.BamFilePath);
//                using (var bamWriter = new BamWriter(resultFileName, header, references))
//                {
//                    var filesToProcess = Genome.ChromosomesToProcess
//                        .Select(
//                            chrName => Path.Combine(OutputFolder, chrName, Path.GetFileName(workRequest.OutputFilePath)))
//                        .Where(File.Exists).ToList();
//                    foreach (var input in filesToProcess)
//                    {
//                        using (var bamReader = new BamReader(input))
//                        {
//                            while (bamReader.GetNextAlignment(ref alignment, false))
//                            {
//                                bamWriter.WriteAlignment(alignment);
//                            }
//                        }
//                        File.Delete(input);
//                    }
//                    // add on the unaligned reads
//                    using (var bamReader = new BamReader(workRequest.BamFilePath))
//                    {
//                        bamReader.JumpToUnaligned();
//                        var read = new BamAlignment();
//                        while (true)
//                        {
//                            var result = bamReader.GetNextAlignment(ref alignment, false);
//                            if (!result) break;
//                            if (read.RefID != -1) continue; // skip over last reads
//                            bamWriter.WriteAlignment(alignment);
//                        }
//                    }
//                }
//                new BamIndex().CreateIndexFromBamFile(resultFileName);
//            }
//            base.Finish();
//        }
//    }
//}
