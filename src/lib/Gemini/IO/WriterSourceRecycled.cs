using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alignment.IO;
using Gemini.Interfaces;
using Gemini.Types;

namespace Gemini.IO
{
    public class WriterSourceRecycled : IWriterSource
    {
        private string _outputBam;
        private readonly IBamWriterFactory _writerFactory;
        private ConcurrentQueue<IBamWriterHandle> _handles;
        private ConcurrentBag<IBamWriterHandle> _allHandles = new ConcurrentBag<IBamWriterHandle>();
        private ConcurrentDictionary<string, int> _bamFileAlignmentsWritten = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> _bamFileTimesUsed = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<IBamWriterHandle, string> _bamFileHandleNames = new ConcurrentDictionary<IBamWriterHandle, string>();

        private readonly ConcurrentBag<string> _bamFiles = new ConcurrentBag<string>();

        public WriterSourceRecycled(string outputBam,
            IBamWriterFactory writerFactory)
        {
            _outputBam = outputBam;
            _writerFactory = writerFactory;
            _handles = new ConcurrentQueue<IBamWriterHandle>();
        }

        private static string GetPathStub(string outStub, string chrom, PairClassification classification)
        {
            var outPath = Path.Combine(outStub + "_" + (int)classification + "_" + chrom + "_" + "All" + "_" + "All");
            return outPath;
        }

        public void Finish()
        {
            var finished = new List<string>();

            foreach (var item in _allHandles)
            {
                try
                {
                    item.WriteAlignment(null);

                    var bamPath = _bamFileHandleNames[item];
                    finished.Add(bamPath);
                    Console.WriteLine($"Finished writing {bamPath} ({_bamFileTimesUsed[bamPath]} writes, {_bamFileAlignmentsWritten[bamPath]} alignments)");
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: Failed to finish bam file. May have already been disposed. See exception for details.");
                    Console.WriteLine(e);
                }


            }

            foreach (var bamPath in _bamFileAlignmentsWritten.Keys)
            {
                if (!finished.Contains(bamPath))
                {
                    Console.WriteLine($"Did not finish writing {bamPath} ({_bamFileTimesUsed[bamPath]} writes, {_bamFileAlignmentsWritten[bamPath]} alignments)");
                }
            }


        }

        public List<string> GetBamFiles()
        {
            return _bamFiles.ToList();
        }

        public IBamWriterHandle BamWriterHandle(string chrom,
            PairClassification classification,
            int idNum)
        {
            IBamWriterHandle writerHandle;
            if (_handles.Count == 0)
            {
                var outStub = GetPathStub(_outputBam, chrom, classification);
                var path =
                    $"{GetPathStub(outStub, chrom, classification)}_{idNum}_{Thread.CurrentThread.ManagedThreadId}_{_allHandles.Count}_{Guid.NewGuid()}";

                writerHandle = _writerFactory.CreateSingleBamWriter(path);
                _allHandles.Add(writerHandle);
                _bamFileHandleNames.AddOrUpdate(writerHandle, path, (h, p) =>
                {
                    Console.WriteLine($"Path already existed for handle: {p} vs {path}.");
                    return p;
                });

                _bamFileAlignmentsWritten.AddOrUpdate(path, 0, (n, i) => i + 0);
                _bamFileTimesUsed.AddOrUpdate(path, 1, (n, i) => i + 1);

                _bamFiles.Add(path);

            }
            else
            {
                _handles.TryDequeue(out writerHandle);
                if (writerHandle == null)
                {
                    var outStub = GetPathStub(_outputBam, chrom, classification);
                    var path =
                        $"{GetPathStub(outStub, chrom, classification)}_{idNum}_{Thread.CurrentThread.ManagedThreadId}_{_allHandles.Count}_{Guid.NewGuid()}";

                    writerHandle = _writerFactory.CreateSingleBamWriter(path);
                    _allHandles.Add(writerHandle);
                    _bamFileHandleNames.AddOrUpdate(writerHandle, path, (h, p) =>
                    {
                        Console.WriteLine($"Path already existed for handle: {p} vs {path}.");
                        return p;
                    });

                    _bamFileAlignmentsWritten.AddOrUpdate(path, 0, (n, i) => i + 0);
                    _bamFileTimesUsed.AddOrUpdate(path, 1, (n, i) => i + 1);

                    _bamFiles.Add(path);
                }
            }

 
            return writerHandle;
        }

        public void DoneWithWriter(string chrom, PairClassification classification, int idNum, int numWritten = 0, IBamWriterHandle handle = null)
        {
            if (handle != null)
            {
                var name = _bamFileHandleNames[handle];
                _bamFileAlignmentsWritten.AddOrUpdate(name, numWritten, (n, i) => { return i + numWritten; });
                _bamFileTimesUsed.AddOrUpdate(name, 1, (n, i) => { return i + 1; });
                _handles.Enqueue(handle);
            }
        }


    }
}