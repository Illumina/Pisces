using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Alignment.Domain.Sequencing
{
    public static class BamUtilities
    {
        /// <summary>
        ///     recreate the behavior of List &lt;T&gt;.Contains() for arrays:
        /// </summary>
        public static bool ArrayContains<T, TU>(T[] array, TU query)
        {
            foreach (var item in array)
            {
                if (item.Equals(query)) return true;
            }
            return false;
        }
    }

    public static class Constants
    {
        public const int FastqOffset = 33;
    }

    public class BamAlignment
    {
        // variables
        private const uint Paired = 1;
        private const uint ProperPair = 2;
        private const uint Unmapped = 4;
        private const uint MateUnmapped = 8;
        private const uint ReverseStrand = 16;
        private const uint MateReverseStrand = 32;
        private const uint Mate1 = 64;
        private const uint Mate2 = 128;
        private const uint Secondary = 256;
        private const uint FailedQC = 512;
        private const uint Duplicate = 1024;
        private const uint Supplementary = 2048;

        ///  <summary>
        ///  Alignment bit-flag - see Is&lt;something&gt;() methods to query this value, SetIs&lt;something&gt;() methods to manipulate
        ///  </summary>
        public uint AlignmentFlag;

        /// <summary>
        /// 'Original' sequence (as reported from sequencing machine) &lt;- This comment seems like a lie
        /// </summary>
        public string Bases;
        /// <summary>
        /// Bin in BAM file where this alignment resides
        /// </summary>
        public uint Bin;
        /// <summary>
        /// CIGAR operations for this alignment
        /// </summary>
        public CigarAlignment CigarData { get { return cigarData; } set { _endPosition = null; cigarData = value; } }
        private CigarAlignment cigarData;

        /// <summary>
        /// Read fragment length
        /// </summary>
        public int FragmentLength;
        /// <summary>
        /// Mapping quality score
        /// </summary>
        public uint MapQuality;
        /// <summary>
        /// Position (0-based) where alignment's mate starts
        /// </summary>
        public int MatePosition;
        /// <summary>
        /// ID number for reference sequence where alignment's mate was aligned
        /// </summary>
        public int MateRefID;
        /// <summary>
        /// Read name
        /// </summary>
        public string Name;
        /// <summary>
        /// Position (0-based) where alignment starts
        /// </summary>
        public int Position { get { return position; } set { _endPosition = null; position = value; } }
        private int position;

        /// <summary>
        ///  Phred qualities
        /// </summary>
        public byte[] Qualities;
        /// <summary>
        /// ID number for reference sequence
        /// </summary>
        public int RefID;
        /// <summary>
        /// Tag data (accessors will pull the requested information out)
        /// </summary>
        public byte[] TagData;

        /// <summary>
        /// Parses the cigar string to determine the last 
        /// (highest/rightmost) position on the genome covered by the read
        /// </summary>
        public int EndPosition => _endPosition ?? (_endPosition = Position + (int)CigarData.GetReferenceSpan() - 1).Value;
        private int? _endPosition;

        /// <summary>
        /// constructor
        /// </summary>
        public BamAlignment()
        {
            //tjd+
            //
            //This is a deliberate departure from the Isas lib (which leaves these two values unset)
            //When this is set to 0 (by simply being left unset)
            //that is a real index value. So trouble can occur wehn unmapped reads might appear to have a mate at ChrM.
            //How best to refactor this is currently under discussion with the Isas team.
            //
            //Related PISCES pull request:
            //(see BamAlignment constructor in BamCommon.cs):
            //https://git.illumina.com/Bioinformatics/Pisces5/pull/495/files 
            //Related ISAS pull request:
            //https://git.illumina.com/Isas/SequencingFiles/pull/127
            //
            //tjd-

            MatePosition = -1;
            MateRefID = -1;
            //tjd-

            CigarData = new CigarAlignment();
            _endPosition = null;
        }

        /// <summary>
        ///  Copy constructor.
        /// </summary>
        public BamAlignment(BamAlignment a)
        {
            AlignmentFlag = a.AlignmentFlag;
            Bases = a.Bases;
            Bin = a.Bin;
            CigarData = new CigarAlignment(a.CigarData.ToString());
            FragmentLength = a.FragmentLength;
            MapQuality = a.MapQuality;
            MatePosition = a.MatePosition;
            MateRefID = a.MateRefID;
            Name = a.Name;
            Position = a.Position;
            Qualities = new byte[a.Qualities.Length];
            Array.Copy(a.Qualities, Qualities, Qualities.Length);

            RefID = a.RefID;
            TagData = a.TagData;
            _endPosition = a._endPosition;
        }

        /// <summary>
        /// Compute the bam bin for an alignment covering [beg, end) - pseudocode taken straight from the bam spec.
        /// </summary>
        public void SetBin()
        {
            int beg = this.Position;
            int end = this.Position + (int)this.CigarData.GetReferenceSpan();
            end--;
            if (beg >> 14 == end >> 14)
            {
                this.Bin = (uint)(((1 << 15) - 1) / 7 + (beg >> 14));
                return;
            }
            if (beg >> 17 == end >> 17)
            {
                this.Bin = (uint)(((1 << 12) - 1) / 7 + (beg >> 17));
                return;
            }
            if (beg >> 20 == end >> 20)
            {
                this.Bin = (uint)(((1 << 9) - 1) / 7 + (beg >> 20));
                return;
            }
            if (beg >> 23 == end >> 23)
            {
                this.Bin = (uint)(((1 << 6) - 1) / 7 + (beg >> 23));
                return;
            }
            if (beg >> 26 == end >> 26)
            {
                this.Bin = (uint)(((1 << 3) - 1) / 7 + (beg >> 26));
                return;
            }
            this.Bin = 0;
        }

        /// <summary>
        ///     appends the supplied bytes to the end of the tag data byte array
        /// </summary>
        public void AppendTagData(byte[] b)
        {
            int newTagDataLen = TagData.Length + b.Length;
            byte[] newTagData = new byte[newTagDataLen];
            Array.Copy(TagData, newTagData, TagData.Length);
            Array.Copy(b, 0, newTagData, TagData.Length, b.Length);
            TagData = newTagData;
        }

        /// <summary>
        ///  Update Int Tag only
        /// </summary>
        public void UpdateIntTagData(string s, int value, bool addIfNotFound = false)
        {
            bool replaced = TagUtils.ReplaceOrAddIntTag(ref TagData, s, value, addIfNotFound);           
        }

        /// <summary>
        ///  calculates alignment end position, based on starting position and CIGAR operations
        /// </summary>
        public int GetEndPosition()
        {
            return EndPosition;
        }

        /// <summary>
        /// retrieves the character data associated with the specified tag
        /// </summary> 
        public char? GetCharTag(string s)
        {
            return TagUtils.GetCharTag(TagData, s);
        }

        /// <summary>
        /// retrieves the integer data associated with the specified tag
        /// </summary> 
        public int? GetIntTag(string s)
        {
            return TagUtils.GetIntTag(TagData, s);
        }

        /// <summary> 
        ///  retrieves the string data associated with the specified tag
        /// </summary>
        public string GetStringTag(string s)
        {
            return TagUtils.GetStringTag(TagData, s);
        }

        /// <summary>
        ///  accessors
        /// </summary>
        public bool IsDuplicate()
        {
            return ((AlignmentFlag & Duplicate) != 0);
        }

        public bool IsFailedQC()
        {
            return ((AlignmentFlag & FailedQC) != 0);
        }

        public bool IsFirstMate()
        {
            return ((AlignmentFlag & Mate1) != 0);
        }

        public bool IsMapped()
        {
            return ((AlignmentFlag & Unmapped) == 0);
        }

        public bool IsMateMapped()
        {
            return ((AlignmentFlag & MateUnmapped) == 0);
        }

        public bool HasPosition()
        {
            return (RefID >= 0);
        }

        public bool IsMateReverseStrand()
        {
            return ((AlignmentFlag & MateReverseStrand) != 0);
        }

        public bool IsPaired()
        {
            return ((AlignmentFlag & Paired) != 0);
        }

        /// <summary>
        /// Return true if this alignment is neither secondary nor supplementary.  If we're counting # of aligned reads,
        /// *these* records are the ones to count.
        /// </summary>
        public bool IsMainAlignment()
        {
            return (AlignmentFlag & (Supplementary + Secondary)) == 0;
        }

        public bool IsPrimaryAlignment()
        {
            return ((AlignmentFlag & Secondary) == 0);
        }

        public bool IsSupplementaryAlignment()
        {
            return ((AlignmentFlag & Supplementary) != 0);
        }

        public bool IsProperPair()
        {
            return ((AlignmentFlag & ProperPair) != 0);
        }

        public bool IsReverseStrand()
        {
            return ((AlignmentFlag & ReverseStrand) != 0);
        }

        public bool IsSecondMate()
        {
            return ((AlignmentFlag & Mate2) != 0);
        }

        public void SetIsDuplicate(bool b)
        {
            if (b) AlignmentFlag |= Duplicate;
            else AlignmentFlag &= ~Duplicate;
        }

        public void SetIsFailedQC(bool b)
        {
            if (b) AlignmentFlag |= FailedQC;
            else AlignmentFlag &= ~FailedQC;
        }

        public void SetIsFirstMate(bool b)
        {
            if (b) AlignmentFlag |= Mate1;
            else AlignmentFlag &= ~Mate1;
        }

        public void SetIsMateUnmapped(bool b)
        {
            if (b) AlignmentFlag |= MateUnmapped;
            else AlignmentFlag &= ~MateUnmapped;
        }

        public void SetIsMateReverseStrand(bool b)
        {
            if (b) AlignmentFlag |= MateReverseStrand;
            else AlignmentFlag &= ~MateReverseStrand;
        }

        public void SetIsPaired(bool b)
        {
            if (b) AlignmentFlag |= Paired;
            else AlignmentFlag &= ~Paired;
        }

        public void SetIsProperPair(bool b)
        {
            if (b) AlignmentFlag |= ProperPair;
            else AlignmentFlag &= ~ProperPair;
        }

        public void SetIsReverseStrand(bool b)
        {
            if (b) AlignmentFlag |= ReverseStrand;
            else AlignmentFlag &= ~ReverseStrand;
        }

        public void SetIsSecondaryAlignment(bool b)
        {
            if (b) AlignmentFlag |= Secondary;
            else AlignmentFlag &= ~Secondary;
        }

        public void SetIsSupplementaryAlignment(bool b)
        {
            if (b) AlignmentFlag |= Supplementary;
            else AlignmentFlag &= ~Supplementary;
        }

        public void SetIsSecondMate(bool b)
        {
            if (b) AlignmentFlag |= Mate2;
            else AlignmentFlag &= ~Mate2;
        }

        public void SetIsUnmapped(bool b)
        {
            if (b) AlignmentFlag |= Unmapped;
            else AlignmentFlag &= ~Unmapped;
        }

    }


    public static class BinaryIO
    {
        /// <summary>
        ///     Adds the bytes from the specified unsigned integer into a byte array
        /// </summary>
        public static void AddUIntBytes(ref byte[] b, ref int offset, uint num)
        {
            b[offset++] = (byte)num;
            b[offset++] = (byte)(num >> 8);
            b[offset++] = (byte)(num >> 16);
            b[offset++] = (byte)(num >> 24);
        }

        /// <summary>
        ///     Adds the bytes from the specified integer into a byte array
        /// </summary>
        public static void AddIntBytes(ref byte[] b, ref int offset, int num)
        {
            b[offset++] = (byte)num;
            b[offset++] = (byte)(num >> 8);
            b[offset++] = (byte)(num >> 16);
            b[offset++] = (byte)(num >> 24);
        }

        /// <summary>
        ///     Adds the bytes from the specified string into a byte array
        /// </summary>
        public static void AddNullTerminatedString(ref byte[] b, ref int offset, string s)
        {
            Encoding.ASCII.GetBytes(s, 0, s.Length, b, offset);
            offset += s.Length;
            b[offset++] = 0;
        }
    }

    // CIGAR operation codes
    internal enum CigarOpCodes
    {
        MatchAndMismatch = 0,
        Insertion = 1,
        Deletion = 2,
        SkippedRefRegion = 3,
        SoftClipping = 4,
        HardClipping = 5,
        Padding = 6
    };

    // CIGAR operation data type
    // This is a struct because as a class it was causing a large imapct
    // on performance through excessive memory allocations. As a struct,
    // there is no garbage collection.
    public struct CigarOp
    {
        private uint _length; // Operation length (number of bases)
        private char _type; // Operation type (MIDNSHP)

        // The CigarOp is read only because it is a struct. Structs are not
        // passed by reference and so setting the values may result in
        // unexpected behavior.
        public uint Length {  get { return _length; } }
        public char Type { get { return _type; } }

        public CigarOp(char type, uint length)
        {
            _type = type;
            _length = length;
        }

        public override bool Equals(Object obj)
        {
            // If parameter is null return false.
            if (obj == null) return false;

            // Throws an exception if the cast fails.
            CigarOp co = (CigarOp)obj;

            // Return true if the fields match:
            return (Type == co.Type) && (Length == co.Length);
        }

        public override int GetHashCode()
        {
            return ((Type << 24) | (int)Length);
        }

        public override string ToString()
        {
            return String.Format("{0}{1}", Length, Type);
        }

        public CigarOp DeepCopy()
        {
            return (CigarOp)MemberwiseClone();
        }

        public bool IsReferenceSpan()
        {
            switch (Type)
            {
                case 'M':
                case 'D':
                case 'N':
                case '=':
                case 'X':
                    return true;
                default:
                    return false;
            }
        }

        public bool IsReadSpan()
        {
            switch (Type)
            {
                case 'M':
                case 'I':
                case 'S':
                case '=':
                case 'X':
                    return true;
                default:
                    return false;
            }
        }
    }


    /// <summary>
    ///     subset of alignment information which is represented in a SAM/BAM CIGAR string
    /// </summary>
    public class CigarAlignment : IEnumerable
    {
        private readonly List<CigarOp> _data;

        public CigarAlignment()
        {
            _data = new List<CigarOp>();
        }

        public CigarAlignment(CigarAlignment other)
        {
            _data = new List<CigarOp>(other._data);
        }

        /// <summary>
        ///     initialize from SAM CIGAR string:
        /// </summary>
        public CigarAlignment(string cigarString)
            : this()
        {
            if (String.IsNullOrEmpty(cigarString) || (cigarString == "*")) return;

            int head = 0;
            for (int i = 0; i < cigarString.Length; ++i)
            {
                if (Char.IsDigit(cigarString, i)) continue;
                if (!BamUtilities.ArrayContains(BamConstants.CigarTypes, cigarString[i]))
                {
                    throw new InvalidDataException(string.Format("ERROR: Unexpected format in character {0} of CIGAR string: {1}",
                                                      (i + 1), cigarString));
                 
                }
                var length = uint.Parse(cigarString.Substring(head, i - head));
                var op = new CigarOp(cigarString[i], length);
                _data.Add(op);
                head = i + 1;
            }
            if (head != cigarString.Length)
            {
                throw new InvalidDataException(string.Format("ERROR: Unexpected format in CIGAR string: {0}", cigarString));

            }
        }

        public void Insert(int index, CigarOp op)
        {
            _data.Insert(index, op);
        }

        public int Count
        {
            get { return _data.Count; }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (CigarOp op in _data)
            {
                yield return op;
            }
        }

        public CigarOp this[int i]
        {
            get
            {
                return _data[i];
            }
            set
            {
                _data[i] = value;
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        public void Add(CigarOp op)
        {
            _data.Add(op);
        }

        public void Reverse()
        {
            _data.Reverse();
        }

        /// <summary>
        ///     reference distance spanned by alignment.
        /// </summary>
        public uint GetReferenceSpan()
        {
            uint length = 0;
            foreach (CigarOp op in _data)
            {
                if (op.IsReferenceSpan()) length += op.Length;
            }
            return length;
        }


        /// <summary>
        ///     read distance spanned by the alignmnet
        /// </summary>
        public uint GetReadSpan()
        {
            uint length = 0;
            foreach (CigarOp op in _data)
            {
                if (op.IsReadSpan()) length += op.Length;
            }
            return length;
        }

        public uint GetCigarSpan()
        {
            uint length = 0;
            foreach (CigarOp op in _data)
            {
                length += op.Length;
            }
            return length;
        }


        /// <summary>
        ///     provide the total leading soft-clip
        /// </summary>
        public uint GetPrefixClip()
        {
            uint length = 0;
            foreach (CigarOp op in _data)
            {
                if (op.Type == 'S')
                {
                    length += op.Length;
                }
                else if (op.Type != 'H')
                {
                    break;
                }
            }
            return length;
        }

        /// <summary>
        ///     provide the total trailing soft-clip
        /// </summary>
        public uint GetSuffixClip()
        {
            uint length = 0;

            for (int index = (_data.Count - 1); index >= 0; index--)
            {
                CigarOp op = _data[index];
                if (op.Type == 'S')
                {
                    length += op.Length;
                }
                else if (op.Type != 'H')
                {
                    break;
                }
            }
            return length;
        }

        /// <summary>
        /// Number of bases 'matched' in alignment
        /// </summary>
        public int CountMatches()
        {
            return CountOperations('M');
        }

        /// <summary>
        /// Number of bases 'matched' in alignment
        /// </summary>
        public int CountOperations(char operationType)
        {
            int matches = 0;
            foreach (CigarOp op in this)
                if (op.Type == operationType) matches += (int)op.Length;
            return matches;
        }

        /// <summary>
        ///  Given a read okkk
        /// </summary>
        /// <param name="readOffset"></param>
        /// <returns></returns>
        public int TranslateReadToReferenceOffset(int readOffset)
        {
            int result = 0;
            foreach (var cigarOp in _data)
            {
                if (readOffset >= 0)
                {
                    if (cigarOp.IsReadSpan())
                    {
                        result += cigarOp.IsReferenceSpan() ? (int)Math.Min(readOffset, cigarOp.Length) : 0;
                        readOffset -= (int)cigarOp.Length;
                    }
                    else
                    {
                        result += (int)cigarOp.Length;
                    }
                }
                else
                {
                    return result;
                }
            }
            return result;
        }
        /// <summary>
        ///     given an offset in the reference sequence from the start
        ///     of the alignment, return the corresponding read offset. Return
        ///     -1 if reference offset has no mapped read position
        /// </summary>
        public int TranslateReferenceToReadOffset(int refOffset)
        {
            const int noMapping = -1;

            int refPos = 0;
            int nextRefPos = 0;
            int readPos = 0;
            foreach (CigarOp op in _data)
            {
                if (refPos > refOffset) return noMapping;
                if (op.IsReferenceSpan()) nextRefPos += (int)op.Length;
                // target segment:
                if (nextRefPos > refOffset)
                {
                    if (op.IsReadSpan())
                    {
                        return readPos + (refOffset - refPos);
                    }
                    return noMapping;
                }

                refPos = nextRefPos;
                if (op.IsReadSpan()) readPos += (int)op.Length;
            }
            return noMapping;
        }

        /// <summary>
        ///     if duplicated adjacent tags are present, reduce them to one copy,
        ///     also reduce adjacent insertion/deletion tags to a single pair
        /// </summary>
        /// <returns>true if cigar was altered by compression</returns>
        public bool Compress()
        {
            for (int segmentIndex = 0; segmentIndex < _data.Count; ++segmentIndex)
            {
                if (_data[segmentIndex].Length == 0) continue;
                for (int j = (segmentIndex + 1); j < _data.Count; ++j)
                {
                    if (_data[j].Length == 0) continue;
                    if (_data[segmentIndex].Type != _data[j].Type) break;
                    _data[segmentIndex] = new CigarOp(_data[segmentIndex].Type, _data[segmentIndex].Length + _data[j].Length);
                    _data[j] = new CigarOp(_data[j].Type, 0);
                }
            }

            int insertIndex = -1;
            int deleteIndex = -1;
            for (int segmentIndex = 0; segmentIndex < _data.Count; ++segmentIndex)
            {
                if (_data[segmentIndex].Length == 0) continue;
                if (_data[segmentIndex].Type == 'I')
                {
                    if (insertIndex >= 0 && deleteIndex >= 0)
                    {
                        _data[insertIndex] = new CigarOp(_data[insertIndex].Type, _data[insertIndex].Length + _data[segmentIndex].Length);
                        _data[segmentIndex] = new CigarOp(_data[segmentIndex].Type, 0);
                    }
                    if (insertIndex == -1) insertIndex = segmentIndex;
                }
                else if (_data[segmentIndex].Type == 'D')
                {
                    if (insertIndex >= 0 && deleteIndex >= 0)
                    {
                        _data[deleteIndex] = new CigarOp(_data[deleteIndex].Type, _data[deleteIndex].Length + _data[segmentIndex].Length);
                        _data[segmentIndex] = new CigarOp(_data[segmentIndex].Type, 0);
                    }
                    if (deleteIndex == -1) deleteIndex = segmentIndex;
                }
                else
                {
                    insertIndex = -1;
                    deleteIndex = -1;
                }
            }
            int numberRemoved = _data.RemoveAll(op => (op.Length == 0));
            return (numberRemoved != 0);
        }

        /// <summary>
        ///     Convert to SAM CIGAR format:
        /// </summary>
        public override string ToString()
        {
            StringBuilder val = new StringBuilder();
            foreach (CigarOp op in _data)
            {
                val.Append(op);
            }
            return val.ToString();
        }

        public CigarAlignment DeepCopy()
        {
            CigarAlignment val = new CigarAlignment();
            foreach (CigarOp op in _data)
            {
                val.Add(op.DeepCopy());
            }
            return val;
        }
    }

    // commonly used constants
    public static class BamConstants
    {
        public const uint CoreAlignmentDataLen = 32;
        public const int CigarShift = 4;
        public const uint CigarMask = ((1 << CigarShift) - 1);
        public const int MaxReadLength = 2048;
        public const int BlockHeaderLength = 18;
        public const int BlockFooterLength = 8;
        public const ushort GzipMagicNumber = 35615;
        public const string MagicNumber = "BAM\x1";
        public const string BaiMagicNumber = "BAI\x1";
        public const uint LutError = 255;
        public const int BestCompression = 9;
        public const int DefaultCompression = -1;
        public const int BestSpeed = 1;
        public static char[] CigarTypes = { 'M', 'I', 'D', 'N', 'S', 'H', 'P', '=', 'X' };
    }

    public class TagUtils
    {
        private readonly List<byte> _mByteList;

        // constructor
        public TagUtils()
        {
            _mByteList = new List<byte>();
        }

        // adds a string tag
        public void AddStringTag(string key, string value)
        {
            foreach (char c in key) _mByteList.Add((byte)c);
            _mByteList.Add(90); // Z
            foreach (char c in value) _mByteList.Add((byte)c);
            _mByteList.Add(0); // null termination
        }

        // adds an integer tag
        public void AddIntTag(string key, int value)
        {
            foreach (char c in key) _mByteList.Add((byte)c);
            _mByteList.Add(105); // i
            _mByteList.Add((byte)value);
            _mByteList.Add((byte)(value >> 8));
            _mByteList.Add((byte)(value >> 16));
            _mByteList.Add((byte)(value >> 24));
        }

        // adds a char tag
        public void AddCharTag(string key, char value)
        {
            foreach (char c in key) _mByteList.Add((byte)c);
            _mByteList.Add(65); // A
            _mByteList.Add((byte)value);
        }

        // clears the tag data
        public void Clear()
        {
            _mByteList.Clear();
        }

        // returns the byte array
        public byte[] ToBytes()
        {
            return _mByteList.ToArray();
        }

        /// <summary>
        /// Returns the index for the first byte of the tag, including the tagKey
        /// </summary>
        /// <returns>the index, or -1 if not found</returns>
        private static int GetTagBeginIndex(byte[] tagData, string tagKey)
        {
            // convert the string into bytes
            byte[] tagNameBytes = new byte[2];
            tagNameBytes[0] = (byte)tagKey[0];
            tagNameBytes[1] = (byte)tagKey[1];

            int tagDataBegin = -1;
            char dataType;

            int tagIndex = 0;
            while (tagIndex < tagData.Length)
            {
                // check the current tag
                if ((tagData[tagIndex] == tagNameBytes[0]) && (tagData[tagIndex + 1] == tagNameBytes[1]))
                {
                    tagDataBegin = tagIndex;
                    break;
                }

                // skip to the next tag
                tagIndex += 2;
                dataType = Char.ToUpper((char)tagData[tagIndex]);
                tagIndex++;

                switch (dataType)
                {
                    case 'A':
                    case 'C':
                        tagIndex++;
                        break;

                    case 'S':
                        tagIndex += 2;
                        break;

                    case 'I':
                    case 'F':
                        tagIndex += 4;
                        break;

                    case 'Z':
                    case 'H':
                        while (tagData[tagIndex] != 0) tagIndex++;
                        tagIndex++;
                        break;

                    default:
                        throw new InvalidDataException(
                            string.Format("Found an unexpected BAM tag data type: [{0}] while looking for a tag ({1})",
                                          dataType, tagKey));
                }
            }
            return tagDataBegin;
        }




        #region Getting Values
        // retrieves the integer data associated with the specified tag
        public static char? GetCharTag(byte[] tagData, string tagKey)
        {

            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);

            if (tagDataBegin < 0) return null;

            // grab the value
            char dataType = Char.ToUpper((char)tagData[tagDataBegin + 2]);
            char? ret = null;
            switch (dataType)
            {
                // character
                case 'A':
                    ret = (char)tagData[tagDataBegin + 3];
                    break;

                default:
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected character BAM tag data type: [{0}] while looking for a tag ({1})",
                            dataType, tagKey));
            }

            return ret;
        }

        // retrieves the integer data associated with the specified tag
        public static int? GetIntTag(byte[] tagData, string tagKey)
        {
            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);

            if (tagDataBegin < 0) return null;

            // grab the value
            int ret = 0;
            char dataType = Char.ToUpper((char)tagData[tagDataBegin + 2]);
            
            switch (dataType)
            {
                // signed and unsigned int8
                case 'C':
                    ret = tagData[tagDataBegin + 3];
                    break;

                // signed and unsigned int16
                case 'S':
                    ret = BitConverter.ToInt16(tagData, tagDataBegin + 3);
                    break;

                // signed and unsigned int32
                case 'I':
                    ret = BitConverter.ToInt32(tagData, tagDataBegin + 3);
                    break;

                default:
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected integer BAM tag data type: [{0}] while looking for a tag ({1})",
                            dataType, tagKey));
            }

            return ret;
        }

        // retrieves the string data associated with the specified tag
        public static string GetStringTag(byte[] tagData, string tagKey)
        {
            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);

            if (tagDataBegin < 0) return null;

            // grab the value
            char dataType = Char.ToUpper((char)tagData[tagDataBegin + 2]);
            tagDataBegin += 3;    // the beginning of the tag value
            string ret = null;

            switch (dataType)
            {
                // null terminated and hex strings
                case 'Z':
                case 'H':
                    int len = 0;
                    while (tagData[tagDataBegin + len] != 0) len++;
                    ret = Encoding.ASCII.GetString(tagData, tagDataBegin, len);
                    break;
                case 'A':
                case 'C':
                    // single character
                    ret += Encoding.ASCII.GetChars(tagData, tagDataBegin, 1)[0];
                    break;
                default:
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected string BAM tag data type: [{0}] while looking for a tag ({1})",
                            dataType, tagKey));
            }

            return ret;
        }
        #endregion

        #region Replacing Values

        /// <summary>
        /// Verify that the existing tag is the correct type
        /// <para>Tag type is tagData[tagDataBegin + 2]</para>
        /// </summary>
        /// <param name="dataType">Tag type is tagData[tagDataBegin + 2]</param>
        /// <param name="expectedType">Uppercase char ('A' for char, 'Z' for string, etc.)</param>
        /// <param name="tagKey">Name of the tag (for a more helpful error message)</param>
        private static void AssertTagType(byte dataType, char expectedType, string tagKey)
        {
            if (char.ToUpper((char)dataType) != expectedType)
                throw new InvalidDataException(
                    string.Format(
                        "Found an unexpected char BAM tag data type: [{0}] while looking for a tag ({1})",
                        dataType, tagKey));
        }

        /// <summary>
        /// Replace or add a char tag
        /// <para>Returns true if value was replaced (false if tag was added)</para>
        /// </summary>
        /// <param name="tagData">i.e. BamAlignment.TagData</param>
        /// <param name="tagKey">Name of the tag</param>
        /// <param name="value">Value of the tag</param>
        /// <param name="addIfMissing">If true, missing tags will be created. 
        /// <para>Otherwise, no changes are made and method returns false.</para></param>
        /// <returns>True if value was replaced (false if tag was added)</returns>
        public static bool ReplaceOrAddCharTag(ref byte[] tagData, string tagKey, char value, bool addIfMissing = true)
        {
            bool replaced = true;
            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);
            if (tagDataBegin < 0 && addIfMissing)
            {
                replaced = false;
                // Make room for the tag
                byte[] newTagData = new byte[tagData.Length + 4];
                Array.Copy(tagData, newTagData, tagData.Length);
                tagDataBegin = tagData.Length;
                tagData = newTagData;

                // Add the tag metadata
                tagData[tagDataBegin] = (byte)tagKey[0];
                tagData[tagDataBegin + 1] = (byte)tagKey[1];
                tagData[tagDataBegin + 2] = (byte)'A';
            }
            else if (tagDataBegin < 0)
            {
                return false;
            }
            else
            {
                // Make sure the tag we are replacing is the right type
                AssertTagType(tagData[tagDataBegin + 2], 'A', tagKey);
            }

            // Add the tag
            tagData[tagDataBegin + 3] = (byte)value;

            return replaced;
        }


        /// <summary>
        /// Replace or add the specified int tag
        /// <para>Returns true if tag is replaced (false if tag is added)</para>
        /// </summary>
        /// <param name="tagData">i.e. BamAlignment.TagData</param>
        /// <param name="tagKey">Name of the tag</param>
        /// <param name="value">Value of the tag</param>
        /// <returns></returns>
        public static bool ReplaceOrAddIntTag(ref byte[] tagData, string tagKey, int value, bool addIfNotFound = false)
        {
            bool replaced = true;
            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);

            if (tagDataBegin < 0 && addIfNotFound == false) {
                replaced = false;
                return replaced;
            }
            if (tagDataBegin < 0 && addIfNotFound) {
                replaced = false;
                // Make room for the tag
                byte[] newTagData = new byte[tagData.Length + 7];
                Array.Copy(tagData, newTagData, tagData.Length);
                tagDataBegin = tagData.Length;
                tagData = newTagData;

                // Add the tag metadata
                tagData[tagDataBegin] = (byte)tagKey[0];
                tagData[tagDataBegin + 1] = (byte)tagKey[1];
                tagData[tagDataBegin + 2] = (byte)'I';
            }

            // grab the value
            char dataType = Char.ToUpper((char)tagData[tagDataBegin + 2]);

            switch (dataType)
            {
                // signed and unsigned int8
                case 'C':
                    tagData[tagDataBegin + 3] = (byte)value;
                    break;

                // signed and unsigned int16
                case 'S':
                    tagData[tagDataBegin + 3] = (byte)value;
                    tagData[tagDataBegin + 4] = (byte)(value >> 8);
                    break;

                // signed and unsigned int32
                case 'I':
                    tagData[tagDataBegin + 3] = (byte)value;
                    tagData[tagDataBegin + 4] = (byte)(value >> 8);
                    tagData[tagDataBegin + 5] = (byte)(value >> 16);
                    tagData[tagDataBegin + 6] = (byte)(value >> 24);
                    break;

                default:
                    throw new InvalidDataException(
                        string.Format(
                            "Found an unexpected integer BAM tag data type: [{0}] while looking for a tag ({1})",
                            dataType, tagKey));
            }

            return replaced;
        }

        /// <summary>
        /// Replace or add the specified string tag
        /// <para>Returns true if tag is replaced (false if tag is added)</para>
        /// </summary>
        /// <param name="tagData">i.e. BamAlignment.TagData</param>
        /// <param name="tagKey">Name of the tag</param>
        /// <param name="value">Value of the tag</param>
        /// <returns></returns>
        public static bool ReplaceOrAddStringTag(ref byte[] tagData, string tagKey, string value)
        {
            int tagDataBegin = GetTagBeginIndex(tagData, tagKey);

            bool found = false;
            int tagDataEnd;
            if (tagDataBegin < 0)
            {
                tagDataBegin = tagData.Length;
                tagDataEnd = tagData.Length;
            }
            else
            {
                found = true;
                // check the tag type
                char dataType = Char.ToUpper((char)tagData[tagDataBegin + 2]);
                if (dataType != 'Z')
                    throw new InvalidDataException(string.Format(
                                "Found an unexpected char BAM tag data type: [{0}] while looking for a tag ({1})",
                                dataType, tagKey));

                // find the end of the tag
                tagDataEnd = tagDataBegin + 3;
                while (tagData[tagDataEnd] != 0) tagDataEnd++;
            }

            // if the new value doesn't have the same length as the old one, 
            //   or if the tag was non-existent, we need to reallocate
            if (tagDataEnd - tagDataBegin - 3 != value.Length)
            {
                int sizeDiff = value.Length - (tagDataEnd - tagDataBegin - 3);
                if (!found) sizeDiff++; // one more character for the final \0
                byte[] newTagData = new byte[tagData.Length + sizeDiff];
                if (found)
                {
                    for (int i = 0; i < tagDataBegin + 3; i++)
                    {
                        newTagData[i] = tagData[i];
                    }

                    for (int i = tagDataEnd; i < tagData.Length; i++)
                    {
                        newTagData[i + sizeDiff] = tagData[i];
                    }
                }
                else
                {
                    for (int i = 0; i < tagData.Length; i++)
                    {
                        newTagData[i] = tagData[i];
                    }
                    newTagData[tagData.Length] = (byte)tagKey[0];
                    newTagData[tagData.Length + 1] = (byte)tagKey[1];
                    newTagData[tagData.Length + 2] = (byte)'Z';
                    newTagData[newTagData.Length - 1] = 0;
                }
                tagData = newTagData;
            }

            // overwrite the tag
            for (int valueIndex = 0; valueIndex < value.Length; valueIndex++)
            {
                tagData[tagDataBegin + 3 + valueIndex] = (byte)value[valueIndex];
            }

            return found;
        }

        #endregion


    }
}