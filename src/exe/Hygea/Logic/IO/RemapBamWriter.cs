using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealignIndels.Interfaces;
using Alignment.IO.Sequencing;
using Common.IO.Utility;
using Alignment.Domain.Sequencing;
using Common.IO;

namespace RealignIndels.Logic
{
    public class RemapBamWriter : IRealignmentWriter
    {
        private string _inputFile;
        private string _outputFile;

        private string _temp1File;
        private string _temp2File;
        private BamWriter _bamWriter;
        private LinkedList<ReadsForPosition> _readBuffer;

        private int _maxRealignShift;
        private Dictionary<string, RemapInfo> _remappings = new Dictionary<string, RemapInfo>();
        private readonly bool _createIndex;
        private readonly bool _copyUnaligned;

        public int MinimumRealignmentStartPosition = 0;

        public RemapBamWriter(string inputBamFile, string outputFile, int maxRealignShift = 500, bool createIndex = true, bool copyUnaligned = true)
        {
            _maxRealignShift = maxRealignShift;
            _createIndex = createIndex;
            _copyUnaligned = copyUnaligned;

            _inputFile = inputBamFile;
            _outputFile = outputFile;

            var outputFileNoExt = Path.GetFileNameWithoutExtension(_outputFile);
            var outputDir = Path.GetDirectoryName(_outputFile);

            _temp1File = Path.Combine(outputDir, outputFileNoExt + ".tmp1.bam");
            _temp2File = Path.Combine(outputDir, outputFileNoExt + ".tmp2.bam");

            _readBuffer = new LinkedList<ReadsForPosition>();

        }

        public void Initialize()
        {
            var outputDirectory = Path.GetDirectoryName(_outputFile);
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using (var reader = new BamReader(_inputFile))
            {
                var genome = reader.GetReferences();
                
                _bamWriter = new BamWriter(_temp1File, reader.GetHeader(), genome);
            }
        }
        public static string UpdateBamHeader(string bamHeader)
        {
            var headers = bamHeader.Split('\n').ToList();

            var lastPgHeaderIndex = 0;
            var headerLen = headers.Count;
            for (int i = 0; i < headerLen; i++)
            {
                if (headers[i].StartsWith("@PG")) lastPgHeaderIndex = i;
            }

            var hygeaVersion = FileUtilities.LocalAssemblyVersion<Program>();

            headers[lastPgHeaderIndex] += "\n@PG\tID:Pisces PN:Hygea VN:" + hygeaVersion;

            return string.Join("\n", headers);

        }

        public void FinishAll()
        {
            FlushAllBufferedRecords();

            _bamWriter.Close();
            _bamWriter.Dispose();

            using (var writer = GetWriter(_temp2File))
            {
                AdjustMates(_temp1File, writer);
                if (_copyUnaligned)
                    WriteUnalignedReads(writer);
            }

            if (_createIndex)
            {
                Logger.WriteToLog("Creating index");
                new BamIndex().CreateIndexFromBamFile(_temp2File);
                Logger.WriteToLog("Completed creating index");
            }

            // if everything goes well, rename to final destination
            File.Delete(_temp1File);
            File.Delete(_outputFile);
            
            File.Move(_temp2File, _outputFile);
            if (_createIndex)
            {
                var outputBaiFile = _outputFile + ".bai";
                File.Delete(outputBaiFile);

                File.Move(_temp2File + ".bai", outputBaiFile);
            }
        }

        /// <summary>
        /// Seek to the unaligned (and mate-unaligned) reads at the tail of the input file, and write them all out to the output file.
        /// </summary>
        private void WriteUnalignedReads(BamWriter writer)
        {
            Logger.WriteToLog("Writing unaligned reads");
            using (var reader = new BamReader(_inputFile))
            {
                reader.JumpToUnaligned();
                var read = new BamAlignment();
                while (true)
                {
                    var result = reader.GetNextAlignment(ref read, false);
                    if (!result) break;
                    if (read.RefID != -1) continue; // skip over last reads
                    writer.WriteAlignment(read);
                }
            }
        }

        private void FlushBufferedRecords(ReadsForPosition reads)
        {
            foreach (byte[] buffer in reads.Reads)
            {
                _bamWriter.WriteAlignment(buffer);
            }
        }

        public void FlushAllBufferedRecords()
        {
            foreach (var position in _readBuffer)
            {
                foreach (byte[] buffer in position.Reads)
                {
                    _bamWriter.WriteAlignment(buffer);
                }
            }
            _readBuffer.Clear();
            MinimumRealignmentStartPosition = 0;
        }

        /// <summary>
        /// We're finished processing a read.  Add it to the read buffer, and flush 
        /// old nodes to disk.
        /// </summary>
        public void WriteRead(ref BamAlignment read, bool remapped)
        {
            if (remapped)
            {
                var info = new RemapInfo(read.Position,
                    read.Position + (int)read.CigarData.GetReferenceSpan() - 1);
                _remappings[string.Format("{0}-{1}", read.Name, read.IsFirstMate() ? 1 : 2)] = info;
            }

            LinkedListNode<ReadsForPosition> node = _readBuffer.First;
            LinkedListNode<ReadsForPosition> nextNode;
            byte[] buffer = BamWriter.SerializeAlignment(ref read);
            while (node != null)
            {
                // Flush reads that are earlier than the earliest possible read we could see in the future (i.e. 2x max shift less than current read).
                // Reasoning: The realign shift could go in two directions. So, for example with max shift of 10, you could receive 
                // and buffer reads in an order like this: 100, 100, 110, 115 (originally 110, shifted right to 115), 
                // 100 (originally 110, shifted left to 100), 120 (originally 110, shifted right to 120), 
                // 101 (originally 110, shifted left to 100), 121, 130, etc. If you had been flushing reads 
                // using the max shift only as the threshold, upon hitting the 115 (fourth read) you would have 
                // flushed the 100s (because 100 < 115 - 10), even though there still may be 100s coming through. 
                // By the time we hit 121, we know that all of the reads we encounter in the future are going to be > 100 (at minimum, the 121 could represent a max-right-shift from 111 and other 111 reads could be max-left-shifted to 101).
                if (node.Value.Position < read.Position - (_maxRealignShift * 2))
                {
                    MinimumRealignmentStartPosition = Math.Max(MinimumRealignmentStartPosition, node.Value.Position);
                    nextNode = node.Next;
                    FlushBufferedRecords(node.Value);
                    _readBuffer.Remove(node);
                    node = nextNode;
                    continue;
                }
                if (node.Value.Position == read.Position)
                {
                    node.Value.Reads.Add(buffer);
                    return;
                }
                if (node.Value.Position > read.Position)
                {
                    ReadsForPosition reads = new ReadsForPosition();
                    reads.Position = read.Position;
                    reads.Reads.Add(buffer);
                    _readBuffer.AddBefore(node, reads);
                    return;
                }
                node = node.Next;
            }
            ReadsForPosition readList = new ReadsForPosition();
            readList.Position = read.Position;
            readList.Reads.Add(buffer);
            _readBuffer.AddLast(readList);
        }

        private BamWriter GetWriter(string outputFile)
        {
            using (var reader = new BamReader(_inputFile))
            {
                var genome = reader.GetReferences();
                string originalSamHeader = reader.GetHeader();
                var updatedHeader = UpdateBamHeader(originalSamHeader);
                return new BamWriter(outputFile, updatedHeader, genome);
            }
        }

        private void AdjustMates(string tmpFile, BamWriter writer)
        {
            // Second pass: Adjust flags on mates
            Logger.WriteToLog("Writing reads with corrected mate flags, {0} total remapped reads", _remappings.Count);
            var read = new BamAlignment();
            using (var reader = new BamReader(tmpFile))
            {
                while (true)
                {
                    var result = reader.GetNextAlignment(ref read, false);
                    if (!result) break;

                    // Adjust flags as needed:
                    var mateKey = string.Format("{0}-{1}", read.Name, read.IsFirstMate() ? 2 : 1);
                    RemapInfo info;

                    if (!_remappings.TryGetValue(mateKey, out info))
                    {
                        writer.WriteAlignment(read);
                        continue;
                    }

                    if (info.Start == -1)
                    {
                        read.SetIsMateUnmapped(true);
                        read.SetIsProperPair(false);
                        read.FragmentLength = 0;
                    }
                    else
                    {
                        read.MatePosition = info.Start;
                    }
                    if (read.IsMateMapped() && read.IsProperPair())
                    {
                        int readEnd = read.Position + (int) read.CigarData.GetReferenceSpan() - 1;
                        // todo jg - should FragmentLength be 0 if the reads are mapped to diff chrs
                        read.FragmentLength = (read.Position < info.Start
                            ? info.End - read.Position + 1
                            : info.Start - readEnd - 1);
                    }

                    writer.WriteAlignment(read);
                }
            }
        }
    }

    public class ReadsForPosition
    {
        public int Position;
        public List<byte[]> Reads = new List<byte[]>();
    }

    public class RemapInfo
    {
        public int Start;
        public int End;
        public RemapInfo(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
