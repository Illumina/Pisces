using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.IO.Sequencing;
using Common.IO.Utility;
using Alignment.Domain.Sequencing;

namespace Alignment.IO.Sequencing
{
    public class BamWriterInMem : BamWriter, IBamWriterMultithreaded
    {
        public class BamWriterHandle : IBamWriterHandle
        {
            BamWriterInMem _bamWriter;
            int _threadNumber;

            public BamWriterHandle(BamWriterInMem bamWriter, int threadNumber)
            {
                _bamWriter = bamWriter;
                _threadNumber = threadNumber;
            }

            public void WriteAlignment(BamAlignment al)
            {
                _bamWriter.WriteAlignment(al, _threadNumber);
            }
        }

        // Byte array that can be bigger than a byte[] can be.
        // Internally, it stores several byte[].
        protected class BigByteArray
        {
            public BigByteArray(UInt64 sizeInBytes)
            {
                _sizeInBytes = sizeInBytes;

                // Figure out how many byte[] we need.
                uint numArrays = (uint)(sizeInBytes / GetMaxArraySize()) + (uint)(sizeInBytes % GetMaxArraySize() == 0 ? 0 : 1);
                _byteArray = new byte[numArrays][];

                for (int i = 0; i < numArrays - 1; ++i)
                {
                    _byteArray[i] = new byte[GetMaxArraySize()];
                }

                // The last one is smaller than the maximum size.
                _byteArray[numArrays - 1] = new byte[numArrays % GetMaxArraySize() == 0 ? GetMaxArraySize() : sizeInBytes % GetMaxArraySize()];
            }

            public UInt64 SizeInBytes
            {
                get { return _sizeInBytes; }
            }

            public byte this[UInt64 key]
            {
                get
                {
                    return _byteArray[key / GetMaxArraySize()][key % GetMaxArraySize()];
                }
                set
                {
                    _byteArray[key / GetMaxArraySize()][key % GetMaxArraySize()] = value;
                }
            }

            public bool GetByteArrayAndOffset(UInt64 largeOffset,
                                              ref byte[] byteArray,
                                              ref int offset)
            {
                UInt64 index = largeOffset / GetMaxArraySize();
                if (index >= (ulong)_byteArray.Length)
                {
                    // Out of space
                    return false;
                }

                byteArray = _byteArray[index];
                offset = (int)(largeOffset % GetMaxArraySize());

                return true;
            }

            public void Clear()
            {
                foreach (byte[] byteArray in _byteArray)
                {
                    Array.Clear(byteArray, 0, byteArray.Length);
                }
            }

            // I'd like to get this value from some kinda of system call...
            // I'm not sure why it's not max int, but searching online give me this value.
            protected static UInt64 MAX_ARRAY_SIZE = 2147483591;

            public virtual UInt64 GetMaxArraySize() { return MAX_ARRAY_SIZE; }

            protected byte[][] _byteArray;
            protected UInt64 _sizeInBytes;
        }

        protected class SerializedAlignmentContainer
        {
            public SerializedAlignmentContainer(UInt64 sizeInBytes)
            {
                // Reduce the size to 50% to ensure we don't exceed limits.
                // Need to save some memory for _bamAlignmentList as well.
                // Also, tests (on linux with mono) show that 3-4 GB of data is
                // briefly used after sorting, presumably while writing the
                // data. It's not clear to me what is using this memory, but
                // until I track that down, a 50% limit should work, albeit not
                // as efficiently as possible.
                _serializedAlignments = new BigByteArray(5 * (sizeInBytes / 10));

                _bamAlignmentList = new List<SerializedBamAlignment>();
                _offset = 0;
            }

            public bool AddSerializedAlignment(ref BamAlignment al)
            {
                byte[] byteArray = null;
                int smallOffset = 0;
                if (!_serializedAlignments.GetByteArrayAndOffset(
                     _offset,
                     ref byteArray,
                     ref smallOffset))
                {
                    // Out of space
                    return false;
                }

                int smallOffsetInitial = smallOffset;
                if (!SerializeAlignment(ref al, ref byteArray, ref smallOffset))
                {
                    // It didn't fit in the subarray. Try the next one.
                    // This math moves to the next array. For example, say the
                    // max size for 1 array is 1000, and we were at 1987.
                    // 1000 - (1987 - 1000 * (1987/1000)) = 13.
                    // 1987 + 13 = 2000
                    // 2000 is the first element of array number 2. (indexes start at 0).
                    _offset += _serializedAlignments.GetMaxArraySize() - (_offset - _serializedAlignments.GetMaxArraySize() * (_offset / _serializedAlignments.GetMaxArraySize()));
                    if (!_serializedAlignments.GetByteArrayAndOffset(
                         _offset,
                         ref byteArray,
                         ref smallOffset))
                    {
                        // Out of space
                        return false;
                    }

                    smallOffsetInitial = smallOffset;

                    if (!SerializeAlignment(ref al, ref byteArray, ref smallOffset))
                    {
                        // We just checked that we have space. This should never fail.
                        throw new InvalidOperationException("Error: Check available memory. Serialization of alignment failed.");
                    }
                }

                int alignmentSize = smallOffset - smallOffsetInitial;

                if (_bamAlignmentList.Count == 1000)
                {
                    // Assume the first 1000 records are representative of the typical size
                    // Add 20% to ensure a memory reallocation is unlikely.
                    _bamAlignmentList.Capacity = (int)(1.2 * _serializedAlignments.SizeInBytes / (_offset / 1000));
                }
                _bamAlignmentList.Add(new SerializedBamAlignment(
                    _offset,
                    alignmentSize,
                    al.RefID,
                    al.Position,
                    al.AlignmentFlag,
                    al.FragmentLength,
                    al.MapQuality,
                    al.MatePosition,
                    al.MateRefID,
                    al.IsReverseStrand()));

                _offset += (UInt64)alignmentSize;

                return true;
            }

            public void Sort(BamAlignmentComparer comparer)
            {
                _bamAlignmentList.Sort(comparer);
            }

            public void write(BamWriter writer,
                              int index)
            {
                SerializedBamAlignment alignment = _bamAlignmentList[index];

                byte[] byteArray = null;
                int smallOffset = 0;
                _serializedAlignments.GetByteArrayAndOffset(alignment.SerializedOffset, ref byteArray, ref smallOffset);
                writer.Write(byteArray, (uint)alignment.SerializedSize, smallOffset);
            }

            public SerializedBamAlignment GetBamAlignment(int index)
            {
                return _bamAlignmentList[index];
            }

            public int Count
            {
                get
                {
                    return _bamAlignmentList.Count;
                }
            }

            public void Clear()
            {
                _offset = 0;
                _bamAlignmentList.Clear();
                _serializedAlignments.Clear();
            }

            // Avoid string allocations by iterating over the internal byte array.
            public static int CompareNames(SerializedBamAlignment alignment1,
                                           SerializedBamAlignment alignment2,
                                           SerializedAlignmentContainer alignmentContainer1,
                                           SerializedAlignmentContainer alignmentContainer2)
            {
                byte[] byteArray1 = null;
                int offset1 = 0;
                alignmentContainer1._serializedAlignments.GetByteArrayAndOffset(
                    alignment1.SerializedOffset,
                    ref byteArray1,
                    ref offset1);

                byte[] byteArray2 = null;
                int offset2 = 0;
                alignmentContainer2._serializedAlignments.GetByteArrayAndOffset(
                    alignment2.SerializedOffset,
                    ref byteArray2,
                    ref offset2);

                // The name starts at the 36th byte
                UInt32 alignmentNamePos = 36;

                offset1 += (int)alignmentNamePos;
                offset2 += (int)alignmentNamePos;

                int initOffset1 = offset1;

                while (byteArray1[offset1] != '\0' && byteArray1[offset1] == byteArray2[offset2])
                {
                    ++offset1;
                    ++offset2;
                }

                if (byteArray1[offset1] < byteArray2[offset2])
                {
                    return -1;
                }

                if (byteArray1[offset1] > byteArray2[offset2])
                {
                    return 1;
                }

                return 0;
            }

            private UInt64 _offset;
            protected BigByteArray _serializedAlignments;
            private List<SerializedBamAlignment> _bamAlignmentList;
        }

        #region members

        protected List<SerializedAlignmentContainer> _bamAlignmentLists;
        private int _numTempFiles;
        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        #endregion

        public BamWriterInMem(Stream outStream,
                              string samHeader,
                              List<GenomeMetadata.SequenceMetadata> references,
                              float maxMemGB = 0.5f, // Same default as samtools sort
                              int numThreads = 1,
                              int compressionLevel = BamConstants.DefaultCompression)
            : base(outStream, samHeader, references, compressionLevel, numThreads)
        {
            Initialize(maxMemGB, numThreads, compressionLevel);
        }

        public BamWriterInMem(string filename,
                              string samHeader,
                              List<GenomeMetadata.SequenceMetadata> references,
                              float maxMemGB = 0.5f, // Same default as samtools sort
                              int numThreads = 1,
                              int compressionLevel = BamConstants.DefaultCompression)
            : base(filename, samHeader, references, compressionLevel, numThreads)
        {
            Initialize(maxMemGB, numThreads, compressionLevel);
        }

        private void Initialize(float maxMemGB,
                                int numThreads,
                                int compressionLevel)
        {
            _bamAlignmentLists = new List<SerializedAlignmentContainer>();
            UInt64 maxMemPerThread = (Convert.ToUInt64(maxMemGB * 1073741824)) / Convert.ToUInt64(numThreads);

            _numTempFiles = 0;
            _lock = new ReaderWriterLockSlim();

            for (int i = 0; i < numThreads; ++i)
            {
                _bamAlignmentLists.Add(new SerializedAlignmentContainer(maxMemPerThread));
            }
        }

        public List<IBamWriterHandle> GenerateHandles()
        {
            List<IBamWriterHandle> handles = new List<IBamWriterHandle>();
            for (int i = 0; i < NumThreads; ++i)
            {
                handles.Add(new BamWriterHandle(this, i));
            }
            return handles;
        }

        private void WriteAlignment(BamAlignment al, int bufferNumber)
        {
            if (!LockAndRecordAlignment(ref al, bufferNumber))
            {
                // Write failed because we're out of memory. Flush everything to disk.
                LockSortMemAndWrite(bufferNumber);

                // Try recording it again. It should work this time.
                if (!LockAndRecordAlignment(ref al, bufferNumber))
                {
                    throw new InvalidOperationException("ERROR: Check available memory. Memory full after flush in bam writing.");
                }
            }
        }

        private bool LockAndRecordAlignment(ref BamAlignment al, int bufferNumber)
        {
            _lock.EnterReadLock();
            bool ret = _bamAlignmentLists[bufferNumber].AddSerializedAlignment(ref al);
            _lock.ExitReadLock();

            return ret;
        }

        private void LockSortMemAndWrite(int bufferNumber)
        {
            _lock.EnterWriteLock();
            if (_bamAlignmentLists[bufferNumber].Count == 0)
            {
                // Multiple threads could try to call this method once
                // memory becomes full. If this thread's list is empty,
                // it is because it was flushed by another thread and
                // the write is already complete.
                _lock.ExitWriteLock();
                return;
            }

            ++_numTempFiles;
            SortMemAndWrite();
            _lock.ExitWriteLock();
        }

        public void Flush()
        {
            SortAndWrite();
        }

        public void SortAndWrite()
        {
            if (_numTempFiles > 0)
            {
                ++_numTempFiles;
            }

            SortMemAndWrite();

            if (_numTempFiles > 0)
            {
                MergeTempFiles();
            }
        }

        private string GetTempFileName(int index)
        {
            return Path.Combine(Path.GetDirectoryName(Filename), (index + 1).ToString() + Path.GetFileName(Filename));
        }

        // This method is very specialized. Each list should be the same size, so sorting
        // each on its own thread is very efficient. There is one additional pass
        // to merge the lists, but the writing occurs during this iteration, so we're
        // not waiting to write.
        private void SortMemAndWrite()
        {
            Logger.WriteToLog("Sorting BAM data...");

            // Sort each of the lists
            Parallel.ForEach(_bamAlignmentLists,
                             new ParallelOptions { MaxDegreeOfParallelism = NumThreads },
                             bamAlignmentList =>
                             {
                                 bamAlignmentList.Sort(new BamAlignmentComparer(bamAlignmentList));
                             }
            );

            Logger.WriteToLog("BAM sort complete.");

            List<BamRecordAccessor> bamListContainer = new List<BamRecordAccessor>();
            foreach (SerializedAlignmentContainer bamAlignmentList in _bamAlignmentLists)
            {
                bamListContainer.Add(new BamListContainer(bamAlignmentList));
            }

            BamWriter bamWriter = _numTempFiles > 0 ? GetNewTemporaryBamWriter() : this;
            WriteListsInOrder(bamListContainer, bamWriter);

            Logger.WriteToLog("Finished writing temporary file.");

            if (_numTempFiles > 0)
            {
                bamWriter.Dispose();

                foreach (var alignmentContainer in _bamAlignmentLists)
                {
                    alignmentContainer.Clear();
                }
            }
        }

        protected virtual BamWriter GetNewTemporaryBamWriter()
        {
            string filename = _numTempFiles > 0 ? GetTempFileName(_numTempFiles - 1) : Filename;
            return new BamWriter(filename, SamHeader, References, _compressionLevel, NumThreads);
        }

        #region helper classes

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected struct SerializedBamAlignment
        {
            public SerializedBamAlignment(UInt64 serializedOffset, int serializedSize, int refID, int position, uint alignmentFlag, int fragmentLength, uint mapQuality, int matePos, int mateRefID, bool isReverseStrand, string name = null)
            {
                SerializedOffset = serializedOffset;
                SerializedSize = serializedSize;

                RefID = refID;
                Position = position;

                AlignmentFlag = alignmentFlag;
                FragmentLength = fragmentLength;
                MapQuality = mapQuality;
                MatePosition = matePos;
                MateRefID = mateRefID;
                IsReverseStrand = isReverseStrand;
                Name = name;
            }

            public static bool operator ==(SerializedBamAlignment first, SerializedBamAlignment second)
            {
                if (first.RefID == second.RefID &&
                    first.Position == second.Position &&
                    first.AlignmentFlag == second.AlignmentFlag &&
                    first.FragmentLength == second.FragmentLength &&
                    first.MapQuality == second.MapQuality &&
                    first.MatePosition == second.MatePosition &&
                    first.MateRefID == second.MateRefID &&
                    first.IsReverseStrand == second.IsReverseStrand &&
                    first.Name == second.Name)
                {
                    return true;
                }
                return false;
            }

            public static bool operator !=(SerializedBamAlignment first, SerializedBamAlignment second)
            {
                return !(first == second);
            }

            public override bool Equals(Object obj)
            {
                // Check for null values and compare run-time types.
                if (obj == null || GetType() != obj.GetType())
                    return false;

                SerializedBamAlignment other = (SerializedBamAlignment)obj;
                return this == other;
            }

            public override int GetHashCode()
            {
                throw new NotImplementedException();
            }

            public UInt64 SerializedOffset;
            public int SerializedSize;

            public int RefID;
            public int Position;

            public uint AlignmentFlag;
            public int FragmentLength;
            public uint MapQuality;
            public int MatePosition;
            public int MateRefID;
            public bool IsReverseStrand;
            public string Name;
        }

        protected class BamAlignmentComparer : IComparer<SerializedBamAlignment>
        {
            public BamAlignmentComparer(SerializedAlignmentContainer alignmentContainer = null) : base()
            {
                _alignmentContainer = alignmentContainer;
            }

            public int Compare(SerializedBamAlignment a,
                               SerializedBamAlignment b)
            {
                return Compare(a, b, _alignmentContainer, _alignmentContainer);
            }

            // This comparison operator is copied exactly from samtools.
            public int Compare(SerializedBamAlignment a,
                               SerializedBamAlignment b,
                               SerializedAlignmentContainer alignmentContainerA,
                               SerializedAlignmentContainer alignmentContainerB)
            {
                int cmp = FileOrderCompare(a, b);

                if (cmp != 0)
                {
                    return cmp;
                }

                // They're the same.
                // Test of negative strand flag is not really necessary, because it is tested
                // with cmp if getFlags, but it is left here because that is the way it was done
                // in the past.
                if (a.IsReverseStrand == b.IsReverseStrand)
                {
                    cmp = -1 * a.MapQuality.CompareTo(b.MapQuality);
                    if (cmp != 0) return cmp;

                    if (alignmentContainerA != null && alignmentContainerB != null)
                    {
                        // When storing a large number of records, the names are stored in the byte
                        // array to avoid a lot of memory allocations.
                        cmp = SerializedAlignmentContainer.CompareNames(a, b, alignmentContainerA, alignmentContainerB);
                    }
                    else
                    {
                        cmp = string.CompareOrdinal(a.Name, b.Name);
                    }

                    if (cmp != 0) return cmp;
                    cmp = a.AlignmentFlag.CompareTo(b.AlignmentFlag);
                    if (cmp != 0) return cmp;
                    cmp = a.MateRefID.CompareTo(b.MateRefID);
                    if (cmp != 0) return cmp;
                    cmp = a.MatePosition.CompareTo(b.MatePosition);
                    if (cmp != 0) return cmp;
                    cmp = a.FragmentLength.CompareTo(b.FragmentLength);
                    return cmp;

                }
                else return (a.IsReverseStrand ? 1 : -1);
            }

            private int FileOrderCompare(SerializedBamAlignment a, SerializedBamAlignment b)
            {
                if (a.RefID == -1)
                {
                    return (b.RefID == -1 ? 0 : 1);
                }
                else if (b.RefID == -1)
                {
                    return -1;
                }
                int cmp = a.RefID - b.RefID;
                if (cmp != 0)
                {
                    return cmp;
                }
                return a.Position - b.Position;
            }

            private SerializedAlignmentContainer _alignmentContainer;
        }

        protected abstract class BamRecordAccessor
        {
            public abstract SerializedBamAlignment GetCurrentAlignment();

            public abstract void WriteCurrentAlignment(BamWriter writer);
            public abstract void MoveToNextRecord();
            public abstract bool IsEnd();
            public virtual void Close() { }

            public virtual SerializedAlignmentContainer GetSerializedAlignmentContainer() { return null; }
        }

        protected class BamRecordAccessorComparer : IComparer<BamRecordAccessor>
        {
            public BamRecordAccessorComparer() : base()
            {
                _bamAlignmentComparer = new BamAlignmentComparer(null);
            }

            public int Compare(BamRecordAccessor a,
                               BamRecordAccessor b)
            {
                return _bamAlignmentComparer.Compare(
                    a.GetCurrentAlignment(),
                    b.GetCurrentAlignment(),
                    a.GetSerializedAlignmentContainer(),
                    b.GetSerializedAlignmentContainer());
            }

            private BamAlignmentComparer _bamAlignmentComparer;
        }

        private class BamListContainer : BamRecordAccessor
        {
            SerializedAlignmentContainer _bamList;
            int _index;
            public BamListContainer(SerializedAlignmentContainer bamList)
            {
                _index = 0;
                _bamList = bamList;
            }

            public override SerializedAlignmentContainer GetSerializedAlignmentContainer() { return _bamList; }

            public override SerializedBamAlignment GetCurrentAlignment()
            {
                return _bamList.GetBamAlignment(_index);
            }

            public override void WriteCurrentAlignment(BamWriter writer)
            {
                _bamList.write(writer, _index);
            }

            public override void MoveToNextRecord()
            {
                ++_index;
            }

            public override bool IsEnd()
            {
                return _index >= _bamList.Count;
            }
        }

        protected class BamReaderContainer : BamRecordAccessor
        {
            BamReader _bamReader;
            string _filename;
            SerializedBamAlignment _currentSerializedAlignment;
            BamAlignment _currentBamAlignment;
            bool _isEnd;

            public BamReaderContainer(Stream inStream, string filename)
            {
                _bamReader = new BamReader();
                _bamReader.Open(inStream);
                _filename = filename;
                _currentBamAlignment = new BamAlignment();
                _currentSerializedAlignment = new SerializedBamAlignment();
                MoveToNextRecord();
            }

            public override SerializedBamAlignment GetCurrentAlignment()
            {
                return _currentSerializedAlignment;
            }

            public override void WriteCurrentAlignment(BamWriter writer)
            {
                byte[] serializedAlignment = BamWriter.SerializeAlignment(ref _currentBamAlignment);
                writer.Write(serializedAlignment, (uint)serializedAlignment.Length);
            }

            public override void MoveToNextRecord()
            {
                _isEnd = !_bamReader.GetNextAlignment(ref _currentBamAlignment, false);

                if (_isEnd)
                {
                    return;
                }

                // No memory allocation
                _currentSerializedAlignment.RefID = _currentBamAlignment.RefID;
                _currentSerializedAlignment.Position = _currentBamAlignment.Position;
                _currentSerializedAlignment.AlignmentFlag = _currentBamAlignment.AlignmentFlag;
                _currentSerializedAlignment.FragmentLength = _currentBamAlignment.FragmentLength;
                _currentSerializedAlignment.MapQuality = _currentBamAlignment.MapQuality;
                _currentSerializedAlignment.MatePosition = _currentBamAlignment.MatePosition;
                _currentSerializedAlignment.MateRefID = _currentBamAlignment.MateRefID;
                _currentSerializedAlignment.IsReverseStrand = _currentBamAlignment.IsReverseStrand();
                _currentSerializedAlignment.Name = _currentBamAlignment.Name;
            }

            public override bool IsEnd()
            {
                return _isEnd;
            }

            public override void Close()
            {
                base.Close();
                _bamReader.Dispose();

                // Delete the temporary file.
                if (!string.IsNullOrEmpty(_filename))
                {
                    File.Delete(_filename);
                }
            }
        }

        #endregion

        void WriteListsInOrder(List<BamRecordAccessor> bamContainers, BamWriter bamWriter)
        {
            List<int> bamAlignmentListIndexes = new List<int>();
            for (int i = 0; i < bamContainers.Count; ++i)
            {
                if (!bamContainers[i].IsEnd())
                {
                    bamAlignmentListIndexes.Add(i);
                }
            }

            if (bamAlignmentListIndexes.Count == 0)
            {
                return;
            }

            BamRecordAccessorComparer recordAccessorComparer = new BamRecordAccessorComparer();
            SortedList<BamRecordAccessor, BamRecordAccessor> sortedList = new SortedList<BamRecordAccessor, BamRecordAccessor>(bamContainers.Count, recordAccessorComparer);

            for (int i = 0; i < bamContainers.Count; ++i)
            {
                var bamContainer = bamContainers[i];
                sortedList.Add(bamContainer, bamContainer);
            }

            while (sortedList.Count > 1)
            {
                var bamContainer = sortedList.Values[0];
                sortedList.RemoveAt(0);

                bamContainer.WriteCurrentAlignment(bamWriter);
                bamContainer.MoveToNextRecord();

                if (!bamContainer.IsEnd())
                {
                    sortedList.Add(bamContainer, bamContainer);
                }
            }

            // There is only 1 list left
            var finalBamContainer = sortedList.Values[0];
            while (!finalBamContainer.IsEnd())
            {
                finalBamContainer.WriteCurrentAlignment(bamWriter);
                finalBamContainer.MoveToNextRecord();
            }
        }

        protected virtual BamReaderContainer GetNewBamReaderContainer(int index)
        {
            string filename = GetTempFileName(index);
            return new BamReaderContainer(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), filename);
        }

        private void MergeTempFiles()
        {
            List<BamRecordAccessor> bamReaders = new List<BamRecordAccessor>();

            for (int i = 0; i < _numTempFiles; ++i)
            {
                // Each of these files has already been sorted individually.
                bamReaders.Add(GetNewBamReaderContainer(i));
            }

            WriteListsInOrder(bamReaders, this);

            foreach (BamRecordAccessor bamReader in bamReaders)
            {
                bamReader.Close();
            }
        }
    }
}