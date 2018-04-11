using System.IO;
using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Common.IO.Utility;
using Xunit;

namespace TestUtilities
{
    public static class TestHelper
    {
        [Fact]
        public static void ClearMutex()
        {
            Logger.CloseLog();
        }

        public static void VerifyArray<T>(T[] array1, T[] array2)
        {
            if (array1 == null || array2 == null)
                Assert.Equal(array1, array2);
            else
            {
                Assert.Equal(array1.Length, array2.Length);
                for (var i = 0; i < array1.Length; i++)
                    Assert.Equal(array1[i], array2[i]);
            }
        }

        public static CalledAllele CreateDummyAllele(
          string chrom, int position, string refAllele, string altAllele, int depth, int altCalls)
        {
            return new CalledAllele(Pisces.Domain.Types.AlleleCategory.Snv)
            {
                Chromosome = chrom,
                ReferencePosition = position,
                ReferenceAllele = refAllele,
                AlternateAllele = altAllele,
                TotalCoverage = depth,
                AlleleSupport = altCalls,
                Type = Pisces.Domain.Types.AlleleCategory.Snv,
                ReferenceSupport = depth - altCalls,
                VariantQscore = 100
            };
        }

   

        public static VcfVariant CreateDummyVariant(
            string chrom, int position, string refAllele, string altAllele, int depth, int altCalls)
        {
            return new VcfVariant()
            {
                ReferenceName = chrom,
                ReferencePosition = position,
                ReferenceAllele = refAllele,
                VariantAlleles = new[] { altAllele },
                GenotypeTagOrder = new[] { "GT", "GQ", "AD", "DP", "VF", "NL", "SB", "NC" },
                InfoTagOrder = new[] { "DP" },
                Genotypes = new List<Dictionary<string, string>>()
                {
                    new Dictionary<string, string>()
                    {
                        {"GT", "0/1"},
                        {"GQ", "100"},
                        {"AD", (depth-altCalls).ToString() +"," + altCalls.ToString()},//"6830,156"
                        {"DP", depth.ToString()},
                        { "VF", "0.156" },//"0.05"},
                        {"NL", "20"},
                        {"SB", "-100.0000"},
                        {"NC","0.0100"}
                    }
                },
                InfoFields = new Dictionary<string, string>() { { "DP", depth.ToString() } }, //
                Filters = "PASS",
                Identifier = ".",
            };
        }


        public static void SetQualities(AlignmentSet set, int quality)
        {
            for (var i = 0; i < set.PartnerRead1.Qualities.Length; i++)
                set.PartnerRead1.Qualities[i] = (byte)quality;

            if (set.PartnerRead2 != null)
                for (var i = 0; i < set.PartnerRead2.Qualities.Length; i++)
                    set.PartnerRead2.Qualities[i] = (byte)quality;
        }

        public static void SetQualities(IEnumerable<Read> reads, int quality)
        {
            foreach (var read in reads)
            {
                for (var i = 0; i < read.Qualities.Length; i++)
                    read.Qualities[i] = (byte)quality;
            }
        }


        public static List<Read> CreateTestReads(Read read1, int quality = 30)
        {
            var reads = new List<Read>() {read1};
            SetQualities(reads, quality);

            return reads;
        }

        public static List<Read> CreateTestReads(Read read1, Read read2, int quality = 30)
        {
            var reads = new List<Read>() { read1, read2 };
            SetQualities(reads, quality);

            return reads;
        }

        public static List<Read> CreateTestReads(Read read1, int quality1, 
                                                 Read read2,  int quality2)
        {
            var reads = new List<Read>() { read1, read2 };
            SetQualities(new List<Read>() { read1 }, quality1);
            SetQualities(new List<Read>() { read2 }, quality2);
            return reads;
        }

        public static CigarAlignment GetReadCigarFromStitched(string stitchedCigar, int readLength, bool reverse)
        {
            var cigar = new CigarAlignment(stitchedCigar);
            if (reverse)
                cigar.Reverse();

            var totalLengthSofar = 0;
            var newCigar = new CigarAlignment();

            for (var i = 0; i < cigar.Count; i++)
            {
                var operation = cigar[i];
                if (operation.IsReadSpan())
                {
                    if (totalLengthSofar + operation.Length > readLength)
                    {
                        newCigar.Add(new CigarOp(operation.Type, (uint)(readLength - totalLengthSofar)));
                        break;
                    }

                    newCigar.Add(operation);
                    totalLengthSofar += (int)operation.Length;

                    if (totalLengthSofar == readLength)
                        break;
                }
                else
                {
                    newCigar.Add(operation);
                }
            }

            if (reverse)
                newCigar.Reverse();

            return newCigar;
        }



        public static CalledAllele CreatePassingVariant(bool isReference)
        {
            var calledAllele = isReference ? new CalledAllele() :
                new CalledAllele(AlleleCategory.Snv);

            calledAllele.ReferencePosition = 1;
            calledAllele.AlternateAllele = "C";
            calledAllele.ReferenceAllele = "A";
            calledAllele.AlleleSupport = isReference ? 490 : 10;
            calledAllele.TotalCoverage = 490;
            calledAllele.NumNoCalls = 10;
            calledAllele.StrandBiasResults = new BiasResults() { BiasAcceptable = true };
            calledAllele.VariantQscore = 30;

            return calledAllele;
        }

        public static void CompareFiles(string testfile, string expectedFile)
        {
            Assert.True(File.Exists(testfile), "Looking for: " + testfile);

            var observedLines = File.ReadAllLines(testfile);
            var expectedLines = File.ReadAllLines(expectedFile);
            Assert.Equal(expectedLines.Length, observedLines.Length);

            for (int i = 0; i < expectedLines.Length; i++)
            {
                 //Note, skip anything that contains a version number, test path, or date. These are expected to vary.
                if (!expectedLines[i].ToLower().Contains("filedate")
                    && !expectedLines[i].ToLower().Contains("cmdline")
                    && !expectedLines[i].ToLower().Contains("psara"))
                    Assert.Equal(expectedLines[i], observedLines[i]);
            }
        }
    }
}