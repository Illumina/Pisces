using System;
using System.Collections.Generic;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;

namespace CallSomaticVariants.Logic.Alignment
{
    public class AlignmentMateFinder : IAlignmentMateFinder
    {
        private readonly Dictionary<int, Dictionary<string, Read>> _readsLookingForMateLookup = new Dictionary<int, Dictionary<string, Read>>();
        private readonly int _maxWindow;

        public AlignmentMateFinder(int maxWindow = 1000)
        {
            _maxWindow = maxWindow;
        }

        public Read GetMate(Read read)
        {
            Read readMate = null;

            if (read.MatePosition < 0)
                throw new ArgumentException(string.Format("Invalid mate position {0} for read '{1}'.", read.MatePosition, read.Name));
            
            if (string.IsNullOrEmpty(read.Name))
                throw new ArgumentException(string.Format("Read at position {0} has empty name.", read.Position));

           /* var unfoundKeys = _readsLookingForMateLookup.Keys.Where(k => read.Position - k > _maxWindow).ToArray();
            if (unfoundKeys.Any())
            {
                var unfoundRead = _readsLookingForMateLookup[unfoundKeys.First()].Values.First();
                throw new Exception(string.Format("Exceeded max window {0} for finding mate for read '{1}' at position {2}.",
                    _maxWindow, unfoundRead.Name, unfoundRead.Position));
            }*/

            Dictionary<string, Read> matePositionLookup;

            if (_readsLookingForMateLookup.TryGetValue(read.MatePosition, out matePositionLookup)
                && (matePositionLookup.TryGetValue(read.Name, out readMate)))
            {
                //this guy is no longer looking for a mate
               matePositionLookup.Remove(read.Name);

                if (matePositionLookup.Count == 0)
                {
                    _readsLookingForMateLookup.Remove(read.MatePosition);
                }
            }
            else
            {
                Dictionary<string, Read> positionLookup;
                if (!_readsLookingForMateLookup.TryGetValue(read.Position, out positionLookup))
                {
                    positionLookup = new Dictionary<string, Read>();
                    _readsLookingForMateLookup.Add(read.Position, positionLookup);
                }

                positionLookup.Add(read.Name, read.DeepCopy());  // important to copy to new read since input read object gets reused
            }

            return readMate;
        }

        public int? LastClearedPosition 
        {
            get
            {
                int? min = null;
                if (_readsLookingForMateLookup.Keys.Any())
                {
                    min = _readsLookingForMateLookup.Keys.Min() - 1;
                }
                return min;
            }
        }
    }
}
