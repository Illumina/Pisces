using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    public class CigarDirection
    {
        public List<DirectionOp> Directions = new List<DirectionOp>();

        public CigarDirection()
        {
        }

        public CigarDirection(string directionString)
        {
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
                throw new Exception(string.Format("Unexpected format in direction string: {0}", directionString));
            }
        }

        /// <summary>
        ///     If duplicated adjacent tags are present, reduce them to one copy
        /// </summary>
        /// <returns>true if cigar was altered by compression</returns>
        public bool Compress()
        {
            DirectionOp? lastOp = null;
            var newDirections = new List<DirectionOp>();
            foreach (var directionOp in Directions)
            {
                if (lastOp == null)
                {
                    // First time through
                    lastOp = directionOp;
                }
                else if (directionOp.Direction == ((DirectionOp)lastOp).Direction)
                {
                    // Add the two ops together
                    lastOp = new DirectionOp
                    {
                        Direction = directionOp.Direction,
                        Length = directionOp.Length + ((DirectionOp)lastOp).Length
                    };
                }
                else
                {
                    newDirections.Add((DirectionOp)lastOp);
                    lastOp = directionOp;
                }
            }
            newDirections.Add((DirectionOp)lastOp);

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
}