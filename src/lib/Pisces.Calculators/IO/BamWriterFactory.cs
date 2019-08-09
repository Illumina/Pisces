using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alignment.IO;
using Alignment.IO.Sequencing;
using Common.IO;
using Common.IO.Sequencing;

namespace Gemini.IO
{
    public class BamWriterFactory : IBamWriterFactory
    {
        private readonly int _numThreads;
        private readonly string _header;
        private readonly List<GenomeMetadata.SequenceMetadata> _references;

        public BamWriterFactory(int numThreads, string inBamForHeaderInformation, int cacheSize = 64)
        {
            _numThreads = numThreads;
            _header = GetHeader(inBamForHeaderInformation);
            _references = GetReferences(inBamForHeaderInformation);
        }

        public IBamWriterHandle CreateSingleBamWriter(string outBam)
        {
            if (File.Exists(outBam))
            {
                Console.WriteLine("ERROR: " + $"Bam file '{outBam}' already exists. Proceeding now would overwrite it.");
                throw new ArgumentException($"Bam file '{outBam}' already exists. Proceeding now would overwrite it.");
            }
            return new BamWriterHandle(new BamWriter(outBam, _header, _references));
            //return new CachedBamWriter(new BamWriter(outBam, _header, _references), _cacheSize);
        }

        private List<GenomeMetadata.SequenceMetadata> GetReferences(string inBam)
        {
            List<GenomeMetadata.SequenceMetadata> bamReferences;
            var refIdMapping = new Dictionary<int, string>();

            using (var reader = new BamReader(inBam))
            {
                bamReferences = reader.GetReferences();
                foreach (var referenceName in reader.GetReferenceNames())
                {
                    refIdMapping.Add(reader.GetReferenceIndex(referenceName), referenceName);
                }
            }

            return bamReferences;
        }

        private string GetHeader(string inBam)
        {
            using (var reader = new BamReader(inBam))
            {
                var oldBamHeader = reader.GetHeader();
                return UpdateBamHeader(oldBamHeader);
            }

        }

        private static string UpdateBamHeader(string bamHeader)
        {
            var headers = bamHeader.Split('\n').ToList();

            var lastPgHeaderIndex = 0;
            var headerLen = headers.Count;
            for (int i = 0; i < headerLen; i++)
            {
                if (headers[i].StartsWith("@PG")) lastPgHeaderIndex = i;
            }

            var geminiVersion = FileUtilities.LocalAssemblyVersion<GeminiWorkflow>();

            headers[lastPgHeaderIndex] += ("\n@PG\tID:Gemini PN:Gemini VN:" + geminiVersion + " CL:" + string.Join("", Environment.GetCommandLineArgs()));

            return string.Join("\n", headers);

        }

    }
}