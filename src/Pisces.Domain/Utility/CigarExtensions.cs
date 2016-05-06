using SequencingFiles;

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
            var reverseCigar = new CigarAlignment(cigar.ToString());
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
        public static CigarAlignment GetTrimmed(this CigarAlignment cigar, int readCycles, bool fromEnd = false)
        {
            var numBases = 0;
            var sourceCigar = fromEnd ? cigar.GetReverse() :  cigar;

            var trimmedCigar = new CigarAlignment();

            if (readCycles > 0)
            {
                for (var i = 0; i < sourceCigar.Count; i++)
                {
                    var operation = sourceCigar[i];
                    if (!operation.IsReadSpan())
                        trimmedCigar.Add(operation); // doesn't contribute any read cycles (e.g. deletion), just add
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
    }
}
