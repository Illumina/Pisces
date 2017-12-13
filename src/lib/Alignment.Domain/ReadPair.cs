using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Alignment.Domain.Sequencing;

namespace Alignment.Domain
{
    public enum ReadNumber
    {
        NA,
        Read1,
        Read2
    }
    public class ReadPair
    {
        public string Name { get; set; }
        public BamAlignment Read1;
        public BamAlignment Read2;
        public List<BamAlignment> Read1SupplementaryAlignments;
        public List<BamAlignment> Read2SupplementaryAlignments;
        public List<BamAlignment> Read1SecondaryAlignments;
        public List<BamAlignment> Read2SecondaryAlignments;
        public bool IsImproper;

        public List<BamAlignment> Read1Alignments
        {
            get
            {
                var alignments = new List<BamAlignment>() { Read1 };
                if (Read1SupplementaryAlignments != null)
                {
                    alignments.AddRange(Read1SupplementaryAlignments);
                }
                if (Read1SecondaryAlignments != null)
                {
                    alignments.AddRange(Read1SecondaryAlignments);
                }
                return alignments;
            }
        }
        public List<BamAlignment> Read2Alignments
        {
            get
            {
                var alignments = new List<BamAlignment>() { Read2 };
                if (Read2SupplementaryAlignments != null)
                {
                    alignments.AddRange(Read2SupplementaryAlignments);
                }
                if (Read2SecondaryAlignments != null)
                {
                    alignments.AddRange(Read2SecondaryAlignments);
                }
                return alignments;
            }
        }

        public ReadPair(BamAlignment alignment, string name = null, ReadNumber readNumber = ReadNumber.NA)
        {
            Name = name ?? alignment.Name;

            AddAlignment(alignment, readNumber);
        }

        public void AddAlignment(BamAlignment alignment, ReadNumber readNumber = ReadNumber.NA)
        {
            var alignmentCopy = new BamAlignment(alignment);
            if (alignment.IsPrimaryAlignment() && !alignment.IsSupplementaryAlignment())
            {
                if (readNumber == ReadNumber.NA)
                {
                    if (Read1 != null && Read2 != null) throw new InvalidDataException("Already have both primary alignments.");
                    if (Read1 == null) Read1 = alignmentCopy;
                    else Read2 = alignmentCopy;
                }
                else if (readNumber == ReadNumber.Read1)
                {
                    if (Read1 != null) throw new InvalidDataException("Already have a read 1 primary alignment.");
                    Read1 = alignmentCopy;
                }
                else if (readNumber == ReadNumber.Read2)
                {
                    if (Read2 != null) throw new InvalidDataException("Already have a read 2 primary alignment.");
                    Read2 = alignmentCopy;
                }
            }
            else if (alignment.IsSupplementaryAlignment())
            {
                switch (readNumber)
                {
                    case ReadNumber.Read1:
                        if (Read1SupplementaryAlignments == null)
                        {
                            Read1SupplementaryAlignments = new List<BamAlignment>();
                        }
                        Read1SupplementaryAlignments.Add(alignmentCopy);
                        break;
                    case ReadNumber.Read2:
                        if (Read2SupplementaryAlignments == null)
                        {
                            Read2SupplementaryAlignments = new List<BamAlignment>();
                        }
                        Read2SupplementaryAlignments.Add(alignmentCopy);
                        break;
                    case ReadNumber.NA:
                        if (Read1SupplementaryAlignments == null)
                        {
                            Read1SupplementaryAlignments = new List<BamAlignment>();
                        }
                        Read1SupplementaryAlignments.Add(alignmentCopy);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(readNumber), readNumber, null);
                }
            }
            else
            {
                switch (readNumber)
                {
                    case ReadNumber.Read1:
                        if (Read1SecondaryAlignments == null)
                        {
                            Read1SecondaryAlignments = new List<BamAlignment>();
                        }
                        Read1SecondaryAlignments.Add(alignmentCopy);
                        break;
                    case ReadNumber.Read2:
                        if (Read2SecondaryAlignments == null)
                        {
                            Read2SecondaryAlignments = new List<BamAlignment>();
                        }
                        Read2SecondaryAlignments.Add(alignmentCopy);
                        break;
                    case ReadNumber.NA:
                        if (Read1SecondaryAlignments == null)
                        {
                            Read1SecondaryAlignments = new List<BamAlignment>();
                        }
                        Read1SecondaryAlignments.Add(alignmentCopy);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(readNumber), readNumber, null);
                }
            }

            // Set as improper once we add any alignment that is flagged as improper
            if (!alignment.IsProperPair()) IsImproper = true;
        }

        public bool IsComplete(bool requireSupplementaryForCompletion = true)
        {
            var completePair = Read1 != null && Read2 != null;
            if (!completePair) return false;
            
            if (requireSupplementaryForCompletion)
            {
                var r1SupplementariesCollected = !Read1.HasSupplementaryAlignment() || Read1.GetSupplementaryAlignments().Count <= (Read1SupplementaryAlignments?.Count ?? 0);
                var r2SupplementariesCollected = !Read2.HasSupplementaryAlignment() || Read2.GetSupplementaryAlignments().Count <= (Read2SupplementaryAlignments?.Count ?? 0);

                return r1SupplementariesCollected && r2SupplementariesCollected;
            }

            return true;
        }

        /// <summary>
        /// Get all primary and supplementary alignments for read 1 and read 2. Does not include secondary alignments.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BamAlignment> GetAlignments()
        {
            return
                new List<BamAlignment>() { Read1, Read2 }
                .Concat(Read1SupplementaryAlignments ?? new List<BamAlignment>())
                .Concat(Read2SupplementaryAlignments ?? new List<BamAlignment>())
                .Where(a => a != null);
        }
    }
}
