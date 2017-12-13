using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models
{
    public class AlignmentSet
    {
        public Read PartnerRead1 { get; private set; }
        public Read PartnerRead2 { get; private set; }

        public List<Read> ReadsForProcessing { get; set; }
        public bool IsStitched { get; set; }
        /// <summary>
        /// Whether the AlignmentSet's PartnerRead1 (which represents the earlier-occurring read, when softclip adjusted) is the "actual" Read2.
        /// For an AlignmentSet with non-null PartnerRead1 and PartnerRead2, if PartnerRead2 is in the forward direction, we set this to true.
        /// </summary>
        public bool IsOutie { get; private set; }

        //Maybe add something like this or other validation logic to make sure there aren't somehow more than 2 buddies that try to get in here
        public bool IsFullPair
        {
            get { return PartnerRead1 != null && PartnerRead2 != null; }
        }

        public AlignmentSet(Read read1, Read read2, bool readyToProcess = false)
        {
            if (read1 == null)
                throw new ArgumentException("Read 1 cannot be null.");

            if (read2 == null)
                PartnerRead1 = read1;
            else
            {
                if (read1.ClipAdjustedPosition > read2.ClipAdjustedPosition)
                {
                    var tmpRead = read2;
                    read2 = read1;
                    read1 = tmpRead;
                }
                PartnerRead1 = read1;
                PartnerRead2 = read2;

                // Assumption is that exactly one read is Forward and one is Reverse
                if (PartnerRead2.SequencedBaseDirectionMap.First() == DirectionType.Forward)
                {
                    IsOutie = true;
                }
            }


            ReadsForProcessing = new List<Read>();

            if (readyToProcess)
            {
                ReadsForProcessing.Add(PartnerRead1);
                if (PartnerRead2 != null)
                    ReadsForProcessing.Add(PartnerRead2);
            }
        }
    }
}