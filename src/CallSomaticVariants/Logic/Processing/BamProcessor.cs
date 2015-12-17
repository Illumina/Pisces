using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants.Logic.Processing
{
    public class 
        BamProcessor : BaseProcessor
    {
        private readonly string _inputBamFilePath;
        private readonly string _outputVcfFilePath;

        public BamProcessor(Factory factory, IGenome genome)
        {
            var workRequest = factory.WorkRequests[0];
            _inputBamFilePath = workRequest.BamFilePath;
            _outputVcfFilePath = workRequest.VcfFilePath;
            Factory = factory;
            Genome = genome;
        }

        public override void InternalExecute(int maxThreads)
        {
            Logger.WriteToLog("{0}: Start processing.", Path.GetFileName(_inputBamFilePath));
            var startTime = DateTime.UtcNow;

            // for each chromosome, write a chromosome specific vcf with no header.
            // then combine after
            var jobManager = new JobManager(maxThreads);

            var jobs = new List<IJob>();
            for (var i = 0; i < Genome.ChromosomesToProcess.Count(); i ++)
            {
                var chromosome = Genome.ChromosomesToProcess[i];

                jobs.Add(new GenericJob(() => ProcessByChr(chromosome)));
            }

            jobManager.Process(jobs);

            CombinePerChromosomeFiles();

            Logger.WriteToLog("{0}: Completed processing in {1}s.", Path.GetFileName(_inputBamFilePath),
                DateTime.UtcNow.Subtract(startTime).TotalSeconds);
        }

        private void CombinePerChromosomeFiles()
        {
            //TODO combine the bias output files
            using (var vcfWriter = Factory.CreateVcfWriter(_outputVcfFilePath, new VcfWriterInputContext
            {
                ReferenceName = Genome.Directory,
                CommandLine = Factory.GetCommandLine(),
                SampleName = Path.GetFileName(_inputBamFilePath),
                ContigsByChr = Genome.ChromosomeLengths
            }))
            {
                vcfWriter.WriteHeader();
            }

            foreach (var chrName in Genome.ChromosomesToProcess)
            {
                var chrFilePath = GetChrOutputPath(chrName);
                File.AppendAllText(_outputVcfFilePath, File.ReadAllText(chrFilePath));
                File.Delete(chrFilePath);
            }
        }

        private string GetChrOutputPath(string chrName)
        {
            return _outputVcfFilePath + "_" + chrName;
        }

        private void ProcessByChr(string chrName)
        {
            ChrReference chrReference = null;
            var bamFileName = Path.GetFileName(_inputBamFilePath);

            try
            {
                var loadStartTime = DateTime.Now;
                chrReference = Genome.GetChrReference(chrName);
                Logger.WriteToLog("Loaded chromosome '{0}' in {1} secs", chrName, DateTime.Now.Subtract(loadStartTime).TotalSeconds);

                if (chrReference == null)
                {
                    Logger.WriteToLog("Unable to find {0} in the reference genome.", chrName);
                    Logger.WriteToLog("Skipping {0}.", chrName);
                }
                else
                {
                    var outputVcfPath = GetChrOutputPath(chrReference.Name);

                    Logger.WriteToLog("{1}: Start processing chr '{0}'.", chrReference.Name, bamFileName);
                    var startTime = DateTime.UtcNow;

                    using (var vcfWriter = Factory.CreateVcfWriter(outputVcfPath, new VcfWriterInputContext()))
                    using (var biasFileWriter = Factory.CreateBiasFileWriter(outputVcfPath))
                    {
                        var caller = Factory.CreateSomaticVariantCaller(chrReference, _inputBamFilePath, vcfWriter,
                            biasFileWriter);
                        caller.Execute();
                    }

                    var processingTime = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                    Logger.WriteToLog("{2}: Completed processing chr '{1}' in {0}s.", processingTime, chrReference.Name, bamFileName);
                    TrackTime(_inputBamFilePath, processingTime);
                }
            }
            catch (Exception ex)
            {
                var wrappedException = new Exception(string.Format("{2}: Error processing chr '{0}': {1}", chrName, ex.Message, bamFileName), ex);
                Logger.WriteExceptionToLog(wrappedException);

                lock (this)
                    Exceptions.Add(ex);
            }
            finally
            {
                if (chrReference != null)
                    chrReference.Sequence = null; // clear sequence so we dont hold it in memory
            }
        }
    }
}
