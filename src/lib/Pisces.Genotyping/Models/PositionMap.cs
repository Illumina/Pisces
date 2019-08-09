using System.Linq;

namespace Pisces.Domain.Models
{
    public class PositionMap
    {
        private readonly int[] _positionMap;

        public PositionMap(int length)
        {
            _positionMap = new int[length];
            for (int i = 0; i < length; i++)
            {
                _positionMap[i] = int.MinValue;
            }
        }

        public PositionMap(int[] rawPositionMap)
        {
            _positionMap = rawPositionMap;
        }

        public void SetIndexUnmapped(int index)
        {
            _positionMap[index] = -1;
        }

        public void SetIndexSoftclip(int index)
        {
            _positionMap[index] = -2;
        }

        public void UpdatePositionAtIndex(int index, int value, bool canBeNegative = false)
        {
            // This is one-based so value of 0 doesn't make sense.
            // In some cases a negative value makes sense (unmapped bases), but this should be explicit and intentional (TODO maybe just do those in explicit set unmapped methods? for now, no, because there are some cases where we're blindly copying over values.)
            if (value == 0 || !canBeNegative && value < 0)
            {
                throw new System.ArgumentException($"Updating position made it too low ({value} at {index}).");
            }

            _positionMap[index] = value;
        }

        public int GetPositionAtIndex(int index)
        {
            return _positionMap[index];
        }

        public int Length
        {
            get { return _positionMap.Length; }
        } 

        public int[] Map
        {
            get { return _positionMap; }
        }

        public bool HasAnyMappableBases()
        {
            // TODO Position map is one-based, so should be >, not >= 0.
            return _positionMap.Any(p => p >= 0);
        }

        public int FirstMappableBase()
        {
            return _positionMap.First(p => p >= 0);
        }

        public int MaxPosition()
        {
            // TODO may actually be able to take this opportunity to optimize since we know positionmap will always be increasing # (except unmappeds)
            return _positionMap.Max();
        }
    }
}