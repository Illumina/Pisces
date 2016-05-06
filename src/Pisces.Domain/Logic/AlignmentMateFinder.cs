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
        private readonly int _maxWindow;

        public int ReadsSkipped { get; set; }

        public AlignmentMateFinder(int maxWindow = 1000)
        {
            _maxWindow = maxWindow;
        }

        public Read GetMate(Read read)
        {
            if (read.MatePosition < 0)
                throw new ArgumentException(string.Format("Invalid mate position {0} for read '{1}'.", read.MatePosition, read.Name));
            
            if (string.IsNullOrEmpty(read.Name))
                throw new ArgumentException(string.Format("Read at position {0} has empty name.", read.Position));

            if (read.MatePosition == 1)
            {
                ReadsSkipped ++;
                return null; // jg todo temp fix for umiak issue
            }

            // purge any reads that are past the max window to look
            Purge(read.Position);    

            try
            {
                Read readMate;

                if (_readsLookupByName.TryGetValue(read.Name, out readMate))
                {
                    _readsLookupByName.Remove(read.Name);  // remove

                    List<string> readsAtPosition;
                    if (_readsLookupByPosition.TryGetValue(readMate.Position, out readsAtPosition))
                    {
                        readsAtPosition.Remove(read.Name);

                        if (!readsAtPosition.Any())
                            _readsLookupByPosition.Remove(readMate.Position);
                    }

                    if (readMate.Position != read.MatePosition || readMate.MatePosition != read.Position)
                    {
                        ReadsSkipped += 2;
                        throw new Exception(string.Format("Read pair '{0}' do not have matching mate positions", read.Name));
                    }
                }
                else
                {
                    if (read.MatePosition < read.Position)
                    {
                        ReadsSkipped ++;
                        throw new Exception(string.Format("Mate at position '{0}' for read '{1}' at position '{2}' was never encountered.", read.MatePosition, read.Name, read.Position));
                    }

                    _readsLookupByName.Add(read.Name, read.DeepCopy());   // important to copy to new read since input read object gets reused

                    if (!_readsLookupByPosition.ContainsKey(read.Position))
                        _readsLookupByPosition.Add(read.Position, new List<string>() { read.Name });
                    else
                        _readsLookupByPosition[read.Position].Add(read.Name);
                }

                return readMate;
            }
            catch (Exception ex)
            {
                return null; // continue, but skip read
            }
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
                        ReadsSkipped++;
                        var purgedRead = _readsLookupByName[readToPurge];
                        allReads++;
                        if (purgedRead.IsDuplex)
                            duplexReads ++;
                        _readsLookupByName.Remove(readToPurge);
                    }

                    Console.WriteLine("Max window error: skipping " + duplexReads + " duplex reads out of " + allReads);
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
    }
}
