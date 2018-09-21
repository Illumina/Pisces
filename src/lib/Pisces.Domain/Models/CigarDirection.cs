using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    public class CigarDirection
    {
        public List<DirectionOp> Directions { get; private set; }

        public CigarDirection()
        {
            Directions = new List<DirectionOp>();
        }

        public CigarDirection(string directionString)
        {
            Directions = new List<DirectionOp>();

            // Similar functionality to cigar string parsing
            int head = 0;
            for (int i = 0; i < directionString.Length; ++i)
            {
                if (Char.IsDigit(directionString, i)) continue;
                // TODO check for unexpected chars

                var length = uint.Parse(directionString.Substring(head, i - head));
                var directionChar = directionString[i];
                var direction = DirectionHelper.GetDirection(directionChar.ToString());
                var op = new DirectionOp() {Direction = direction, Length = (int) length};

                Directions.Add(op);
                head = i + 1;
            }
            if (head != directionString.Length)
            {
                throw new InvalidDataException(string.Format("Unexpected format in direction string: {0}", directionString));
            }
        }

        public CigarDirection(List<DirectionOp> directionOps)
        {
            Directions = directionOps;
        }

        /// <summary>
        ///     If duplicated adjacent tags are present, reduce them to one copy
        /// </summary>
        /// <returns>true if cigar was altered by compression</returns>
        public bool Compress()
        {
            var newDirections = DirectionHelper.CompressDirections(Directions);

            Directions = newDirections;
            return true;
        }

        public int GetCigarSpan()
        {
            int length = 0;
            foreach (DirectionOp op in Directions)
            {
                length += op.Length;
            }
            return length;
        }

        public List<DirectionType> Expand()
        {
            var expandedDirections = new List<DirectionType>();
            foreach (var direction in Directions)
            {
                for (var i = 0; i < direction.Length; i++)
                {
                    expandedDirections.Add(direction.Direction);
                }
            }

            return expandedDirections;
        }

        public override string ToString()
        {
            return string.Join("", Directions.Select(d => d.Length + DirectionHelper.GetDirectionKey(d.Direction)));
        }
    }

    public class CigarDirectionExpander
    {
        private CigarDirection _cigarDirection;
        private int _cigarIndex;
        private int _opIndex;

        public CigarDirectionExpander(CigarDirection cigarDirection)
        {
            _cigarDirection = cigarDirection;
            _cigarIndex = 0;
            _opIndex = 0;
        }

        public bool MoveNext()
        {
            if (_cigarIndex < _cigarDirection.Directions.Count)
            {
                ++_opIndex;
                if (_opIndex >= _cigarDirection.Directions[_cigarIndex].Length)
                {
                    _opIndex = 0;
                    ++_cigarIndex;
                }
            }
            return IsNotEnd();
        }

        public bool IsNotEnd()
        {
            return _cigarIndex < _cigarDirection.Directions.Count;
        }

        public void Reset()
        {
            _cigarIndex = 0;
            _opIndex = 0;
        }

        public DirectionType Current
        {
            get { return _cigarDirection.Directions[_cigarIndex].Direction; }
        }
    }
}