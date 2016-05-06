using System;
using System.Text;
using SequencingFiles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    public class Read 
    {
        public CigarAlignment CigarData { get { return _bamAlignment == null ? null : _bamAlignment.CigarData; } }
        public string Sequence { get { return BamAlignment.Bases; } }
        public int ReadLength { get { return BamAlignment.Bases.Length; } }
        public byte[] Qualities { get { return BamAlignment.Qualities; }}
        public int Position { get { return BamAlignment.Position + 1; } }
        
        public int ClipAdjustedPosition
        {
            get { return Position - (int)CigarData.GetPrefixClip(); }
        }

        public int EndPosition { get { return Position + (int)CigarData.GetReferenceSpan() - 1; } }

        public int ClipAdjustedEndPosition
        {
            get { return EndPosition + (int)CigarData.GetSuffixClip(); }
        }
        public int MatePosition { get { return BamAlignment.MatePosition + 1; }}
        public string Name { get { return BamAlignment.Name; } } // cluster name
        public bool IsMapped { get { return BamAlignment.IsMapped(); } }
        public bool IsPrimaryAlignment { get { return BamAlignment.IsPrimaryAlignment(); } }
        public bool IsPcrDuplicate { get { return BamAlignment.IsDuplicate(); } }
        public bool IsProperPair { get { return BamAlignment.IsProperPair(); } }
        public uint MapQuality { get { return BamAlignment.MapQuality; } }
        public bool IsFirstMate { get { return BamAlignment.IsFirstMate(); } }

        public string Chromosome { get; private set; }
        public CigarAlignment StitchedCigar { get; set; }
        public DirectionType[] DirectionMap { get; set; }
        public int[] PositionMap { get; set; }  // for every base in the sequence, map back to reference position or -1 if does not map
        public bool HasCigar {
            get { return CigarData != null && CigarData.Count > 0; }
        }

        private BamAlignment _bamAlignment;
        public bool IsDuplex { get; set; }

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

        public Read(string chromosome, BamAlignment bamAlignment)
        {
            BamAlignment = bamAlignment;
            Chromosome = chromosome;

            if (string.IsNullOrEmpty(chromosome))
                throw new ArgumentException("Chromosome cannot be empty.");

            UpdateFromBam();
        }

        public Read()
        {
            BamAlignment = new BamAlignment() { Bases = string.Empty };
        }

        private void UpdateFromBam()
        {
            StitchedCigar = null;
            IsDuplex = false;

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

            if (BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                var xcTag = BamAlignment.GetStringTag("XC");
                if (xcTag != null)
                    StitchedCigar = new CigarAlignment(xcTag);

                var umi1 = BamAlignment.GetIntTag("XV");
                if (umi1 == null)
                    umi1 = BamAlignment.GetIntTag("X1");
                var umi2 = BamAlignment.GetIntTag("XW");
                if (umi2 == null)
                    umi2 = BamAlignment.GetIntTag("X2");

                if (umi1 != null && umi2 != null)
                    IsDuplex = umi1 != 0 && umi2 != 0;
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

        public void Reset(string chromosome, BamAlignment bamAlignment)
        {
            Chromosome = chromosome;
            BamAlignment = bamAlignment;
            UpdateFromBam();
        }

        private void UpdateMapFromCigar()
        {
            if (CigarData == null || CigarData.Count == 0)
                return;

            UpdatePositionMap(Position, CigarData, PositionMap);
        }

        public static void UpdateDirectionMap(DirectionInfo directionInfo, DirectionType[] directionMap)
        {
            var mapIndex = 0;
            foreach (var directionOp in directionInfo.Directions)
            {
                for (var i = 0; i < directionOp.Length; i ++)
                {
                    directionMap[i + mapIndex] = directionOp.Direction;
                }

                mapIndex += directionOp.Length;
            }
        }

        public static void UpdatePositionMap(int position, CigarAlignment cigarData, int[] positionMap, bool differentiateSoftClip = false)
        {
            if (cigarData.Count == 1 && (cigarData[0].Type =='I'|| cigarData[0].Type == 'D'))
                throw new Exception(string.Format("Invalid cigar '{0}': indel must have anchor", cigarData));

            if (cigarData.GetReadSpan() != positionMap.Length)
                throw new Exception(string.Format("Invalid cigar '{0}': does not match length {1} of read", cigarData, positionMap.Length));

            int readIndex = 0;
            int referencePosition = position;

            for (var cigarOpIndex = 0; cigarOpIndex < cigarData.Count; cigarOpIndex++)
            {
                var operation = cigarData[cigarOpIndex];
                var readSpan = operation.IsReadSpan();
                var refSpan = operation.IsReferenceSpan();

                for (var opIndex = 0; opIndex < operation.Length; opIndex++)
                {
                    if (readSpan)
                    {
                        positionMap[readIndex] = refSpan ? referencePosition++ : differentiateSoftClip && operation.Type == 'S' ? -2 : -1;
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

        public ReadCoverageSummary GetCoverageSummary()
        {
            return new ReadCoverageSummary()
            {
                ClipAdjustedStartPosition = ClipAdjustedPosition,
                ClipAdjustedEndPosition = ClipAdjustedEndPosition,
                Cigar = CigarData.DeepCopy(),
                //DirectionString = GetDirectionString()
                DirectionInfo = GetDirectionInfo()
            };
        }

        private DirectionInfo GetDirectionInfo()
        {
            var directionInfo = new DirectionInfo();
            DirectionType? lastDirection = null;
            var lastDirectionSize = 0;

            for (var i = 0; i < DirectionMap.Length; i++)
            {
                var direction = DirectionMap[i];
                if (!lastDirection.HasValue)  // first time, just set it
                {
                    lastDirection = direction;
                    lastDirectionSize++;
                }
                else
                {
                    if (direction == lastDirection)
                        lastDirectionSize++;
                    else
                    {
                        directionInfo.Directions.Add(new DirectionOp()
                        {
                            Direction = lastDirection.Value,
                            Length = lastDirectionSize
                        });
                        lastDirection = direction;
                        lastDirectionSize = 1;
                    }
                }
            }

            directionInfo.Directions.Add(new DirectionOp()
            {
                Direction = lastDirection.Value,
                Length = lastDirectionSize
            });

            return directionInfo;
        }
    }
}

