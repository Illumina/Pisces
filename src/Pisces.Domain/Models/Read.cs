using System;
using System.IO;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    public class Read
    {
        public CigarAlignment CigarData { get { return _bamAlignment == null ? null : _bamAlignment.CigarData; } }
        public string Sequence { get { return BamAlignment.Bases; } }
        public int ReadLength { get { return BamAlignment.Bases.Length; } }
        public byte[] Qualities { get { return BamAlignment.Qualities; } }
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
        public int MatePosition { get { return BamAlignment.MatePosition + 1; } }
        public string Name { get { return BamAlignment.Name; } } // cluster name
        public bool IsMapped { get { return BamAlignment.IsMapped(); } }
        public bool IsPrimaryAlignment { get { return BamAlignment.IsPrimaryAlignment(); } }
        public bool IsPcrDuplicate { get { return BamAlignment.IsDuplicate(); } }
        public bool IsProperPair { get { return BamAlignment.IsProperPair(); } }
        public uint MapQuality { get { return BamAlignment.MapQuality; } }
        public bool IsFirstMate { get { return BamAlignment.IsFirstMate(); } }

        public string Chromosome { get; private set; }

        public CigarDirection CigarDirections
        {

            get
            {
                if (!_directionCigarInitialized)
                    GetDirectionCigarFromBam();
                return _directionCigar;
            }
            set
            {
                _directionCigar = value;
                _directionCigarInitialized = true;
            }

        }

        public CigarAlignment StitchedCigar
        {
            get
            {
                if (!_stitchedCigarInitialized)
                    GetStitchedCigarFromBam();
                return _stitchedCigar;
            }
            set
            {
                _stitchedCigar = value;
                _stitchedCigarInitialized = true;
            }
        }

        public DirectionType[] SequencedBaseDirectionMap
        {
            get
            {
                if (!_sequencedBaseDirectionMapInitialized)
                    SetSequencedBaseDirectionMapFromBam();
                return _sequencedBaseDirectionMap;
            }
            set
            {
                _sequencedBaseDirectionMap = value;
                _sequencedBaseDirectionMapInitialized = true;
            }
        }
        // for every base in the sequence, map back to reference position or -1 if does not map
        public int[] PositionMap
        {
            get
            {
                if (!_positionMapInitialized)
                    GetPositionMapFromBam();
                return _positionMap;
            }
            set
            {
                _positionMap = value;
                _positionMapInitialized = true;
            }
        }
        public bool HasCigar
        {
            get { return CigarData != null && CigarData.Count > 0; }
        }

        private BamAlignment _bamAlignment;

        private CigarAlignment _stitchedCigar;
        private bool _stitchedCigarInitialized;
        private bool? _isDuplex;
        private int[] _positionMap;
        private bool _positionMapInitialized;


        private CigarDirection _directionCigar;
        private bool _directionCigarInitialized;
        private bool _sequencedBaseDirectionMapInitialized;
        private DirectionType[] _sequencedBaseDirectionMap;


        public bool IsDuplex
        {
            get
            {
                if (!_isDuplex.HasValue)
                    _isDuplex = GetIsDuplexFromBam();
                return _isDuplex.Value;
            }
            set
            {
                _isDuplex = value;
            }
        }

        public BamAlignment BamAlignment
        {
            get { return _bamAlignment; }
            private set
            {
                if (value == null)
                    throw new ArgumentException("Alignment cannot be empty.");

                if (value.Bases == null)
                    throw new ArgumentException("Alignment sequence cannot be empty.");

                _bamAlignment = value;
                UpdateFromBam();
            }
        }

        public Read(string chromosome, BamAlignment bamAlignment)
        {
            if (string.IsNullOrEmpty(chromosome))
                throw new ArgumentException("Chromosome cannot be empty.");
            BamAlignment = bamAlignment;
            Chromosome = chromosome;
        }

        public Read()
        {
            BamAlignment = new BamAlignment() { Bases = string.Empty };
        }


        private void UpdateFromBam()
        {
            if (BamAlignment.CigarData != null)
                ValidateCigar(BamAlignment.CigarData, BamAlignment.Bases.Length);
            _stitchedCigarInitialized = _directionCigarInitialized = _sequencedBaseDirectionMapInitialized = _positionMapInitialized = false;
            _isDuplex = null;

            _directionCigar = null;
            _sequencedBaseDirectionMap = null;
        }

        private bool GetIsDuplexFromBam()
        {
            if (BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                var umi1 = BamAlignment.GetIntTag("XV");
                if (umi1 == null)
                    umi1 = BamAlignment.GetIntTag("X1");
                var umi2 = BamAlignment.GetIntTag("XW");
                if (umi2 == null)
                    umi2 = BamAlignment.GetIntTag("X2");

                if (umi1 != null && umi2 != null)
                    return umi1 != 0 && umi2 != 0;
            }
            return false;
        }


        private void GetDirectionCigarFromBam()
        {
            if (BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                var xdTag = BamAlignment.GetStringTag("XD");
                if (xdTag != null)
                    _directionCigar = new CigarDirection(xdTag);

                _directionCigarInitialized = true;
            }

        }


        private void GetStitchedCigarFromBam()
        {
            if (BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                var xcTag = BamAlignment.GetStringTag("XC");
                if (xcTag != null)
                    _stitchedCigar = new CigarAlignment(xcTag);
            }
            _stitchedCigarInitialized = true;
        }

        private void GetPositionMapFromBam()
        {
            if (_positionMap == null || _positionMap.Length != ReadLength)
                _positionMap = new int[ReadLength];

            for (var i = 0; i < ReadLength; i++)
            {
                _positionMap[i] = -1;
            }
            if (CigarData != null && CigarData.Count > 0)
                UpdatePositionMap(Position, CigarData, _positionMap);
            _positionMapInitialized = true;
        }

        private void SetSequencedBaseDirectionMapFromBam()
        {
            if (_sequencedBaseDirectionMap == null || _sequencedBaseDirectionMap.Length != ReadLength)
            {
                _sequencedBaseDirectionMap = new DirectionType[ReadLength];

                if (CigarDirections != null && (CigarDirections.Directions.Count > 0))
                {
                    _sequencedBaseDirectionMap = CreateSequencedBaseDirectionMap(CigarDirections, CigarData);
                }
                else
                {
                    var reverse = BamAlignment.IsReverseStrand();
                    for (var i = 0; i < ReadLength; i++)
                    {
                        _sequencedBaseDirectionMap[i] = reverse ? DirectionType.Reverse : DirectionType.Forward;
                    }
                }


                _sequencedBaseDirectionMapInitialized = true;
            }
        }

        public Read DeepCopy()
        {
            var read = new Read(Chromosome, new BamAlignment(BamAlignment));
            if (_stitchedCigarInitialized)
            {
                read.StitchedCigar = StitchedCigar.DeepCopy();
            }

            //read.StitchedCigar = StitchedCigar; // copy this one - it may have been reset

            if (_directionCigarInitialized)
            {
                read.CigarDirections = new CigarDirection( CigarDirections.ToString());
            }
            if (_sequencedBaseDirectionMapInitialized)
            {
                read.SequencedBaseDirectionMap = (DirectionType[])SequencedBaseDirectionMap.Clone();
            }
            if (_positionMapInitialized)
            {
                read.PositionMap = (int[])PositionMap.Clone();
            }
            return read;
        }

        public void Reset(string chromosome, BamAlignment bamAlignment)
        {
            Chromosome = chromosome;
            BamAlignment = bamAlignment;
            UpdateFromBam();
        }

   
        public static void UpdateDirectionMap(DirectionInfo directionInfo, DirectionType[] directionMap)
        {
            var mapIndex = 0;
            foreach (var directionOp in directionInfo.Directions)
            {
                for (var i = 0; i < directionOp.Length; i++)
                {
                    directionMap[i + mapIndex] = directionOp.Direction;
                }

                mapIndex += directionOp.Length;
            }
        }

        public static void UpdatePositionMap(int position, CigarAlignment cigarData, int[] positionMap, bool differentiateSoftClip = false)
        {
            if (cigarData != null)
                ValidateCigar(cigarData, positionMap.Length);

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
                        readIndex++;
                    }
                    else if (refSpan)
                    {
                        referencePosition++;
                    }
                }
            }
        }

        private static void ValidateCigar(CigarAlignment cigarData, int readLength)
        {
            if (cigarData.Count == 1 && (cigarData[0].Type == 'I' || cigarData[0].Type == 'D'))
                throw new InvalidDataException(string.Format("Invalid cigar '{0}': indel must have anchor", cigarData));

            if (cigarData.Count > 0 && cigarData.GetReadSpan() != readLength)
                throw new InvalidDataException(string.Format("Invalid cigar '{0}': does not match length {1} of read", cigarData,
                    readLength));
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
                DirectionInfo = GetDirectionInfo()
            };
        }

        private DirectionInfo GetDirectionInfo()
        {
            var directionInfo = new DirectionInfo();
            DirectionType? lastDirection = null;
            var lastDirectionSize = 0;

            for (var i = 0; i < SequencedBaseDirectionMap.Length; i++)
            {
                var direction = SequencedBaseDirectionMap[i];
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


        public static DirectionType[] CreateSequencedBaseDirectionMap(CigarDirection directionCigar, CigarAlignment cigarData)
        {
            var cigarBaseDirectionMap = directionCigar.Expand(); ;
            var cigarBaseAlleleMap = cigarData.Expand();
            var sequencedBaseDirectionMap = new DirectionType[cigarData.GetReadSpan()];

            int sequencedBaseIndex = 0;
            for (int cigarBaseIndex = 0; cigarBaseIndex < cigarBaseDirectionMap.Count; cigarBaseIndex++)
            {
                var cigarOp = cigarBaseAlleleMap[cigarBaseIndex];

                if (cigarOp.IsReadSpan()) //choices: (MIDNSHP)
                {
                    sequencedBaseDirectionMap[sequencedBaseIndex] = cigarBaseDirectionMap[cigarBaseIndex];
                    sequencedBaseIndex++;
                }

            }
            return sequencedBaseDirectionMap;
        }
    }
}
