using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace Pisces.Domain.Logic
{
    public class AlignmentMateFinder : IAlignmentMateFinder
    {
        private readonly Dictionary<string, Read> _readsLookupByName = new Dictionary<string, Read>();
        private readonly SortedList<int, List<string>> _readsLookupByPosition = new SortedList<int, List<string>>();
        private readonly SortedList<int, List<string>> _readsLookupByMatePosition = new SortedList<int, List<string>>();
        private readonly int _maxWindow;

        public int ReadsUnpairable { get; set; }
        public event Action<Read> ReadPurged;
        public IEnumerable<Read> GetUnpairedReads()
        {
            return _readsLookupByName.Values;
        }

        public AlignmentMateFinder(int maxWindow = 1000)
        {
            _maxWindow = maxWindow;
        }

        public Read GetMate(Read read)
        {
            if (read.MatePosition < 0)
                throw new ArgumentException(string.Format("Invalid mate position {0} for read '{1}'.", read.MatePosition,
                    read.Name));

            if (string.IsNullOrEmpty(read.Name))
                throw new ArgumentException(string.Format("Read at position {0} has empty name.", read.Position));

            // purge any reads that are past the max window to look
            Purge(read.Position);

            Read readMate;

            if (_readsLookupByName.TryGetValue(read.Name, out readMate))
            {
                _readsLookupByName.Remove(read.Name); // remove

                List<string> readsAtPosition;
                if (_readsLookupByPosition.TryGetValue(readMate.Position, out readsAtPosition))
                {
                    readsAtPosition.Remove(read.Name);

                    if (!readsAtPosition.Any())
                        _readsLookupByPosition.Remove(readMate.Position);
                }
                if (_readsLookupByMatePosition.TryGetValue(readMate.MatePosition, out readsAtPosition))
                {
                    readsAtPosition.Remove(read.Name);

                    if (!readsAtPosition.Any())
                        _readsLookupByMatePosition.Remove(readMate.MatePosition);
                }

                // TODO: This is a scenario that should never happen on a properly-formed BAM file. Why is this not just thrown?
                // therefore, we're just going to drop these reads on the floor and report them as unpairable
                if (readMate.Position != read.MatePosition || readMate.MatePosition != read.Position)
                {
                    ReadsUnpairable += 2;
                    return null;
                    // TODO: Log a message when logging is integrated
                    // throw new Exception(string.Format("Read pair '{0}' do not have matching mate positions", read.Name));
                }
            }
            else
            {
                if (read.MatePosition < read.Position)
                {
                    NotifyReadPurged(read);
                    return null;
                }

                _readsLookupByName.Add(read.Name, read.DeepCopy());
                    // important to copy to new read since input read object gets reused

                List<string> reads;

                if (!_readsLookupByPosition.TryGetValue(read.Position, out reads))
                    _readsLookupByPosition[read.Position] = new List<string>() {read.Name};
                else
                    reads.Add(read.Name);
                if (!_readsLookupByMatePosition.TryGetValue(read.MatePosition, out reads))
                    _readsLookupByMatePosition[read.MatePosition] = new List<string>() { read.Name };
                else
                    reads.Add(read.Name);
            }

            return readMate;
        }

        private void NotifyReadPurged(Read read)
        {
            var handler = ReadPurged;
            if (handler != null)
                handler(read); 
            ReadsUnpairable++;
        }

        private void Purge(int currentPosition)
        {
            var positionsToPurge = new List<int>();

            var duplexReads = 0;
            var allReads = 0;
            // purge any reads that are past the max window to look
            foreach (var position in _readsLookupByPosition.Keys)
            {
                if (currentPosition > position + _maxWindow)
                {
                    var readsToPurge = _readsLookupByPosition[position];

                    foreach (var readToPurge in readsToPurge)
                    {
                        var purgedRead = _readsLookupByName[readToPurge];
                        allReads++;
                        if (purgedRead.IsDuplex)
                            duplexReads ++;
                        _readsLookupByName.Remove(readToPurge);
                        NotifyReadPurged(purgedRead);
                        List<string> reads = _readsLookupByMatePosition[purgedRead.MatePosition];
                        if (reads.Count == 1)
                            _readsLookupByMatePosition.Remove(purgedRead.MatePosition);
                        else
                            reads.Remove(purgedRead.Name);
                    }

                    positionsToPurge.Add(position);
                }
                else
                {
                    break;  // lookup is sorted, break once we find a good position
                }
            }

            foreach (var position in positionsToPurge)
            {
                _readsLookupByPosition.Remove(position);
            }
        }

        public int? LastClearedPosition 
        {
            get
            {
                int? min = null;
                if (_readsLookupByPosition.Any())
                {
                    min = _readsLookupByPosition.First().Key - 1;
                }
                return min;
            }
        }
        public int? NextMatePosition
        {
            get
            {
                if (_readsLookupByMatePosition.Any())
                    return _readsLookupByMatePosition.Keys.First();
                return null;
            }
        }

    }
}
