//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using Pisces.Domain.Interfaces;
//using Pisces.IO;
//using Pisces.Processing.Logic;
//using Pisces.Processing.Utility;

//namespace Pisces.Processing
//{
//    public class MultiProcessProcessor : MultiProcessProcessorBase
//    {
//        protected readonly List<BamWorkRequest> WorkRequests;
//        private readonly Factory _factory;

//        public MultiProcessProcessor(Factory factory, IGenome genome, IJobManager jobManager,
//            string[] commandLineArgs, string outputFolder, string logFolder, string monoPath = null, string exePath = null)
//            : base(genome, jobManager, factory.WorkRequests.Select(x => x.BamFilePath), commandLineArgs, outputFolder ?? Path.GetDirectoryName(factory.WorkRequests[0].OutputFilePath),
//                logFolder, monoPath, exePath)
//        {
//            Genome = genome;
//            WorkRequests = factory.WorkRequests;

//            _factory = factory;
//        }


//        protected override void Finish()
//        {
//            foreach (var workRequest in WorkRequests)
//            {
//                using (var vcfWriter = _factory.CreateVcfWriter(workRequest.OutputFilePath, new VcfWriterInputContext
//                {
//                    ReferenceName = Genome.Directory,
//                    CommandLine = _factory.GetCommandLine(),
//                    SampleName = Path.GetFileName(workRequest.BamFilePath),
//                    ContigsByChr = Genome.ChromosomeLengths
//                }))
//                {
//                    vcfWriter.WriteHeader();
//                }

//                foreach (var chrName in Genome.ChromosomesToProcess)
//                {
//                    var chrOutput = Path.Combine(OutputFolder, chrName, Path.GetFileName(workRequest.OutputFilePath));

//                    if (File.Exists(chrOutput))
//                    {
//                        using (Stream input = File.OpenRead(chrOutput))
//                        using (Stream output = new FileStream(workRequest.OutputFilePath, FileMode.Append,
//                            FileAccess.Write, FileShare.None))
//                        {
//                            input.CopyTo(output); // Using .NET 4
//                        }

//                        File.Delete(chrOutput);
//                    }
//                }
//            }
//            base.Finish();

//        }
//    }
//}
