using System;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;
using SequencingFiles;

namespace CallSomaticVariants.Models
{
    public class Read
    {
        public CigarAlignment CigarData { get { return BamAlignment.CigarData; } }
        public string Sequence { get { return BamAlignment.Bases; } }
        public int ReadLength { get { return BamAlignment.Bases.Length; } }
        public byte[] Qualities { get { return BamAlignment.Qualities; }}
        public int Position { get { return BamAlignment.Position + 1; } }
        public int MatePosition { get { return BamAlignment.MatePosition + 1; }}
        public string Name { get { return BamAlignment.Name; } } // cluster name
        public bool IsMapped { get { return BamAlignment.IsMapped(); } }
        public bool IsPrimaryAlignment { get { return BamAlignment.IsPrimaryAlignment(); } }
        public bool IsPcrDuplicate { get { return BamAlignment.IsDuplicate(); } }
        public bool IsProperPair { get { return BamAlignment.IsProperPair(); } }
        public uint MapQuality { get { return BamAlignment.MapQuality; } }

        public string Chromosome { get; private set; }
        public CigarAlignment StitchedCigar { get; set; }
        public DirectionType[] DirectionMap { get; set; }
        public int[] PositionMap { get; set; }  // for every base in the sequence, map back to reference position or -1 if does not map

        private BamAlignment _bamAlignment;

        public BamAlignment BamAlignment
        {
            get { return _bamAlignment; }
            private set
            {
                if (value == null)
                    throw new ArgumentException("Alignment cannot be empty.");

                if (value.Bases == null)
                    throw new ArgumentException("Alignment sequence cannot be empty.");

                value.Bases = value.Bases.ToUpper();  // enforce read sequence is always upper case

                _bamAlignment = value;
            }
        }

        public Read(string chromosome, BamAlignment bamAlignment, bool stitchingEnabled = false)
        {
            BamAlignment = bamAlignment;
            Chromosome = chromosome;

            if (string.IsNullOrEmpty(chromosome))
                throw new ArgumentException("Chromosome cannot be empty.");

            UpdateFromBam(stitchingEnabled);
        }

        public Read()
        {
            BamAlignment = new BamAlignment() { Bases = string.Empty };
        }

        private void UpdateFromBam(bool stitchingEnabled = false)
        {
            if (DirectionMap == null || DirectionMap.Length != ReadLength)
                DirectionMap = new DirectionType[ReadLength];

            var reverse = BamAlignment.IsReverseStrand();
            for (var i = 0; i < DirectionMap.Length; i++)
            {
                DirectionMap[i] = reverse ? DirectionType.Reverse : DirectionType.Forward;
            }

            if (PositionMap == null || PositionMap.Length != ReadLength)
                PositionMap = new int[ReadLength];

            for (var i = 0; i < PositionMap.Length; i++)
            {
                PositionMap[i] = -1;
            }

            UpdateMapFromCigar();

            if (stitchingEnabled && BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                var xcTag = BamAlignment.GetStringTag(BamAlignment.TagData, "XC");
                if (xcTag != null)
                    StitchedCigar = new CigarAlignment(xcTag);
            }
        }

        public Read DeepCopy()
        {
            var read = new Read(Chromosome, new BamAlignment(BamAlignment))
            {
                DirectionMap = DirectionMap == null ? null : new DirectionType[DirectionMap.Length],
                StitchedCigar = StitchedCigar,
                PositionMap = PositionMap == null ? null : new int[PositionMap.Length],
            };

            if (DirectionMap != null)
                Array.Copy(DirectionMap, read.DirectionMap, DirectionMap.Length);

            if (PositionMap != null)
                Array.Copy(PositionMap, read.PositionMap, PositionMap.Length);

            return read;
        }

        public void Reset(string chromosome, BamAlignment bamAlignment, bool readStitchingEnabled)
        {
            Chromosome = chromosome;
            BamAlignment = bamAlignment;
            UpdateFromBam(readStitchingEnabled);
        }

        private void UpdateMapFromCigar()
        {
            if (CigarData == null || CigarData.Count == 0)
                return;

            if (CigarData.GetReadSpan() != PositionMap.Length)
                throw new Exception(string.Format("Invalid cigar '{0}': does not match length {1} of read", CigarData, ReadLength));

            if (CigarData.Count == 1 && (CigarData[0].Type =='I'||CigarData[0].Type == 'D'))
                throw new Exception(string.Format("Invalid cigar '{0}': indel must have anchor", CigarData));

            int readIndex = 0;
            int referencePosition = Position;

            for (var cigarOpIndex = 0; cigarOpIndex < CigarData.Count; cigarOpIndex++)
            {
                var operation = CigarData[cigarOpIndex];
                var readSpan = operation.IsReadSpan();
                var refSpan = operation.IsReferenceSpan();

                for (var opIndex = 0; opIndex < operation.Length; opIndex++)
                {
                    if (readSpan)
                    {
                        PositionMap[readIndex] = refSpan ? referencePosition++ : -1;
                        readIndex ++;
                    }
                    else if (refSpan)
                    {
                        referencePosition++;
                    }
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1}:{2}", Name, Chromosome, Position);
        }
    }
}

