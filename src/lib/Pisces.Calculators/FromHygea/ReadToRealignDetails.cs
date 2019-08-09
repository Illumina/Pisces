using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;

namespace Gemini.FromHygea
{
    public partial class GeminiReadRealigner
    {
        public class ReadToRealignDetails
        {
            public readonly int Position;   
            public readonly int NPrefixLength;
            public readonly int NSuffixLength;
            public readonly PositionMap PositionMapWithoutTerminalNs;
            public readonly uint PrefixSoftclip;
            public readonly uint SuffixSoftclip;
            public readonly string SequenceWithoutTerminalNs;
            public readonly CigarAlignment FreshCigarWithoutTerminalNs;
            public readonly int PositionMapLength;

            public ReadToRealignDetails(Read read, int position, bool keepProbeSoftclips = false, bool keepBothSideSoftclips = false)
            {
                var freshCigarWithoutTerminalNsRaw = new CigarAlignment();
                
                NPrefixLength = read.GetNPrefix();

                NSuffixLength = read.GetNSuffix();

                if (keepProbeSoftclips)
                {
                    if (keepBothSideSoftclips || (!read.BamAlignment.IsReverseStrand() || !read.BamAlignment.IsPaired()) && NPrefixLength == 0)
                    {
                        NPrefixLength = (int)read.CigarData.GetPrefixClip();
                    }
                    if (keepBothSideSoftclips || (read.BamAlignment.IsReverseStrand() || !read.BamAlignment.IsPaired()) && NSuffixLength == 0)
                    {
                        NSuffixLength = (int)read.CigarData.GetSuffixClip();
                    }
                }

                // Only build up the cigar for the non-N middle. Add the N prefix back on after the realignment attempts.
                freshCigarWithoutTerminalNsRaw.Add(new CigarOp('M', (uint)(read.Sequence.Length - NPrefixLength - NSuffixLength)));
                freshCigarWithoutTerminalNsRaw.Compress();

                // start with fresh position map
                var positionMapWithoutTerminalNs = new PositionMap(read.ReadLength - NPrefixLength - NSuffixLength);
                Read.UpdatePositionMap(position, freshCigarWithoutTerminalNsRaw, positionMapWithoutTerminalNs);
                PrefixSoftclip = read.CigarData.GetPrefixClip();
                SuffixSoftclip = read.CigarData.GetSuffixClip();

                SequenceWithoutTerminalNs =
                    read.Sequence.Substring(NPrefixLength, read.Sequence.Length - NPrefixLength - NSuffixLength);

                PositionMapWithoutTerminalNs = positionMapWithoutTerminalNs;
                PositionMapLength = positionMapWithoutTerminalNs.Length;
                FreshCigarWithoutTerminalNs = freshCigarWithoutTerminalNsRaw;
                Position = position;
            }
        }
    }
}