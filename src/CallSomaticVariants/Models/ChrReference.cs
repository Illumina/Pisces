using System;
using System.Collections.Generic;
using CallSomaticVariants.Logic.RegionState;

namespace CallSomaticVariants.Models
{
    public class ChrReference
    {
        public string Sequence { get; set; }
        public string Name { get; set; }
        public string FastaPath { get; set; }

        public string GetBase(int position)
        {
            if (position > Sequence.Length || position < 1)
                throw new ArgumentException(string.Format("Invalid position {0} for chr {2} with sequence length {1}.", position, Sequence.Length, Name));

            return Sequence.Substring(position - 1, 1);
        }
    }
}
