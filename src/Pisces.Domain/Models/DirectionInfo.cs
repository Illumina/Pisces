using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    public class DirectionInfo
    {
        private const char _delimiter = ':';
        public List<DirectionOp> Directions = new List<DirectionOp>();

        public DirectionInfo()
        {

        }

        public DirectionInfo(string directionString)
        {
            try
            {
                var directionTokens = directionString.Split(_delimiter);
                foreach (var directionToken in directionTokens)
                {
                    var length = Int32.Parse(directionToken.Substring(0, directionToken.Length - 1));
                    var direction = DirectionHelper.GetDirection(directionToken.Substring(directionToken.Length - 1));

                    Directions.Add(new DirectionOp {Length = length, Direction = direction});
                }
                
                //Validate(); nope. wild west, now.
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Unable to parse direction string '{0}'", directionString), ex);
            }
        }

        public DirectionType[] ToDirectionMap()
        {
            var map = new List<DirectionType>();
            foreach (var directionOp in Directions)
            {
                for (var i = 0; i < directionOp.Length; i++)
                {
                    map.Add(directionOp.Direction);
                }    
            }

            return map.ToArray();
        }

        private void Validate()
        {
            switch (Directions.Count)
            {
                case 0:
                    throw new Exception("No directions found");
                case 2:
                    if (Directions.All(d => d.Direction != DirectionType.Stitched))
                        throw new Exception("Multi directional but with no stitching");
                    break;
                case 3:
                    if (Directions[1].Direction != DirectionType.Stitched)
                        throw new Exception("Stitched direction must be in the center");
                    break;
                default:
                    if (Directions.Count > 3)
                        throw new Exception("More than 3 directions found");
                    break;
            }
        }

        public override string ToString()
        {
            return string.Join(_delimiter.ToString(), Directions.Select(d => d.Length + DirectionHelper.GetDirectionKey(d.Direction)));
        }

    }

    public struct DirectionOp
    {
        public int Length;
        public DirectionType Direction;
    }
}
