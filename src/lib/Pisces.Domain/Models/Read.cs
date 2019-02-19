using System;
using System.IO;
using Alignment.Domain.Sequencing;
using Common.IO.Utility;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Models
{
    /// <summary>
    /// ReadExtentions
    /// </summary>
    /// todo: handling non-proper reads?
    /// todo: throw exceptions for orientations other than RF, FR, FF, RR?
    public static class ReadExtentions
    {
        public static ReadCollapsedType? GetReadCollapsedType(this Read read, 
            DirectionType directionType)
        {
            var readPairDirection = read.ReadPairDirection;
            const string forwardTemplate = "FR";
            const string reverseTemplate = "RF";
            const string nonProperTemplateF1F2 = "FF";
            const string nonProperTemplateR1R2 = "RR";

            if (read.IsDuplex)
            {
                return directionType == DirectionType.Stitched ?
                    ReadCollapsedType.DuplexStitched : ReadCollapsedType.DuplexNonStitched;
            }
            else
            {
                if (directionType == DirectionType.Stitched)
                {
                    switch (readPairDirection)
                    {
                        case forwardTemplate:
                            return ReadCollapsedType.SimplexForwardStitched;
                        case reverseTemplate:
                            return ReadCollapsedType.SimplexReverseStitched;
                        case nonProperTemplateF1F2:
                        case nonProperTemplateR1R2:
                        default:
                            // non proper read pairs FF or RR are not considered 
                            return null;
                     }
                }
                else
                {
                    switch (readPairDirection)
                    {
                        case forwardTemplate:
                            return ReadCollapsedType.SimplexForwardNonStitched;
                        case reverseTemplate:
                            return ReadCollapsedType.SimplexReverseNonStitched;
                        case nonProperTemplateF1F2:
                        case nonProperTemplateR1R2:
                        default:
                            // non proper read pairs FF or RR are not considered
                            return null;
                    }
                }
            }
        }

        public static bool IsCollapsedRead(this Read read)
        {
            return read.BamAlignment.TagData != null &&
                   (read.BamAlignment.GetIntTag("XV") != null ||
                    read.BamAlignment.GetIntTag("XW") != null);
        }
    }

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

        public int EndPosition
        {
            get { return BamAlignment.EndPosition + 1; }
        }

        public int ClipAdjustedEndPosition
        {
            get { return EndPosition + (int)CigarData.GetSuffixClip(); }
        }

        public string ClippedSuffix	
        {	
            get	
            {	
                if (EndsWithSoftClip)	
                {	
                    return Sequence.Substring(ReadLength - (int) CigarData.GetSuffixClip());	
                }	
                else	
                {	
                    return "";	
                }	
            }	
        }	
	
        public string ClippedPrefix
        {
            get
            {
                if (StartsWithSoftClip)
                {
                    return Sequence.Substring(0, (int)CigarData.GetPrefixClip());
                }
                else
                {
                    return "";
                }
            }
        }


        public int MatePosition { get { return BamAlignment.MatePosition + 1; } }
        public string Name { get { return BamAlignment.Name; } } // cluster name
        public bool IsMapped { get { return BamAlignment.IsMapped(); } }
        public bool IsPrimaryAlignment { get { return BamAlignment.IsPrimaryAlignment(); } }
        public bool IsSupplementaryAlignment { get { return BamAlignment.IsSupplementaryAlignment(); } }
        public bool HasSupplementaryAlignment { get { return BamAlignment.HasSupplementaryAlignment(); } }
        public bool IsPcrDuplicate { get { return BamAlignment.IsDuplicate(); } }
        public bool IsProperPair { get { return BamAlignment.IsProperPair(); } }
        public uint MapQuality { get { return BamAlignment.MapQuality; } }
        public bool IsFirstMate { get { return BamAlignment.IsFirstMate(); } }
        public bool StartsWithSoftClip { get { return (HasCigar && CigarData[0].Type == 'S'); } }	
        public bool EndsWithSoftClip { get { return (HasCigar && CigarData[CigarData.Count - 1].Type == 'S'); } }




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

        public DirectionType[] ExpandedBaseDirectionMap
        {
            get
            {
                if (!_sequencedBaseDirectionMapInitialized)
                    SetSequencedBaseDirectionMapFromBam();  //this will also set the extended map at the same time
                return _expandedBaseDirectionMap;
            }
            set
            {
                _expandedBaseDirectionMap = value;
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
        private string _readPairDirection;
        private int[] _positionMap;
        private bool _positionMapInitialized;


        private CigarDirection _directionCigar;
        private bool _directionCigarInitialized;
        private bool _sequencedBaseDirectionMapInitialized;
        private DirectionType[] _sequencedBaseDirectionMap;
        private DirectionType[] _expandedBaseDirectionMap;

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

        public string ReadPairDirection
        {
            get
            {
                _readPairDirection = GetReadPairDirection();
                return _readPairDirection;
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
                ValidateCigar(BamAlignment.CigarData, BamAlignment.Bases.Length, BamAlignment.Name);
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
                var umi2 = BamAlignment.GetIntTag("XW");
                if (umi1 != null && umi2 != null)
                    return umi1 != 0 && umi2 != 0;
            }
            return false;
        }

        private string GetReadPairDirection()
        {
            const char forward = 'F';
            const char reverse = 'R';
            string xr = null;
            if (BamAlignment.TagData != null && BamAlignment.TagData.Length > 0)
            {
                xr = BamAlignment.GetStringTag("XR");
            } //read pair are stitched and direction is inferred from the XR tag
            if (xr == null && BamAlignment.IsProperPair())
            {
                var dir = BamAlignment.IsReverseStrand() ? reverse : forward;
                var dirmate = (dir == forward) ? reverse : forward;  //Assumption of opposite mate is valid only if reads are properly paired
                xr = BamAlignment.IsFirstMate() ? string.Format("{0}{1}", dir, dirmate) : string.Format("{0}{1}", dirmate, dir);
            } //read pair are not stiched and direction is inferred from the flag tag. Only properly paired reads are considered.
            return xr;
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
                UpdatePositionMap(Position, Name, CigarData, _positionMap);
            _positionMapInitialized = true;
        }

        private void SetSequencedBaseDirectionMapFromBam()
        {
            try
            {
                if (_sequencedBaseDirectionMap == null || _sequencedBaseDirectionMap.Length != ReadLength)
                {
                    _sequencedBaseDirectionMap = new DirectionType[ReadLength];

                    if (CigarDirections != null && (CigarDirections.Directions.Count > 0))
                    {
                        _expandedBaseDirectionMap = CigarDirections.Expand().ToArray();
                        _sequencedBaseDirectionMap = CreateSequencedBaseDirectionMap(_expandedBaseDirectionMap, CigarData);
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
            catch (Exception e)
            {
                throw new Exception("Exception caught in " + Name + ": " + e.StackTrace, e);
            }

        }

        /// <summary>
        /// This method takes in indexes in the "sequenced read" coordinate system and remaps them to indexes in the 
        /// "extended read" coordinate system. The mapping between the two coordinate systems is determined by the cigar string.
        /// We output the expandedBaseDirectionMap as a by-product of the method, for any downstream consuptions.
        /// For helpful examples of what this means, pls check the unit tests.
        /// </summary>
        /// <param name="indexesInSequencedReadToRemap">An array of indexes in the sequeced read. Ie, if the read is ACXXGT, the sequenced indexes are 01XX23</param>
        /// <param name="expandedBaseDirectionMap">An array of the directions for each base (including deletions).  Ie, if the read is ACXXGT, the directions might be FFSSRR</param>
        /// <returns>indexesInExtendedReadCoordinates</returns>
        public int[] SequencedIndexesToExpandedIndexes(int[] indexesInSequencedReadToRemap)
        {
            var expandedBaseCigarOpMap = CigarData.Expand();
            var numIndexesToRemap = indexesInSequencedReadToRemap.Length;
            var remappedIndexes = new int[numIndexesToRemap];
            int whichIndex = 0; 
            int maxSequencedIndex = _bamAlignment.Bases.Length - 1;
            int maxExpandedIndex = expandedBaseCigarOpMap.Count -1;
            var indexToRemap = indexesInSequencedReadToRemap[whichIndex];

            int sequencedBaseIndex = 0;

            while (whichIndex < numIndexesToRemap) 
            {
                for (int extendedIndex = 0; extendedIndex <= maxExpandedIndex; extendedIndex++)
                {

                    if ((indexToRemap < 0) || (indexToRemap > maxSequencedIndex))
                    {
                        throw new ArgumentException("Check index arguments for SequencedIndexesToExpandedIndexes method.");
                    }

                    var cigarOp = expandedBaseCigarOpMap[extendedIndex];

                    if (cigarOp.IsReadSpan())
                    {

                        if (sequencedBaseIndex == indexToRemap)
                        {
                            remappedIndexes[whichIndex]= extendedIndex;
                            whichIndex++;

                            if (whichIndex >= numIndexesToRemap)
                                return remappedIndexes;

                            indexToRemap = indexesInSequencedReadToRemap[whichIndex];
                        }
                        sequencedBaseIndex++;

                    }
                }
            }

            return remappedIndexes;
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

        public static void UpdatePositionMap(int position, string readName, CigarAlignment cigarData, int[] positionMap, bool differentiateSoftClip = false)
        {
            if (cigarData != null)
                ValidateCigar(cigarData, positionMap.Length, readName);

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

        private static void ValidateCigar(CigarAlignment cigarData, int readLength, string readName)
        {
            if (cigarData.Count == 1 && (cigarData[0].Type == 'I' || cigarData[0].Type == 'D'))
            {
                //tjd: change this to a warning to be more gentle to BWA-mem results
                //throw new InvalidDataException(string.Format("Invalid cigar '{0}': indel must have anchor", cigarData));
                Logger.WriteWarningToLog("Anomalous alignment {0}. '{1}': indel without anchor", readName, cigarData);
            }

            if (cigarData.Count > 0 && cigarData.GetReadSpan() != readLength)
                throw new InvalidDataException(string.Format("Check alignment {0}. Invalid cigar '{1}': does not match length {2} of read", readName, cigarData,
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

        public static DirectionType[] CreateSequencedBaseDirectionMap(DirectionType[] cigarBaseDirectionMap, CigarAlignment cigarData)
        {
            var cigarBaseAlleleMap = cigarData.Expand();
            var sequencedBaseDirectionMap = new DirectionType[cigarData.GetReadSpan()];

            int sequencedBaseIndex = 0;
            for (int cigarBaseIndex = 0; cigarBaseIndex < cigarBaseDirectionMap.Length; cigarBaseIndex++)
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
