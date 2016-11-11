using System;
using System.Collections.Generic;
using System.Linq;
//using SequencingFiles;

namespace Alignment.Domain.Sequencing

{
    public static class BamAlignmentExtensions
    {
        public static long GetOverlap(this BamAlignment alignment, int start, int end)
        {
            //From pysam.get_overlap:
            //return number of aligned bases of read overlapping the interval start and end on the reference sequence
            //Return None if cigar alignment is not available.

            if (alignment.CigarData == null)
            {
                throw new Exception(string.Format("Cannot get overlap of read {0}: Cigar alignment is not available.", alignment.Name));
            }

            var pos = alignment.Position;
            var overlap = 0L;

            foreach (CigarOp op in alignment.CigarData)
            {
                if (op.Type == 'M') // TODO why is this for just M??? 
                {
                    var o = Math.Min(pos + op.Length, end) - Math.Max(pos, start);
                    if (o > 0) overlap += o;
                }

                if (op.IsReferenceSpan()) pos += (int)op.Length;
            }

            return overlap;
        }

        public static int OneBasedPosition(this BamAlignment alignment)
        {
            return alignment.Position + 1;
        }

        public static int OneBasedEndPosition(this BamAlignment alignment)
        {
            return alignment.GetEndPosition() + 1;
        }

        public static bool HasSupplementaryAlignment(this BamAlignment alignment)
        {
            if (alignment.TagData == null) return false;

            var saTag = alignment.GetStringTag("SA");
            return saTag != null;
        }

        //TODO this looks like something that might be generally useful - consider moving closer to BamAlignment/CigarData definition
        public static int NumOperationsOfType(this BamAlignment alignment, char operationType)
        {
            var count = 0;
            for (var i = 0; i < alignment.CigarData.Count; i++)
            {
                var op = alignment.CigarData[i];
                if (op.Type == operationType) count++;
            }

            return count;
        }

        //TODO this looks like something that might be generally useful - consider moving closer to BamAlignment/CigarData definition
        public static long CountBasesWithOperationType(this BamAlignment alignment, char operationType)
        {
            long count = 0;
            for (var i = 0; i < alignment.CigarData.Count; i++)
            {
                var op = alignment.CigarData[i];
                if (op.Type == operationType) count += op.Length;
            }

            return count;
        }

        public static bool ContainsPosition(this BamAlignment alignment, long position, int refId, bool startInclusive = true, bool endInclusive = true)
        {
            return alignment.RefID == refId &&
                (startInclusive ? alignment.Position <= position : alignment.Position < position) &&
                (endInclusive ? alignment.GetLastBasePosition() >= position : alignment.GetLastBasePosition() > position);
        }

        public static int GetLastBasePosition(this BamAlignment alignment)
        {
            return alignment.GetEndPosition() - 1;
        }

        public static bool OverlapsAlignment(this BamAlignment read1, BamAlignment read2)
        {
            var r2StartIsInR1 = read1.ContainsPosition(read2.Position, read2.RefID);
            var r2EndIsInR1 = read1.ContainsPosition(read2.GetLastBasePosition(), read2.RefID);
            var r1StartIsInR2 = read2.ContainsPosition(read1.Position, read1.RefID);
            var r1EndIsInR2 = read2.ContainsPosition(read1.GetLastBasePosition(), read1.RefID);

            return ((r2StartIsInR1 || r2EndIsInR1 || r1StartIsInR2 || r1EndIsInR2));

        }

        //TODO may be able to get rid of this and just use !IsPrimary
        public static bool IsSecondary(this BamAlignment alignment)
        {
            return ((alignment.AlignmentFlag & 256) != 0);
        }

        // TODO maybe this should be extension of CigarAlignment?
        public static bool ContainsDisallowedCigarOps(this BamAlignment alignment, char[] allowedChars)
        {
            foreach (CigarOp op in alignment.CigarData)
            {
                if (!allowedChars.Contains(op.Type))
                {
                    return true;
                }
            }
            return false;
        }

        // TODO maybe this should be extension of CigarAlignment?
        public static bool CigarMatchesPattern(this BamAlignment alignment, string pattern)
        {
            if (alignment.CigarData.Count != pattern.Length) return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                var op = alignment.CigarData[i];
                if (pattern[i] != op.Type) return false;
            }

            return true;
        }

        public static string StringRepresentation(this BamAlignment alignment)
        {
            return string.Format("{0},{1},{2},{3},{4}", alignment.Name, alignment.RefID, alignment.Position,
                alignment.FragmentLength, alignment.CigarData, alignment.MateRefID, alignment.MatePosition);
        }

        public static List<BamAlignment> GetSupplementaryAlignments(this BamAlignment alignment, Dictionary<string, int> refIdMapping = null)
        {
            var supplementaries = new List<BamAlignment>();

            if (!alignment.HasSupplementaryAlignment()) return supplementaries;

            var tag = alignment.GetStringTag("SA");
            var supplementaryAlignments = tag.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var supplementaryAlignment in supplementaryAlignments)
            {
                // SA tag format: (rname,pos,strand,CIGAR,mapQ,NM ;)+
                var splitTag = supplementaryAlignment.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var tagChromosome = splitTag[0];
                var tagPosition = int.Parse(splitTag[1]);
                var tagStrand = splitTag[2];
                var tagCigar = splitTag[3];
                var tagMapq = splitTag[4];
                var tagNM = splitTag[5];

                var bamAlignment = new BamAlignment()
                {
                    Position = tagPosition - 1, // Deal with the fact that BamAlignment obj is 0-indexed, whereas SA is 1-indexed
                    CigarData = new CigarAlignment(tagCigar),
                };

                if (tagStrand == "-")
                {
                    bamAlignment.SetIsReverseStrand(true);
                }

                if (refIdMapping != null)
                {
                    Console.WriteLine(tagChromosome);
                    bamAlignment.RefID = refIdMapping[tagChromosome];
                }

                supplementaries.Add(bamAlignment);
            }

            return supplementaries;
        }



    }
}
