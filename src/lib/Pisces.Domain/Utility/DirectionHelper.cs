using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;

namespace Pisces.Domain.Utility
{
    public static class DirectionHelper
    {
        public static List<DirectionOp> CompressDirections(List<DirectionOp> origDirections)
        {
            DirectionOp? lastOp = null;
            var newDirections = new List<DirectionOp>();
            foreach (var directionOp in origDirections)
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
            return newDirections;
        }

        public static List<string> ListDirectionKeys()
        {
            return new List<string>() { "F", "R", "S" };
        }

        public static string GetDirectionKey(DirectionType direction)
        {
            switch (direction)
            {
                case DirectionType.Forward:
                    return "F";
                case DirectionType.Reverse:
                    return "R";
                case DirectionType.Stitched:
                    return "S";
                default:
                    throw new ArgumentException($"Unrecognized direction type: {direction}");
            }
        }

        public static DirectionType GetDirection(string directionKey)
        {
            switch (directionKey)
            {
                case "F":
                    return DirectionType.Forward;
                case "R":
                    return DirectionType.Reverse;
                case "S":
                    return DirectionType.Stitched;
                default:
                    throw new ArgumentException(string.Format("Unrecognized direction key '{0}'", directionKey));
            }
        }
    }
}
