using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using TestUtilities;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.Logic
{
    public interface ISupportDirectionTestSetup<T,U,V>
    {
        U GetAlignmentWithCoverageDirections(int numForward, int numStitched, int numReverse, int deletionLength, bool isDuplex = false);
        T GetSupportDirection(V variant, U alignment);
    }

    public class PiscesSupportDirectionTestSetup :  ISupportDirectionTestSetup<DirectionType,Read,CandidateAllele>
    {
        public const int VariantStartInRead = 3;

        public Read GetAlignmentWithCoverageDirections(int numForward, int numStitched, int numReverse, int deletionLength, bool isDuplex = false)
        {
            var read = ReadTestHelper.CreateRead("chr1", String.Concat(Enumerable.Repeat("A", numForward + numReverse + numStitched - deletionLength)), 1);
            read.SequencedBaseDirectionMap = GetCoverageDirections(numForward, numStitched, numReverse, deletionLength);
            read.IsDuplex = isDuplex;
            return read;
        }

        public static DirectionType[] GetCoverageDirections(int numForward, int numStitched, int numReverse, int deletionLength)
        {
            var readCoverageDirections = new List<DirectionType>();

            var forwardPortion = Enumerable.Repeat(DirectionType.Forward, numForward);
            var stitchedPortion = Enumerable.Repeat(DirectionType.Stitched, numStitched);
            var reversePortion = Enumerable.Repeat(DirectionType.Reverse, numReverse);

            readCoverageDirections.AddRange(forwardPortion);
            readCoverageDirections.AddRange(stitchedPortion);
            readCoverageDirections.AddRange(reversePortion);

            readCoverageDirections.RemoveRange(VariantStartInRead, deletionLength);
            return readCoverageDirections.ToArray();
        }

        public DirectionType GetSupportDirection(CandidateAllele variant, Read read)
        {
            return CandidateVariantFinder.GetSupportDirection(variant, read, VariantStartInRead);
        }
    }

    public class SupportDirectionSuite<T, U, V>
    {
        private SupportDirectionScenarios<T, U, V> supportDirectionScenarios;
        private T Stitched;
        private T Reverse;
        private T Forward;

        public SupportDirectionSuite(ISupportDirectionTestSetup<T,U,V> testSetup, T forward, T reverse, T stitched)
        {
            Forward = forward;
            Reverse = reverse;
            Stitched = stitched;
            supportDirectionScenarios = new SupportDirectionScenarios<T, U, V>(testSetup);
        }

        public void RunMnvScenarios(V variant)
        {
            //Variant starts at first position of stitched region and ends within stitched region - should be stitched
            // 0 0 0 2 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts at first position of stitched region and ends on edge of stitched region - should be stitched
            // 0 0 0 2 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts at first position of stitched region and ends after end of stitched region - should be stitched
            // 0 0 0 2 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends within stitched region - should be stitched
            // 0 0 2 2 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends on edge of stitched region - should be stitched
            // 0 0 2 2 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends after end of stitched region - should be stitched
            // 0 0 2 2 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends within stitched region - should be stitched
            // 0 0 0 0 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends on far edge of stitched region - should be stitched
            // 0 0 0 0 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsOnFarEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends after end of stitched region - should be stitched
            // 0 0 0 0 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends right before stitched region - should be forward
            // 0 0 0 0 0 0 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsRightBeforeStitchedRegion(variant, Forward);

            //Variant starts before stitched region and ends at first position of stitched region - should be stitched
            // 0 0 0 0 0 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAtFirstPositionOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends well before stitched region - should be forward
            // 0 0 0 0 0 0 0 2 2 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWellBeforeStitchedRegion(variant, Forward);

            //Variant starts right after stitched region and ends after stitched region - should be reverse
            // 0 2 2 1 1 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsRightAfterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse);

            //Variant starts well after stitched region and ends after stitched region - should be reverse
            // 0 2 1 1 1 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWellAFterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse);

        }
        public void RunInsertionScenarios(V variant)
        {
            //For a variant in the VCF that would look like:
            // X -> XATC
            // Where X is whatever the base is at index 2 of the read

            //Variant starts at first position of stitched region and ends within stitched region - should be stitched
            // 0 0 0 2 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts at first position of stitched region and ends on edge of stitched region - should be stitched
            // 0 0 0 2 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts at first position of stitched region and ends after end of stitched region - should be stitched
            // 0 0 0 2 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends within stitched region - should be stitched
            // 0 0 2 2 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends on edge of stitched region - should be stitched
            // 0 0 2 2 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts within stitched region and ends after end of stitched region - should be stitched
            // 0 0 2 2 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends within stitched region - should be stitched
            // 0 0 0 0 2 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWithinStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends on far edge of stitched region - should be stitched
            // 0 0 0 0 2 2 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsOnFarEdgeOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends after end of stitched region - should be stitched
            // 0 0 0 0 2 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends right before stitched region - should be forward
            // 0 0 0 0 0 0 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsRightBeforeStitchedRegion(variant, Forward);

            //Variant starts before stitched region and ends at first position of stitched region - should be stitched
            // 0 0 0 0 0 2 2 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAtFirstPositionOfStitchedRegion(variant, Stitched);

            //Variant starts before stitched region and ends well before stitched region - should be forward
            // 0 0 0 0 0 0 0 2 2 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWellBeforeStitchedRegion(variant, Forward);

            //Variant starts right after stitched region and ends after stitched region - should be reverse
            // 0 2 2 1 1 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsRightAfterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse);

            //Variant starts well after stitched region and ends after stitched region - should be reverse
            // 0 2 1 1 1 1 1 1 1 1     CoverageDirection
            // N N N A T C N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            supportDirectionScenarios.VariantStartsWellAFterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse);

        }
        public void RunDeletionScenarios(V variant, int deletionLength)
        {
            //Variant starts at first position of stitched region and ends within stitched region - should be forward
            // 0 0 0 2 2 2 2 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsWithinStitchedRegion(variant, Forward, deletionLength);

            //Variant starts at first position of stitched region and ends on edge of stitched region - should be forward, though I don't really see why, looks like it has same support from both sides..
            // 0 0 0 2 2 2 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Forward, deletionLength);

            //Variant starts at first position of stitched region and ends after end of stitched region - should be forward in current
            // 0 0 0 2 2 1 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsAtFirstPositionOfStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Forward, deletionLength);

            //Variant starts within stitched region and ends within stitched region - should be stitched
            // 0 0 2 2 2 2 2 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsWithinStitchedRegion(variant, Stitched, deletionLength);

            //Variant starts within stitched region and ends on edge of stitched region - should be reverse
            // 0 0 2 2 2 2 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsOnEdgeOfStitchedRegion(variant, Reverse, deletionLength);

            //Variant starts within stitched region and ends after end of stitched region - should be reverse
            // 0 0 2 2 2 1 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsWithinStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse, deletionLength);

            //Variant starts before stitched region and ends within stitched region - should be forward
            // 0 0 0 0 2 2 2 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWithinStitchedRegion(variant, Forward, deletionLength);

            //Variant starts before stitched region and ends on far edge of stitched region - should be forward
            // 0 0 0 0 2 2 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsOnFarEdgeOfStitchedRegion(variant, Forward, deletionLength);

            //Variant starts before stitched region and ends after end of stitched region - should be forward
            // 0 0 0 0 2 1 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Forward, deletionLength);

            //Variant starts before stitched region and ends right before stitched region - should be forward
            // 0 0 0 0 0 0 2 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsRightBeforeStitchedRegion(variant, Forward, deletionLength);

            //Variant starts before stitched region and ends at first position of stitched region - should be forward
            // 0 0 0 0 0 2 2 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsAtFirstPositionOfStitchedRegion(variant, Forward, deletionLength);

            //Variant starts before stitched region and ends well before stitched region - should be forward
            // 0 0 0 0 0 0 0 2 2 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsBeforeStitchedRegion_EndsWellBeforeStitchedRegion(variant, Forward, deletionLength);


            //Variant starts right after stitched region and ends after stitched region - should be reverse
            // 0 2 2 1 1 1 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsRightAfterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse, deletionLength);

            //Variant starts well after stitched region and ends after stitched region - should be reverse
            // 0 2 1 1 1 1 1 1 1 1     CoverageDirection
            // N N N D D D N N N N     Variant Position
            // 0 1 2 - - - 3 4 5 6     Index In Read
            supportDirectionScenarios.VariantStartsWellAFterStitchedRegion_EndsAfterEndOfStitchedRegion(variant, Reverse, deletionLength);

        }
    }

    public class SupportDirectionScenarios<T,U,V>
    {
        public ISupportDirectionTestSetup<T,U,V> TestSetup;
        public SupportDirectionScenarios(ISupportDirectionTestSetup<T,U,V> testSetup)
        {
            TestSetup = testSetup;
        }

        public void VariantStartsAtFirstPositionOfStitchedRegion_EndsWithinStitchedRegion(V variant, T expectedDirection, int deletionLength = 0)
        {
            //Variant starts at first position of stitched region and ends within stitched region
            // 0 0 0 2 2 2 2 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(3, 4, 3, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }

        public void VariantStartsAtFirstPositionOfStitchedRegion_EndsOnEdgeOfStitchedRegion(V variant, T expectedDirection, int deletionLength = 0)
        {

            //Variant starts at first position of stitched region and ends on edge of stitched region
            // 0 0 0 2 2 2 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(3, 3, 4, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsAtFirstPositionOfStitchedRegion_EndsAfterEndOfStitchedRegion(V variant, T expectedDirection, int deletionLength = 0)
        {
            //Variant starts at first position of stitched region and ends after end of stitched region
            // 0 0 0 2 2 1 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(3, 2, 5, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsWithinStitchedRegion_EndsWithinStitchedRegion(V variant, T expectedDirection, int deletionLength = 0)
        {
            //Variant starts within stitched region and ends within stitched region
            // 0 0 2 2 2 2 2 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(2,5,3, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsWithinStitchedRegion_EndsOnEdgeOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts within stitched region and ends on edge of stitched region
            // 0 0 2 2 2 2 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(2,4,4, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsWithinStitchedRegion_EndsAfterEndOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts within stitched region and ends after end of stitched region
            // 0 0 2 2 2 1 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(2,3,5, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));   
        }

        public void VariantStartsBeforeStitchedRegion_EndsWithinStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts before stitched region and ends within stitched region
            // 0 0 0 0 2 2 2 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(4,3,3, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsBeforeStitchedRegion_EndsOnFarEdgeOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts before stitched region and ends on far edge of stitched region
            // 0 0 0 0 2 2 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(4,2,4,deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant, alignment));
        }

        public void VariantStartsBeforeStitchedRegion_EndsAfterEndOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts before stitched region and ends after end of stitched region
            // 0 0 0 0 2 1 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(4, 1, 5, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }

        public void VariantStartsBeforeStitchedRegion_EndsRightBeforeStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts before stitched region and ends right before stitched region
            // 0 0 0 0 0 0 2 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(6, 1, 3, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));   
        }

        public void VariantStartsBeforeStitchedRegion_EndsAtFirstPositionOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts before stitched region and ends at first position of stitched region
            // 0 0 0 0 0 2 2 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(5, 2, 3, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }

        public void VariantStartsBeforeStitchedRegion_EndsWellBeforeStitchedRegion(V variant, T expectedDirection, int deletionLength =0)
        {
            //Variant starts before stitched region and ends well before stitched region
            // 0 0 0 0 0 0 0 2 2 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(7, 2, 1, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }

        public void VariantStartsRightAfterStitchedRegion_EndsAfterEndOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts right after stitched region and ends after stitched region
            // 0 2 2 1 1 1 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(1, 2, 7, deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }

        public void VariantStartsWellAFterStitchedRegion_EndsAfterEndOfStitchedRegion(V variant, T expectedDirection, int deletionLength=0)
        {
            //Variant starts well after stitched region and ends after stitched region
            // 0 2 1 1 1 1 1 1 1 1     CoverageDirection
            // N N N V V V N N N N     Variant Position
            // 0 1 2 3 4 5 6 7 8 9     Index In Read
            var alignment = TestSetup.GetAlignmentWithCoverageDirections(1,1,8,deletionLength);
            Assert.Equal(expectedDirection, TestSetup.GetSupportDirection(variant,alignment));
        }
    }
}