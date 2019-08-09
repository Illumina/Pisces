using System;
using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;

namespace Pisces.Domain.Utility
{
    public static class CigarExtensions
    {
        public static bool IsSupported(this CigarAlignment cigar)
        {
            for (var i = 0; i < cigar.Count; i++)
            {
                switch (cigar[i].Type)
                {
                    case 'S':
                    case 'D':
                    case 'I':
                    case 'M':
                        continue;
                    default:
                        return false;
                }
            }

            return true;
        }

        public static CigarAlignment GetReverse(this CigarAlignment cigar)
        {
            var reverseCigar = new CigarAlignment(cigar);
            reverseCigar.Reverse();

            return reverseCigar;
        }

        public static bool HasOperationAtOpIndex(this CigarAlignment cigar, int index, char type, bool fromEnd = false)
        {
            if (cigar == null) return false;

            var opIndex = fromEnd ? cigar.Count - index - 1 : index;
            return cigar.Count > opIndex && opIndex >= 0 && cigar[opIndex].Type == type;
        }

        // trim a cigar string up to a specified read length, cigar operations that don't span a read are taken as is
        public static CigarAlignment GetTrimmed(this CigarAlignment cigar, int readCycles, bool fromEnd = false, bool includeEndDels = true)
        {
            var numBases = 0;
            var sourceCigar = fromEnd ? cigar.GetReverse() : cigar;

            var trimmedCigar = new CigarAlignment();

            if (readCycles > 0)
            {
                for (var i = 0; i < sourceCigar.Count; i++)
                {
                    var operation = sourceCigar[i];
                    if (!operation.IsReadSpan())
                    {
                        if (numBases < readCycles || includeEndDels)
                            trimmedCigar.Add(operation); // doesn't contribute any read cycles (e.g. deletion), just add

                    }
                    else if (operation.Length + numBases <= readCycles)
                    {
                        trimmedCigar.Add(operation);
                        numBases += (int)operation.Length;
                    }
                    else
                    {
                        if (readCycles - numBases > 0)
                            trimmedCigar.Add(new CigarOp(operation.Type, (uint)(readCycles - numBases)));
                        break;
                    }
                }
            }

            if (fromEnd)
                trimmedCigar.Reverse();

            return trimmedCigar;
        }

        public static CigarAlignment GetSubCigar(this CigarAlignment cigar, int start, int end)
        {
            if (start > end) throw new ArgumentException("To get a sub-cigar between operation numbers, start must be less than or equal to end. Values supplied: " + start + ", " + end + ".");

            var trimmedCigar = new CigarAlignment();

            var opsCount = 0;

            foreach (CigarOp op in cigar)
            {
                if (opsCount >= start && opsCount < end)
                {
                    trimmedCigar.Add(op);
                }

                opsCount++;
            }

            return trimmedCigar;
        }

        public static bool HasInternalSoftclip(this CigarAlignment cigar)
        {
            var subCigar = cigar.GetSubCigar(cigar.GetPrefixClip() > 0 ? 1 : 0, cigar.Count - (cigar.GetSuffixClip() > 0 ? 1 : 0) );
            foreach (CigarOp op in subCigar)
            {
                if (op.Type == 'S')
                    return true;
            }
            return false;
        }

        public static CigarAlignment GetClippedCigar(this CigarAlignment cigar, int start, int end, bool includeEndDels = true, bool includeWholeEndIns = false)
        {
            var numBases = 0;
            var sourceCigar = cigar;

            var readCycles = end - start;
            
            var trimmedCigar = new CigarAlignment();

            var prefixDels = new CigarAlignment();
            bool lastWasDeletion = false;

            if (readCycles > 0)
            {
                for (var i = 0; i < sourceCigar.Count; i++)
                {
                    var operation = sourceCigar[i];

                    if (operation.IsReadSpan() && numBases + operation.Length - 1 < start)
                    {
                        lastWasDeletion = false;
                        numBases += (int)operation.Length;
                        continue;
                    }

                    if (!operation.IsReadSpan())
                    {
                        if (prefixDels.Count > 0 && !lastWasDeletion)
                        {
                            prefixDels.Clear();
                        }
                        if (trimmedCigar.Count == 0 && includeEndDels)
                        {
                            prefixDels.Add(operation);
                        }

                        if (trimmedCigar.Count > 0 && (numBases < readCycles || includeEndDels))
                            trimmedCigar.Add(operation); // doesn't contribute any read cycles (e.g. deletion), just add
                    }
                    else if (operation.Length + numBases <= end)
                    {
                        if (lastWasDeletion && prefixDels.Count > 0)
                        {
                            foreach (CigarOp prefixDel in prefixDels)
                            {
                                trimmedCigar.Add(prefixDel);
                            }
                        }
                        trimmedCigar.Add(operation);
                        numBases += (int)operation.Length;
                    }
                    else
                    {
                        if (lastWasDeletion && prefixDels.Count > 0)
                        {
                            foreach (CigarOp prefixDel in prefixDels)
                            {
                                trimmedCigar.Add(prefixDel);
                            }
                        }
                        if (end - numBases > 0)
                        {
                            if (includeWholeEndIns && operation.Type == 'I')
                            {
                                trimmedCigar.Add(new CigarOp(operation.Type, operation.Length));
                            }
                            else
                            {
                                trimmedCigar.Add(new CigarOp(operation.Type, (uint)(end - numBases)));
                            }
                        }
                        break;
                    }

                    lastWasDeletion = operation.Type == 'D';
                }
            }

            //for (var i = 0; i < sourceCigar.Count; i++)
            //{
            //    if (numBases >= end)
            //    {
            //        break;
            //    }

            //    var operation = sourceCigar[i];
            //    if (!operation.IsReadSpan())
            //    {
            //        if (numBases >= start && numBases <= end || includeEndDels)
            //            trimmedCigar.Add(operation); // doesn't contribute any read cycles (e.g. deletion), just add
            //        continue;
            //    }

            //    if (operation.Length + numBases >= end)
            //    {
            //        if (end - numBases > 0)
            //            trimmedCigar.Add(new CigarOp(operation.Type, (uint)(end - numBases - start)));
            //        //break;
            //    }
            //    else 
            //    {
            //        if (operation.Length + numBases >= start)
            //        {
            //            trimmedCigar.Add(operation);
            //        }
            //    }


            //    if (operation.IsReadSpan())
            //    {
            //        numBases += (int)operation.Length;
            //    }

            //}

            trimmedCigar.Compress();

            return trimmedCigar;
        }

        public static uint GetReadSpanBetweenClippedEnds(this CigarAlignment cigar)
        {
            return cigar.GetReadSpan() - cigar.GetPrefixClip() - cigar.GetSuffixClip();
        }

        /// <summary>
        /// If the read starts with insertion or starts with softclip followed by insertion, return the insertion length
        /// </summary>
        /// <returns></returns>
        public static int GetPrefixInsertionLength(this CigarAlignment cigar)
        {
            var insertionLength = 0;
            var cigarIndex = 0;

            if (cigar[0].Type == 'S')
                cigarIndex++;

            while (cigarIndex < cigar.Count && cigar[cigarIndex].Type == 'I')
            {
                insertionLength += (int)cigar[cigarIndex].Length;
                cigarIndex++;
            }

            return insertionLength;
        }

        /// <summary>
        /// If the read ends with insertion or ends with softclip preceeded by insertion, return the insertion length
        /// </summary>
        /// <returns></returns>
        public static int GetSuffixInsertionLength(this CigarAlignment cigar)
        {
            var insertionLength = 0;
            var cigarIndex = cigar.Count - 1;
    
            if (cigar[cigarIndex].Type == 'S')
                cigarIndex--;

            while (cigarIndex >= 0 && cigar[cigarIndex].Type == 'I')
            {
                insertionLength += (int)cigar[cigarIndex].Length;
                cigarIndex--;
            }

            return insertionLength;
        }

        public class CigarOpExpander
        {
            private readonly CigarAlignment _cigar;
            private int _cigarIndex;
            private int _opIndex;

            public CigarOpExpander(CigarAlignment cigar)
            {
                _cigar = cigar;
                _cigarIndex = 0;
                _opIndex = 0;
            }

            public bool MoveNext()
            {
                if (_cigarIndex < _cigar.Count)
                {
                    ++_opIndex;
                    if (_opIndex >= _cigar[_cigarIndex].Length)
                    {
                        _opIndex = 0;
                        ++_cigarIndex;
                    }
                }
                return IsNotEnd();
            }

            public bool IsNotEnd()
            {
                return _cigarIndex < _cigar.Count;
            }

            public void Reset()
            {
                _cigarIndex = 0;
                _opIndex = 0;
            }

            public char Current
            {
                get { return _cigar[_cigarIndex].Type; }
            }
        }

        public static List<CigarOp> Expand(this CigarAlignment cigar)
        {
            var expandedCigar = new List<CigarOp>();
            foreach (CigarOp op in cigar)
            {
                for (var i = 0; i < op.Length; i++)
                {
                    expandedCigar.Add(new CigarOp(op.Type, 1));
                }
            }

            return expandedCigar;
        }

        public static List<char> ExpandToChars(this CigarAlignment cigar)
        {
            var expandedCigar = new List<char>();
            foreach (CigarOp op in cigar)
            {
                for (var i = 0; i < op.Length; i++)
                {
                    expandedCigar.Add(op.Type);
                }
            }

            return expandedCigar;
        }

        public static bool IsReferenceSpan(char opType)
        {
            switch (opType)
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
        public static bool IsReadSpan(char opType)
        {
            switch (opType)
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
        public static void ExpandToChars(this CigarAlignment cigar, List<char> expandedCigar)
        {
            expandedCigar.Clear();
            int recycleIndex = 0;
            foreach (CigarOp op in cigar)
            {
                for (var i = 0; i < op.Length; ++i)
                {
                    expandedCigar.Add(op.Type);
                    ++recycleIndex;
                }
            }
        }

        public static void Expand(this CigarAlignment cigar, List<CigarOp> expandedCigar)
        {
            // Memory allocations from this function used to account for nearly
            // half of all allocations performed by the stitcher. Now there are 0.

            expandedCigar.Clear();
            int recycleIndex = 0;
            foreach (CigarOp op in cigar)
            {
                for (var i = 0; i < op.Length; ++i)
                {
                    expandedCigar.Add(new CigarOp(op.Type, 1));
                    ++recycleIndex;
                }
            }
        }

        public static CigarAlignment GetCigarWithoutProbeClips(this CigarAlignment cigar, bool isRead1)
        {
            return isRead1 ? 
                cigar.GetSubCigar(cigar.GetPrefixClip() > 0 ? 1 : 0, cigar.Count) : 
                cigar.GetSubCigar(0, cigar.Count - (cigar.GetSuffixClip() > 0 ? 1 : 0));
        }
    }
}
