using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.Hosting;
using System.Text.RegularExpressions;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using SequencingFiles;
using Xunit;

namespace CallSomaticVariants.Tests.Utilities
{
    public static class TestHelper
    {
        public static void SetQualities(AlignmentSet set, int quality)
        {
            for (var i = 0; i < set.PartnerRead1.Qualities.Length; i++)
                set.PartnerRead1.Qualities[i] = (byte)quality;

            if (set.PartnerRead2 != null)
                for (var i = 0; i < set.PartnerRead2.Qualities.Length; i++)
                    set.PartnerRead2.Qualities[i] = (byte)quality;
        }

        public static AlignmentSet CreateTestSet(Read read1, int quality = 30)
        {
            var alignmentSet = new AlignmentSet(read1, null, true);
            SetQualities(alignmentSet, quality);

            return alignmentSet;
        }

        public static AlignmentSet CreateTestSet(Read read1, Read read2, int quality = 30)
        {
            var alignmentSet = new AlignmentSet(read1, read2, true);
            SetQualities(alignmentSet, quality);

            return alignmentSet;
        }

        // Utility function to test the format of Vcf files.
        public static void VcfFileFormatValidation(string inputFile, int? expectedCandidateCount = null)
        {
            int observedCandidateCount = 0;
            // Time to read the header
            string[] testFile = File.ReadAllLines(inputFile);

            Assert.True(testFile.Length >= 9);
            bool ff = false,
                fd = false,
                so = false,
                csvc = false,
                rf = false,
                info = false,
                filter = false,
                format = false,
                contig = false;
            foreach (var x in testFile)
            {
                switch (x.Split('=')[0])
                {
                    case "##fileformat":
                        Assert.True(Regex.IsMatch(x, "^##fileformat=VCFv4\\.1"));
                        ff = true;
                        break;
                    case "##fileDate":
                        Assert.True(Regex.IsMatch(x, "^##fileDate="));
                        CultureInfo enUS = new CultureInfo("en-US");
                        DateTime dateValue;
                        Assert.NotNull(DateTime.TryParseExact(x.Split('=')[1],
                            "YYYYMMDD", enUS, DateTimeStyles.None, out dateValue));
                        fd = true;
                        break;
                    case "##source":
                        Assert.True(Regex.IsMatch(x, "^##source=\\S+\\W\\d.\\d.\\d.\\d"));
                        so = true;
                        break;
                    case "##CallSomaticVariants_cmdline":
                        Assert.True(Regex.IsMatch(x, "^##CallSomaticVariants_cmdline=.+"));
                        csvc = true;
                        break;
                    case "##reference":
                        Assert.True(Regex.IsMatch(x, "^##reference="));
                        rf = true;
                        break;
                    case "##INFO":
                        Assert.True(Regex.IsMatch(x, "^##INFO=.+>$"));
                        info = true;
                        break;
                    case "##FILTER":
                        Assert.True(Regex.IsMatch(x, "^##FILTER=.+>$"));
                        filter = true;
                        break;
                    case "##FORMAT":
                        Assert.True(Regex.IsMatch(x, "^##FORMAT=<.+>"));
                        format = true;
                        break;
                    case "##contig":
                        Assert.True(Regex.IsMatch(x, "^##contig=<ID=\\S+,length=\\d+>"));
                        contig = true;
                        break;
                    default:
                        if (Regex.IsMatch(x.Split('\t')[0], "^chr\\d+"))
                        {
                            observedCandidateCount++;
                            break;
                        }

                        if (Regex.IsMatch(x.Split('\t')[0], "^#CHROM"))
                            break;

                        Assert.True(false, "Unrecognized section.");
                        break;
                }
            }

            // Ensure the correct number of candidates are listed.
            if (expectedCandidateCount.HasValue) 
                Assert.Equal(expectedCandidateCount, observedCandidateCount);

            Assert.True(ff && fd && so && csvc && rf && info && filter && format && contig,
                "Missing a section of the header");
        }

        public static Read CreateRead(string chr, string sequence, int position, 
            CigarAlignment cigar = null, byte[] qualities = null, int matePosition = 0, byte qualityForAll = 30)
        {
            return new Read(chr,
                new BamAlignment
                {
                    Bases = sequence,
                    Position = position - 1,
                    CigarData = cigar ?? new CigarAlignment(sequence.Length + "M"),
                    Qualities = qualities ?? Enumerable.Repeat(qualityForAll, sequence.Length).ToArray(),
                    MatePosition = matePosition - 1
                });
        }

        public static void CompareReads(Read read1, Read read2)
        {
            Assert.Equal(read1.Chromosome, read2.Chromosome);
            Assert.Equal(read1.Sequence, read2.Sequence);
            Assert.Equal(read1.Position, read2.Position);
            Assert.Equal(read1.Name, read2.Name);
            Assert.Equal(read1.MatePosition, read2.MatePosition);
            Assert.Equal(read1.IsMapped, read2.IsMapped);
            Assert.Equal(read1.IsPcrDuplicate, read2.IsPcrDuplicate);
            Assert.Equal(read1.IsPrimaryAlignment, read2.IsPrimaryAlignment);
            Assert.Equal(read1.IsProperPair, read2.IsProperPair);
            Assert.Equal(read1.MapQuality, read2.MapQuality);
            Assert.Equal(read1.StitchedCigar == null ? null: read1.StitchedCigar.ToString(),
                read2.StitchedCigar == null ? null : read2.StitchedCigar.ToString());
            Assert.Equal(read1.CigarData == null ? null : read1.CigarData.ToString(),
                            read2.CigarData == null ? null : read2.CigarData.ToString());

            VerifyArray(read1.PositionMap, read2.PositionMap);
            VerifyArray(read1.DirectionMap, read2.DirectionMap);
            VerifyArray(read1.Qualities, read2.Qualities);
        }

        public static void VerifyArray<T>(T[] array1, T[] array2)
        {
            if (array1 == null || array2 == null)
                Assert.Equal(array1, array2);
            else
            {
                Assert.Equal(array1.Length, array2.Length);
                for (var i = 0; i < array1.Length; i ++)
                    Assert.Equal(array1[i], array2[i]);
            }
        }
    }
}
