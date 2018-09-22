using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Alignment.Domain.Sequencing;
using StitchingLogic.Models;
using TestUtilities;
using Xunit;

namespace StitchingLogic.Tests
{
    public class OverlapEvaluatorTests
    {
        [Fact]
        public void TestIsRepeat()
        {
            // mononucleotide repeats
            Assert.True(OverlapEvaluator.IsRepeat("AAAAAAA"));
            Assert.False(OverlapEvaluator.IsRepeat("TAAAAA"));
            Assert.False(OverlapEvaluator.IsRepeat("AAATAA"));
            Assert.False(OverlapEvaluator.IsRepeat("AAAAAT"));

            // dinucleotide repeats
            Assert.True(OverlapEvaluator.IsRepeat("ATATAT"));
            Assert.True(OverlapEvaluator.IsRepeat("ATATATA"));
            Assert.False(OverlapEvaluator.IsRepeat("AATATATA"));
            Assert.False(OverlapEvaluator.IsRepeat("ATATAATA"));

            // trinucleotide repeats
            Assert.True(OverlapEvaluator.IsRepeat("ATGATGATG"));    // 3x ATG
            Assert.True(OverlapEvaluator.IsRepeat("ATGATGATGA"));   // 3x ATG + A
            Assert.True(OverlapEvaluator.IsRepeat("ATGATGATGAT"));  // 3x ATG + AT
            Assert.False(OverlapEvaluator.IsRepeat("ATGATGATGAG")); // 3x ATG + AG
            Assert.False(OverlapEvaluator.IsRepeat("ATGATGATGTG")); // 3x ATG + TG

            Assert.True(OverlapEvaluator.IsRepeat("AA"));
            Assert.False(OverlapEvaluator.IsRepeat("ATC"));
            Assert.False(OverlapEvaluator.IsRepeat("AT"));
            Assert.False(OverlapEvaluator.IsRepeat("A"));
            Assert.True(OverlapEvaluator.IsRepeat("ATA"));
        }

        [Fact]
        public void TestSlidingWindow()
        {
            // Short overlap, min windowsize
            var units = OverlapEvaluator.SlideSequence("ATA", 1); // Will survey 3-1 = Up to index 2 positions to get [A,T,A] = [A,T]
            Assert.Equal(2, units.Count());

            // Short overlap, max windowsize
            var units2 = OverlapEvaluator.SlideSequence("ATAG", 3); // Will survey 4-3 = Up to index 1 positions to get [ATA, TAG]
            Assert.Equal(2, units2.Count());

            // Long overlap, min windowsize
            var units3 = OverlapEvaluator.SlideSequence("ATTTACGCAGTAGACAGATAAAAA", 1); // [A,T,T] = [A,T]
            Assert.Equal(2, units3.Count());

            // Long overlap, max windowsize
            var units4 = OverlapEvaluator.SlideSequence("ATGATGATGATGATGATGATGATG", 3); // [ATG,TGA,GAT]
            Assert.Equal(3, units4.Count());

            // Windowsize above maximum allowed
            Assert.Throws<ArgumentException>(()=>OverlapEvaluator.SlideSequence("ATGATGATGATGATGATGATGATG", 4));
        }

        [Fact]
        public void TryStitchHomopolymer()
        {
            // Simple test, test indexing accuracy, shouldn't stitch
            //          GTTTCCCAGCATGCAGTAAAAAAAAAAAAAA
            //                           AAAAAAAAAAAAAAGCATGACGGAATTGACAG
            var readA1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTAAAAAAAAAAAAAA", 100, new CigarAlignment("31M"), null, 117, 30, false, 30);
            var readA2 = ReadTestHelper.CreateRead("chr1", "AAAAAAAAAAAAAAGCATGACGGAATTGACAG", 117, new CigarAlignment("32M"), null, 100, 30, false, 30);
            var mergedReadA = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTAAAAAAAAAAAAAAGCATGACGGAATTGACAG", 100, new CigarAlignment("49M"), null);
            //mergedReadA.CigarDirections = new CigarDirection("16F13S17R");
            TestHomoPolymerScenario(readA1, readA2, mergedReadA, false, false);

            // Simple test, test indexing accuracy, should stitch
            //          GTTTCCCAGCATGCAGTAAAAAAAAAAAAAAG
            //                           AAAAAAAAAAAAAAGCATGACGGAATTGACAG
            var readB1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTAAAAAAAAAAAAAAG", 100, new CigarAlignment("32M"), null, 117, 30, false, 30);
            var readB2 = ReadTestHelper.CreateRead("chr1", "AAAAAAAAAAAAAAGCATGACGGAATTGACAG", 117, new CigarAlignment("32M"), null, 100, 30, false, 30);
            var mergedReadB = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTAAAAAAAAAAAAAAGCATGACGGAATTGACAG", 100, new CigarAlignment("49M"), null);
            TestHomoPolymerScenario(readB1, readB2, mergedReadB, false, true);

            //// Same start location, different softclipping weirdness, shouldn't stitch
            ////          gttttacAAAAAAAAAGGTGCAGATCAGGTT
            ////          GTTTTACAAAAAAAAAggtgcagatcaggtt
            var readC1 = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 107, new CigarAlignment("7S24M"), null, 100, 30, false, 30);
            var readC2 = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 100, new CigarAlignment("16M15S"), null, 107, 30, false, 30);
            var mergedReadC = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 100, new CigarAlignment("31M"), null);
            TestHomoPolymerScenario(readC1, readC2, mergedReadC, false, false);

            //// Same as C, but less soft clipping so anchor present in overlap, should stitch
            ////          gttttaCAAAAAAAAAGGTGCAGATCAGGTT
            ////          GTTTTACAAAAAAAAAggtgcagatcaggtt
            var readD1 = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 106, new CigarAlignment("6S25M"), null, 100, 30, false, 30);
            var readD2 = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 100, new CigarAlignment("16M15S"), null, 106, 30, false, 30);
            var mergedReadD = ReadTestHelper.CreateRead("chr1", "GTTTTACAAAAAAAAAGGTGCAGATCAGGTT", 100, new CigarAlignment("31M"), null);
            TestHomoPolymerScenario(readD1, readD2, mergedReadD, false, true);

            //// Dinucleotide repeats (AT x 7); should not stitch
            ////          GTTTCCCAGCATGCAGTATATATATATATAT
            ////                           ATATATATATATATGCATGACGGAATTGACAG
            var readE1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATATATATAT", 100, new CigarAlignment("31M"), null, 117, 30, false, 30);
            var readE2 = ReadTestHelper.CreateRead("chr1", "ATATATATATATATGCATGACGGAATTGACAG", 117, new CigarAlignment("32M"), null, 100, 30, false, 30);
            var mergedReadE = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATATATATATGCATGACGGAATTGACAG", 100, new CigarAlignment("49M"), null);
            TestHomoPolymerScenario(readE1, readE2, mergedReadE, false, false);
            
            //// Dinucleotide repeat incomplete; should not stitch (AT x 6 + A) 
            ////          GTTTCCCAGCATGCAGTATATATATATATA
            ////                           ATATATATATATAGCATGACGGAATTGACAG
            var readF1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATATATATA", 100, new CigarAlignment("30M"), null, 117, 30, false, 30);
            var readF2 = ReadTestHelper.CreateRead("chr1", "ATATATATATATAGCATGACGGAATTGACAG", 117, new CigarAlignment("31M"), null, 100, 30, false, 30);
            var mergedReadF = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATATATATAGCATGACGGAATTGACAG", 100, new CigarAlignment("48M"), null);
            TestHomoPolymerScenario(readF1, readF2, mergedReadF, false, false);

            //// Overlap only 3bp long, should stitch
            ////          GTTTCCCAGCATGCAGTATA
            ////                           ATATATGACGGAATTGACAG
            var readG1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATA", 100, new CigarAlignment("20M"), null, 117, 30, false, 30);
            var readG2 = ReadTestHelper.CreateRead("chr1", "ATATATGACGGAATTGACAG", 117, new CigarAlignment("20M"), null, 100, 30, false, 30);
            var mergedReadG = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATGACGGAATTGACAG", 100, new CigarAlignment("37M"), null);
            TestHomoPolymerScenario(readG1, readG2, mergedReadG, false, true);

            //// Overlap 4bp long, should not stitch (AT repeat)
            ////          GTTTCCCAGCATGCAGTATAT
            ////                           ATATATGACGGAATTGACAG
            var readH1 = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATAT", 100, new CigarAlignment("21M"), null, 117, 30, false, 30);
            var readH2 = ReadTestHelper.CreateRead("chr1", "ATATATGACGGAATTGACAG", 117, new CigarAlignment("20M"), null, 100, 30, false, 30);
            var mergedReadH = ReadTestHelper.CreateRead("chr1", "GTTTCCCAGCATGCAGTATATATGACGGAATTGACAG", 100, new CigarAlignment("37M"), null);
            TestHomoPolymerScenario(readH1, readH2, mergedReadH, false, false);
        }

        public void TestHomoPolymerScenario(Read read1, Read read2, Read mergedRead, bool useSoftClippedBases, bool expectedResult)
        {
            var alignmentSet = new AlignmentSet(read1, read2);
            var basicStitcher = new BasicStitcher(10, useSoftclippedBases: useSoftClippedBases);
            Assert.Equal(expectedResult, basicStitcher.TryStitch(alignmentSet));
            //Assert.Equal(expectedResult, OverlapEvaluator.BridgeAnchored(mergedRead));
        }

    }
}
