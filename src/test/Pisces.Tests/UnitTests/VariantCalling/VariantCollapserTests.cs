using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pisces.Logic.VariantCalling;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Xunit;

namespace Pisces.Tests.UnitTests.Pisces
{
    public class VariantCollapserTests
    {
        [Fact]
        public void HappyPath_Insertions()
        {
            var testSuite = GetInsertionTestSuite();
            var insertionFullAnchored = testSuite[TestRead.FullAnchored];
            var insertionFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var insertionFullOpenRight = testSuite[TestRead.FullOpenRight];
            var insertionSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var insertionSmallOpenRight = testSuite[TestRead.SmallOpenRight];
            var insertionSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var insertionSmallerOpenRight = testSuite[TestRead.SmallerOpenRight];

            // same insertion (open on left or right, will collapse to fully anchored version)
            ExecuteTest(new List<CandidateAllele> {insertionFullAnchored, insertionFullOpenLeft, insertionFullOpenRight}, 1, 3);

            // same insertion without fully anchored version should also collapse to one at the end
            ExecuteTest(new List<CandidateAllele> { insertionFullOpenLeft, insertionFullOpenRight }, 1, 2);

            // smaller insertion anchored on same side as should collapse
            ExecuteTest(new List<CandidateAllele> { insertionSmallOpenLeft, insertionFullOpenLeft, insertionSmallerOpenLeft }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { insertionSmallOpenLeft, insertionFullAnchored, insertionSmallerOpenLeft }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { insertionSmallOpenRight, insertionFullOpenRight, insertionSmallerOpenRight }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { insertionSmallOpenRight, insertionFullAnchored, insertionSmallerOpenRight }, 1, 3);
        }

        [Fact]
        public void HappyPath_Deletions()
        {
            var testSuite = GetDeletionTestSuite();
            var deletionFullAnchored = testSuite[TestRead.FullAnchored];
            var deletionFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var deletionFullOpenRight = testSuite[TestRead.FullOpenRight];
            var deletionSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var deletionSmallOpenRight = testSuite[TestRead.SmallOpenRight];
            var deletionSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var deletionSmallerOpenRight = testSuite[TestRead.SmallerOpenRight];

            // same deletion (open on left or right, will collapse to fully anchored version)
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionFullOpenLeft, deletionFullOpenRight }, 1, 3);

            // same deletion without fully anchored version should also collapse to one at the end
            ExecuteTest(new List<CandidateAllele> { deletionFullOpenLeft, deletionFullOpenRight }, 1, 2);

            // smaller deletion anchored on same side as should collapse
            ExecuteTest(new List<CandidateAllele> { deletionSmallOpenLeft, deletionFullOpenLeft, deletionSmallerOpenLeft }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { deletionSmallOpenLeft, deletionFullAnchored, deletionSmallerOpenLeft }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { deletionSmallOpenRight, deletionFullOpenRight, deletionSmallerOpenRight }, 1, 3);
            ExecuteTest(new List<CandidateAllele> { deletionSmallOpenRight, deletionFullAnchored, deletionSmallerOpenRight }, 1, 3);
        }

        [Fact]
        public void HappyPath_MNVs()
        {
            var testSuite = GetMnvTestSuite();
            var fullAnchored = testSuite[TestRead.FullAnchored];
            var fullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var fullOpenRight = testSuite[TestRead.FullOpenRight];
            var smallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var smallOpenRight = testSuite[TestRead.SmallOpenRight];
            var smallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var smallerOpenRight = testSuite[TestRead.SmallerOpenRight];
            var snvOpenLeft = testSuite[TestRead.SnvOpenLeft];
            var snvOpenRight = testSuite[TestRead.SnvOpenRight];

            // same (open on left or right, will collapse to fully anchored version)
            ExecuteTest(new List<CandidateAllele> {fullAnchored, fullOpenLeft, fullOpenRight}, 1, 3);

            // same without fully anchored version should also collapse to one at the end
            ExecuteTest(new List<CandidateAllele> {fullOpenLeft, fullOpenRight}, 1, 2);

            // smaller anchored on same side as should collapse
            ExecuteTest(new List<CandidateAllele> {smallOpenLeft, fullOpenLeft, smallerOpenLeft, snvOpenLeft}, 1, 4);
            ExecuteTest(new List<CandidateAllele> {smallOpenLeft, fullAnchored, smallerOpenLeft, snvOpenLeft}, 1, 4);
            ExecuteTest(new List<CandidateAllele> {smallOpenRight, fullOpenRight, smallerOpenRight, snvOpenRight}, 1, 4);
            ExecuteTest(new List<CandidateAllele> {smallOpenRight, fullAnchored, smallerOpenRight, snvOpenRight}, 1, 4);
        }

        [Fact]
        public void PreferFullyAnchored()
        {
            // always prefer fully anchored match, even if there's a bigger one
            var testSuite = GetMnvTestSuite();
            var fullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var snvOpenLeft = testSuite[TestRead.SnvOpenLeft];
            var snvClosed = testSuite[TestRead.SnvClosedLeft];

            ExecuteTest(new List<CandidateAllele> { snvOpenLeft, fullOpenLeft, snvClosed }, 2, candidateAssertions:
                (candidates) =>
                {
                    Assert.True(candidates.Any(c => c.AlternateAllele == snvClosed.AlternateAllele && !c.OpenOnLeft && !c.OpenOnRight && c.Support == 2));
                    Assert.True(candidates.Any(c => c.AlternateAllele == fullOpenLeft.AlternateAllele && c.Support == 1));
                });
        }

        [Fact]
        public void NegativeCases_Insertions()
        {
            var testSuite = GetInsertionTestSuite();
            var insertionFullAnchored = testSuite[TestRead.FullAnchored];
            var insertionFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var insertionFullOpenRight = testSuite[TestRead.FullOpenRight];
            var insertionSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var insertionSmallOpenRight = testSuite[TestRead.SmallOpenRight];
            var insertionSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var insertionSmallerOpenRight = testSuite[TestRead.SmallerOpenRight];

            // baseline
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionSmallOpenLeft }, 1);

            // coordinate does not match
            insertionSmallOpenLeft.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionSmallOpenLeft }, 2);
            insertionSmallOpenLeft.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionSmallOpenLeft }, 2);
            insertionSmallOpenLeft.ReferencePosition --; // reset

            insertionFullOpenRight.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionFullOpenRight }, 2);
            insertionFullOpenRight.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionFullOpenRight }, 2);
            insertionFullOpenRight.ReferencePosition --;  // reset

            // sequence does not match
            insertionFullAnchored.AlternateAllele = "ACGTACGA";
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionFullOpenRight }, 2);
            insertionFullAnchored.AlternateAllele = "ACGTACGT";

            // fully anchored should not collapse
            insertionSmallerOpenRight.OpenOnRight = false;
            ExecuteTest(new List<CandidateAllele> { insertionFullAnchored, insertionSmallerOpenRight }, 2);
            insertionSmallerOpenRight.OpenOnRight = true;

            // wrong anchor point
            insertionSmallerOpenRight.OpenOnRight = false;
            insertionSmallerOpenRight.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele> { insertionFullOpenRight, insertionSmallerOpenRight }, 2);
            insertionSmallerOpenRight.OpenOnRight = true;
            insertionSmallerOpenRight.OpenOnLeft = false;
        }

        [Fact]
        public void NegativeCases_Deletions()
        {
            var testSuite = GetDeletionTestSuite();
            var deletionFullAnchored = testSuite[TestRead.FullAnchored];
            var deletionFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var deletionFullOpenRight = testSuite[TestRead.FullOpenRight];
            var deletionSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var deletionSmallOpenRight = testSuite[TestRead.SmallOpenRight];
            var deletionSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var deletionSmallerOpenRight = testSuite[TestRead.SmallerOpenRight];

            // baseline
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionSmallOpenLeft }, 1);

            // coordinate does not match
            var originalDelSmallOpenLeftCoordinate = deletionSmallOpenLeft.ReferencePosition;
            deletionSmallOpenLeft.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionSmallOpenLeft }, 2);
            deletionSmallOpenLeft.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionSmallOpenLeft }, 2);
            deletionSmallOpenLeft.ReferencePosition = originalDelSmallOpenLeftCoordinate; // reset

            var originalDelFullOpenRightCoordinate = deletionFullOpenRight.ReferencePosition;
            deletionFullOpenRight.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionFullOpenRight }, 2);
            deletionFullOpenRight.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionFullOpenRight }, 2);
            deletionFullOpenRight.ReferencePosition = originalDelFullOpenRightCoordinate; // reset

            // sequence does not match - we don't really care because this should never happen (still collapse)
            var originalDelFullAnchoredRef = deletionFullAnchored.ReferenceAllele;
            deletionFullAnchored.ReferenceAllele = "ACGTACGA";
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionFullOpenRight }, 1);
            deletionFullAnchored.ReferenceAllele = originalDelFullAnchoredRef;

            // fully anchored should not collapse
            deletionSmallerOpenRight.OpenOnRight = false;
            ExecuteTest(new List<CandidateAllele> { deletionFullAnchored, deletionSmallerOpenRight }, 2);
            deletionSmallerOpenRight.OpenOnRight = true;

            // wrong anchor point
            deletionSmallerOpenRight.OpenOnRight = false;
            deletionSmallerOpenRight.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele> { deletionFullOpenRight, deletionSmallerOpenRight }, 2);
            deletionSmallerOpenRight.OpenOnRight = true;
            deletionSmallerOpenRight.OpenOnLeft = false;
        }

        [Fact]
        public void NegativeCases_MNV()
        {
            var testSuite = GetMnvTestSuite();
            var MNVFullAnchored = testSuite[TestRead.FullAnchored];
            var MNVFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var MNVFullOpenRight = testSuite[TestRead.FullOpenRight];
            var MNVSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var MNVSmallOpenRight = testSuite[TestRead.SmallOpenRight];
            var MNVSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var MNVSmallerOpenRight = testSuite[TestRead.SmallerOpenRight];
            var snvOpenLeft = testSuite[TestRead.SnvOpenLeft];
            var snvOpenRight = testSuite[TestRead.SnvOpenRight];

            // baseline
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVSmallOpenLeft }, 1);

            // coordinate does not match
            MNVSmallOpenLeft.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVSmallOpenLeft }, 2);
            MNVSmallOpenLeft.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVSmallOpenLeft }, 2);
            MNVSmallOpenLeft.ReferencePosition++; // reset

            MNVFullOpenRight.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVFullOpenRight }, 2);
            MNVFullOpenRight.ReferencePosition -= 2;
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVFullOpenRight }, 2);
            MNVFullOpenRight.ReferencePosition++;  // reset

            // sequence does not match
            MNVFullAnchored.AlternateAllele = "ACGTACGA";
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVFullOpenRight }, 2);
            MNVFullAnchored.AlternateAllele = "ACGTACGT";

            // fully anchored should not collapse
            MNVSmallerOpenRight.OpenOnRight = false;
            ExecuteTest(new List<CandidateAllele> { MNVFullAnchored, MNVSmallerOpenRight }, 2);
            MNVSmallerOpenRight.OpenOnRight = true;

            // wrong anchor point
            MNVSmallerOpenRight.OpenOnRight = false;
            MNVSmallerOpenRight.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele> { MNVFullOpenRight, MNVSmallerOpenRight }, 2);
            MNVSmallerOpenRight.OpenOnRight = true;
            MNVSmallerOpenRight.OpenOnLeft = false;

            // Inside, fully anchored, left and right anchored.
            var insideOpen = GetBasicMNV();
            insideOpen.OpenOnRight = false;
            insideOpen.OpenOnLeft = false;
            insideOpen.ReferenceAllele = insideOpen.ReferenceAllele.Substring(1, 5);
            insideOpen.AlternateAllele = insideOpen.AlternateAllele.Substring(1, 5);
            insideOpen.ReferencePosition++;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, insideOpen }, 2);
            insideOpen.OpenOnRight = false;
            insideOpen.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, insideOpen }, 2);
            insideOpen.OpenOnRight = true;
            insideOpen.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, insideOpen }, 2);

            // Partial overlap
            var partialLeft = GetBasicMNV();
            partialLeft.OpenOnRight = true;
            partialLeft.OpenOnLeft = false;
            partialLeft.ReferencePosition -= 4;
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, partialLeft }, 2);

            var partialRight = GetBasicMNV();
            partialRight.OpenOnRight = false;
            partialRight.OpenOnLeft = true;
            partialRight.ReferencePosition += 4;
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, partialRight }, 2);

            // SNVs overlapping the ends of the MNV.
            // ACGTACGT
            // A
            //        T
            //
            snvOpenRight.OpenOnRight = false;
            snvOpenRight.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 2); // fully anchored SNV
            snvOpenRight.OpenOnRight = true;
            snvOpenRight.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 1); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 1); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 1); // SNV open right
            snvOpenRight.OpenOnRight = false;
            snvOpenRight.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 2); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 2); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 2); // SNV open left

            snvOpenLeft.OpenOnRight = false;
            snvOpenLeft.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 2); // fully anchored SNV
            snvOpenLeft.OpenOnRight = true;
            snvOpenLeft.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 2); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 2); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 2); // SNV open right
            snvOpenLeft.OpenOnRight = false;
            snvOpenLeft.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 1); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 1); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 1); // SNV open left

            // SNVs 1 below and 1 above the ends of the MNV.
            //  ACGTACGT
            // A
            //          T
            //
            snvOpenRight.ReferencePosition -= 1;
            snvOpenRight.OpenOnRight = false;
            snvOpenRight.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 2); // fully anchored SNV

            snvOpenRight.OpenOnRight = true;
            snvOpenRight.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 2); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 2); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 2); // SNV open right
            snvOpenRight.OpenOnRight = false;
            snvOpenRight.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenRight }, 2); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, 2); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, 2); // SNV open left

            snvOpenLeft.ReferencePosition += 1;
            snvOpenLeft.OpenOnRight = false;
            snvOpenLeft.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 2); // fully anchored SNV
            snvOpenLeft.OpenOnRight = true;
            snvOpenLeft.OpenOnLeft = false;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 2); // SNV open right
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 2); // SNV open right
            snvOpenLeft.OpenOnRight = false;
            snvOpenLeft.OpenOnLeft = true;
            ExecuteTest(new List<CandidateAllele>() { MNVFullAnchored, snvOpenLeft }, 2); // fully anchored SNV
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, 2); // SNV open left
            ExecuteTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenLeft }, 2); // SNV open left
        }

        [Fact]
        public void TestOpennessUpdates()
        {
            // The generally the openness of the variant does not change, but when 
            // SNVs of opposing endedness collapse into another variant the endedness will become anchored.
            var MNVFullOpenLeft = GetBasicMNV();
            MNVFullOpenLeft.OpenOnLeft = true;
            var MNVFullOpenRight = GetBasicMNV();
            MNVFullOpenRight.OpenOnRight = true;
            var snvOpenLeft = new CandidateAllele("chr1", 12, "T", "A", AlleleCategory.Snv);
            snvOpenLeft.OpenOnLeft = true;
            var snvOpenRight = new CandidateAllele("chr1", 5, "T", "A", AlleleCategory.Snv);
            snvOpenRight.OpenOnRight = true;

            // Baseline tests
            ExecuteEndednessTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenLeft }, true, false);
            ExecuteEndednessTest(new List<CandidateAllele>() { MNVFullOpenRight, snvOpenRight }, false, true);

            // Collapse Tests
            ExecuteEndednessTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, false, false);
            ExecuteEndednessTest(new List<CandidateAllele>() { MNVFullOpenLeft, snvOpenRight }, false, false);
        }


        [Fact]
        public void Collapse_IgnoreMNVs()
        {
            var mnv = new CandidateAllele("chr6", 91698264, "AC", "GT", AlleleCategory.Mnv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 3047 }
            };

            var snv = new CandidateAllele("chr6", 91698264, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 16 }
            };

            var snv2 = new CandidateAllele("chr6", 91698264, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = true,
                SupportByDirection = new[] { 30 }
            };

            var candidates = new List<CandidateAllele>();
            candidates.Add(mnv);
            candidates.Add(snv2);
            candidates.Add(snv);

            // Try again refusing to do anything to MNVs
            candidates = new List<CandidateAllele>();
            candidates.Add(snv);
            candidates.Add(snv2);
            candidates.Add(mnv);

            var addedBack = new List<CandidateAllele>();
            var collapser = new VariantCollapser(new List<CandidateAllele>(), true, null, 0, 0);
            var mockSource = GetMockSource(addedBack);

            var result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(3047, result.First(x => x.AlternateAllele == "GT").Support);
            Assert.Equal(16 + 30, result.First(x => x.AlternateAllele == "G").Support);
        }


        [Fact]
        public void Collapse_CandidateOrderIndependent()
        {
            var mnv = new CandidateAllele("chr6", 91698264, "AC", "GT", AlleleCategory.Mnv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 3047 }
            };

            var snv = new CandidateAllele("chr6", 91698264, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 16 }
            };

            var snv2 = new CandidateAllele("chr6", 91698264, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = true,
                SupportByDirection = new[] { 30 }
            };

            var candidates = new List<CandidateAllele>();
            candidates.Add(mnv);
            candidates.Add(snv2);
            candidates.Add(snv);

            var addedBack = new List<CandidateAllele>();
            var collapser = new VariantCollapser(new List<CandidateAllele>(),false, null,0,0);
            var mockSource = GetMockSource(addedBack);

            var result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(3077, result.First(x => x.AlternateAllele == "GT").Support);
            Assert.Equal(16, result.First(x => x.AlternateAllele == "G").Support);


            // Try again with the single-side-open variant coming first.
            candidates = new List<CandidateAllele>();
            candidates.Add(mnv);
            candidates.Add(snv);
            candidates.Add(snv2);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>(),  false, null, 0, 0);
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(3077, result.First(x => x.AlternateAllele == "GT").Support);
            Assert.Equal(16, result.First(x => x.AlternateAllele == "G").Support);


            // Try again with the MNV coming last.
            candidates = new List<CandidateAllele>();
            candidates.Add(snv);
            candidates.Add(snv2);
            candidates.Add(mnv);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>(), false, null, 0, 0);
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(3077, result.First(x => x.AlternateAllele == "GT").Support);
            Assert.Equal(16, result.First(x => x.AlternateAllele == "G").Support);

            // Order of same variant with different endedness - should at least be deterministic
            //chr21:33694224 CGCCAA>GGCCAG ... False/False
            //chr21:33694229 A>G : 1 ... True/False
            //chr21:33694229 A>G : 1 ... False/True

            mnv = new CandidateAllele("chr21", 33694224, "CGCCAA", "GGCCAG", AlleleCategory.Mnv)
            {
                OpenOnLeft = false,
                OpenOnRight = false,
                SupportByDirection = new[] { 64 }
            };

            snv = new CandidateAllele("chr21", 33694229, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 1 }
            };

            snv2 = new CandidateAllele("chr21", 33694229, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = false,
                OpenOnRight = true,
                SupportByDirection = new[] { 1 }
            };

            candidates = new List<CandidateAllele>();
            candidates.Add(mnv);
            candidates.Add(snv);
            candidates.Add(snv2);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>());
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(65, result.First(x => x.AlternateAllele == "GGCCAG").Support);
            Assert.Equal(1, result.First(x => x.AlternateAllele == "G").Support);


            candidates = new List<CandidateAllele>();
            candidates.Add(mnv);
            candidates.Add(snv2);
            candidates.Add(snv);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>());
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(65, result.First(x => x.AlternateAllele == "GGCCAG").Support);
            Assert.Equal(1, result.First(x => x.AlternateAllele == "G").Support);
        }

        [Fact]
        public void NonEquivalentFullyAnchoredShouldNotCollapse()
        {
            //chr21:33694229 A>G : 1 ... False/True
            //chr21:33694229 A>G : 1 ... True/False
            //... Combo of the 2 above results in chr21:33694229 A>G : 2 ... False/False, and now that this is fully anchored this needs to NOT try to collapse to anything that's not identical
            //... Note that we only get to this situation if we start with two unanchored guys that complementarily collapse to each other. If it was fully anchored to begin with, we never would have entered the collapsing logic
            //chr21:33694221 G>G : 27608 ... False/False
            
            var snv1a = new CandidateAllele("chr21", 33694229, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = false,
                OpenOnRight = true,
                SupportByDirection = new[] { 1 }
            };

            var snv1b = new CandidateAllele("chr21", 33694229, "A", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 1 }
            };

            var snv2 = new CandidateAllele("chr21", 33694221, "G", "G", AlleleCategory.Snv)
            {
                OpenOnLeft = false,
                OpenOnRight = false,
                SupportByDirection = new[] { 27608 }
            };

            var candidates = new List<CandidateAllele>();
            candidates.Add(snv1a);
            candidates.Add(snv1b);
            candidates.Add(snv2);

            var addedBack = new List<CandidateAllele>();
            var collapser = new VariantCollapser(new List<CandidateAllele>());
            var mockSource = GetMockSource(addedBack);

            var result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(27608, result.First(x => x.AlternateAllele == "G" && x.ReferenceAllele == "G").Support);
            Assert.Equal(2, result.First(x => x.AlternateAllele == "G" && x.ReferenceAllele == "A").Support);

            var ins1a = new CandidateAllele("chr1", 100, "A", "ATG", AlleleCategory.Insertion)
            {
                OpenOnLeft = false,
                OpenOnRight = true,
                SupportByDirection = new[] { 1 }
            };
            var ins1b = new CandidateAllele("chr1", 100, "A", "ATG", AlleleCategory.Insertion)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 1 }
            };
            var ins2 = new CandidateAllele("chr1", 110, "A", "ATG", AlleleCategory.Insertion)
            {
                OpenOnLeft = false,
                OpenOnRight = false,
                SupportByDirection = new[] { 100 }
            };

            candidates = new List<CandidateAllele>();
            candidates.Add(ins1a);
            candidates.Add(ins1b);
            candidates.Add(ins2);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>());
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(100, result.First(x => x.AlternateAllele == "ATG" && x.ReferencePosition == 110).Support);
            Assert.Equal(2, result.First(x => x.AlternateAllele == "ATG" && x.ReferencePosition == 100).Support);

            var del1a = new CandidateAllele("chr1", 100, "ATG", "A", AlleleCategory.Deletion)
            {
                OpenOnLeft = false,
                OpenOnRight = true,
                SupportByDirection = new[] { 1 }
            };
            var del1b = new CandidateAllele("chr1", 100, "ATG", "A", AlleleCategory.Deletion)
            {
                OpenOnLeft = true,
                OpenOnRight = false,
                SupportByDirection = new[] { 1 }
            };
            var del2 = new CandidateAllele("chr1", 110, "ATG", "A", AlleleCategory.Deletion)
            {
                OpenOnLeft = false,
                OpenOnRight = false,
                SupportByDirection = new[] { 100 }
            };

            candidates = new List<CandidateAllele>();
            candidates.Add(del1a);
            candidates.Add(del1b);
            candidates.Add(del2);

            addedBack = new List<CandidateAllele>();
            collapser = new VariantCollapser(new List<CandidateAllele>());
            mockSource = GetMockSource(addedBack);

            result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c => c.DeepCopy())), mockSource.Object, null);
            Assert.Equal(100, result.First(x => x.AlternateAllele == "A" && x.ReferencePosition == 110).Support);
            Assert.Equal(2, result.First(x => x.AlternateAllele == "A" && x.ReferencePosition == 100).Support);


        }

        [Fact]
        public void Priority()
        {
            Func<List<CandidateAllele>, CandidateAllele> getLarge = list => list.First(c => c.AlternateAllele.EndsWith("ACGT"));
            Func<List<CandidateAllele>, CandidateAllele> getMed = list => list.First(c => c.AlternateAllele.EndsWith("TCGT")); 
            
            // layer on priorities
            // freq < anchored < size < known

            // freq
            // med more frequent
            ExecuteTest(GetPriorityTestSuite(), 2, 
                stage: (c) =>
                {
                    getMed(c).SupportByDirection[0] ++;
                },
                candidateAssertions:
                (c) =>
                {
                    Assert.Equal(3, getMed(c).Support);
                    Assert.Equal(1, getLarge(c).Support);
                });

            // anchored - make other variant more frequent, but prefer anchored
            // med more frequent but large anchored
            ExecuteTest(GetPriorityTestSuite(), 2,
                stage: (c) =>
                {
                    getLarge(c).OpenOnLeft = false;
                    getMed(c).SupportByDirection[0]++;
                },
                candidateAssertions:
                    (c) =>
                    {
                        Assert.Equal(2, getMed(c).Support);
                        Assert.Equal(2, getLarge(c).Support);
                    });

            // size - make other fully anchored and more frequent, but prefer longer
            ExecuteTest(GetPriorityTestSuite(), 2,
                stage: (c) =>
                {
                    var large = getLarge(c);
                    large.SupportByDirection[0] ++;
                    large.AlternateAllele = large.AlternateAllele.Substring(1); // trim off one base to make it smaller
                    large.ReferenceAllele = large.ReferenceAllele.Substring(1);
                    large.OpenOnLeft = false;
                },
                candidateAssertions:
                    (c) =>
                    {
                        Assert.Equal(2, getMed(c).Support);
                        Assert.Equal(2, getLarge(c).Support);
                    });

            // known - make other fully anchored, more frequent and longer, but prefer known
            var testSuite = GetPriorityTestSuite();
            ExecuteTest(testSuite, 2,
                stage: (c) =>
                {
                    var large = getLarge(c);
                    large.SupportByDirection[0]++;
                    large.OpenOnLeft = false;

                    large.AlternateAllele = "A" + large.AlternateAllele;
                    large.ReferenceAllele = "G" + large.ReferenceAllele;
                },
                known: new List<CandidateAllele>() { getMed(testSuite) },
                candidateAssertions:
                (c) =>
                {
                    Assert.Equal(2, getMed(c).Support);
                    Assert.Equal(2, getLarge(c).Support);
                });
        }

        [Fact]
        public void Compare()
        {
            var collapser = new VariantCollapser(null, true);

            // priority should be given to known, fully anchored, longer, more frequent, and left positioned
            var candidateA = GetBasicInsertion();
            var candidateB = GetBasicInsertion();
            candidateA.OpenOnLeft = true;
            candidateB.OpenOnLeft = true;

            Assert.Equal(0, collapser.Compare(candidateA, candidateB));
            Assert.Equal(0, collapser.Compare(candidateB, candidateA));

            candidateA.AlternateAllele = "AAGTACGT";
            Assert.Equal(-1, collapser.Compare(candidateA, candidateB));

            candidateB.ReferencePosition = 4;
            Assert.Equal(1, collapser.Compare(candidateA, candidateB));

            candidateA.Frequency = 0.0000005f;
            candidateB.Frequency = 0.0000004f;
            Assert.Equal(-1, collapser.Compare(candidateA, candidateB));

            candidateB.AlternateAllele = "ACGTACGTT";
            Assert.Equal(1, collapser.Compare(candidateA, candidateB));

            candidateA.OpenOnLeft = false;
            Assert.Equal(-1, collapser.Compare(candidateA, candidateB));

            candidateB.IsKnown = true;
            Assert.Equal(1, collapser.Compare(candidateA, candidateB));
        }

        [Fact]
        public void ReadCounts()
        {
            var candidateA = GetBasicInsertion();
            candidateA.ReadCollapsedCountsMut = new[] {1, 2, 3, 4, 1, 2, 3, 4};
            var candidateB = GetBasicInsertion();
            candidateB.OpenOnLeft = true;
            candidateB.ReadCollapsedCountsMut = new[] { 4, 3, 2, 1, 4, 3, 2, 1 };

            // verify read counts combined
            ExecuteTest(new List<CandidateAllele> { candidateA, candidateB }, 1,
                candidateAssertions:
                    (c) =>
                    {
                        var remainingCandidate = c.First();
                        for (var i = 0; i < remainingCandidate.ReadCollapsedCountsMut.Length; i ++)
                            Assert.Equal(5, remainingCandidate.ReadCollapsedCountsMut[i]);
                    });

        }

        [Fact]
        public void CrossBlock()
        {
            // any variant outside of max position should get removed from candidates and tossed back to source
            // this only really applies to MNVs

            var testSuite = GetMnvTestSuite();
            var fullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var smallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var smallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];
            var snvOpenLeft = testSuite[TestRead.SnvOpenLeft];

            var currentTestSuite = new List<CandidateAllele> {smallOpenLeft, fullOpenLeft, smallerOpenLeft, snvOpenLeft};

            // can collapse across max cleared position
            ExecuteTest(currentTestSuite, 1, 4, 10, addedBackAssertions: (a) => Assert.Equal(0, a.Count));
            ExecuteTest(currentTestSuite, 1, 4, 10, addedBackAssertions: (a) => Assert.Equal(0, a.Count));
            ExecuteTest(currentTestSuite, 1, 4, 10, addedBackAssertions: (a) => Assert.Equal(0, a.Count));
            ExecuteTest(currentTestSuite, 1, 4, 10, addedBackAssertions: (a) => Assert.Equal(0, a.Count));
            
            // can't collapse snv
            snvOpenLeft.AlternateAllele = "G";
            ExecuteTest(currentTestSuite, 1, 3, 10, 
                addedBackAssertions:
                    (a) =>
                    {
                        Assert.Equal(1, a.Count);
                        Assert.True(a.Any(c=> c.Type == AlleleCategory.Snv));
                        // smaller should be collapsed to full, snv passed back because outside of cleared position
                    });

            ExecuteTest(currentTestSuite, 2, null, 12,
                addedBackAssertions:
                    (a) => Assert.Equal(0, a.Count)); // snv gets passed back with candidates because within cleared position

            // snv can collapse to another candidate outside of cleared position
            snvOpenLeft.AlternateAllele = "T";  // set back
            ExecuteTest(currentTestSuite, 1, 1, 6, known: new List<CandidateAllele> {smallOpenLeft},
                addedBackAssertions:
                    (a) =>
                    {
                        Assert.Equal(1, a.Count);

                        var small = a.First(c => c.Type == AlleleCategory.Mnv && c.AlternateAllele == "ACGT");
                        Assert.True(a.Contains(small));
                        Assert.Equal(3, small.Support);
                    });

            // make sure insertions aren't really affected
            testSuite = GetInsertionTestSuite();
            var insertionFullOpenLeft = testSuite[TestRead.FullOpenLeft];
            var insertionSmallOpenLeft = testSuite[TestRead.SmallOpenLeft];
            var insertionSmallerOpenLeft = testSuite[TestRead.SmallerOpenLeft];

            currentTestSuite = new List<CandidateAllele> { insertionFullOpenLeft, insertionSmallOpenLeft, insertionSmallerOpenLeft };

            // test collapsing 3 inserts that do and don't collapse completely with maxClearedPosition at all locations throughout the insertions.

            for (int pos = 5; pos < insertionFullOpenLeft.ReferencePosition + insertionFullOpenLeft.Length + 1; pos++)
            {
                insertionSmallOpenLeft.AlternateAllele = "ACGT";
                ExecuteTest(currentTestSuite, 1, 3, pos, addedBackAssertions: (a) => { Assert.Equal(0, a.Count); });
                
                insertionSmallOpenLeft.AlternateAllele = "ATCC";
                ExecuteTest(currentTestSuite, 2, null, pos, addedBackAssertions: (a) => { Assert.Equal(0, a.Count); });
            }
        }

        private List<CandidateAllele> GetPriorityTestSuite()
        {
            // smaller could potentially align to either first two
            // tests will twiddle with priority
            var fullOpenLeft = GetBasicMNV();
            fullOpenLeft.OpenOnLeft = true;
            var otherOpenLeft = GetBasicMNV();
            otherOpenLeft.OpenOnLeft = true;
            otherOpenLeft.AlternateAllele = "ACGTTCGT";
            var smallerOpenLeft = GetBasicMNV();
            smallerOpenLeft.OpenOnLeft = true;
            smallerOpenLeft.AlternateAllele = smallerOpenLeft.AlternateAllele.Substring(6);
            smallerOpenLeft.ReferenceAllele = smallerOpenLeft.ReferenceAllele.Substring(6);
            smallerOpenLeft.ReferencePosition += 6;

            return new List<CandidateAllele> { fullOpenLeft, otherOpenLeft, smallerOpenLeft };
        }

        private void ExecuteTest(List<CandidateAllele> candidates, int expectedNumAfter, int? expectedSupport = null, int? maxClearedPosition = null, List<CandidateAllele> known = null, 
            Action<List<CandidateAllele>> candidateAssertions = null, Action<List<CandidateAllele>> stage = null, Action<List<CandidateAllele>> addedBackAssertions = null)
        {
            var addedBack = new List<CandidateAllele>();

            ResetSupport(candidates);

            var collapser = new VariantCollapser(known, false, null, 0, 0);
            var mockSource = GetMockSource(addedBack);

            if (stage != null)
                stage(candidates);

            var result = collapser.Collapse(new List<CandidateAllele>(candidates.Select(c=> c.DeepCopy())), mockSource.Object, maxClearedPosition);

            Assert.Equal(expectedNumAfter, result.Count);
            if (expectedSupport.HasValue)
                Assert.Equal(expectedSupport, result[0].Support);
            if (candidateAssertions != null)
                candidateAssertions(result);
            if (addedBackAssertions != null)
                addedBackAssertions(addedBack);

            // order of candidates shouldnt matter
            addedBack.Clear();
            ResetSupport(candidates);

            if (stage != null)
                stage(candidates);

            var reverse = new List<CandidateAllele>(candidates.Select(c => c.DeepCopy()));
            reverse.Reverse();
            result = collapser.Collapse(reverse, mockSource.Object, maxClearedPosition);
            Assert.Equal(expectedNumAfter, result.Count);
            if (expectedSupport.HasValue)
                Assert.Equal(expectedSupport, result[0].Support);
            if (candidateAssertions != null)
                candidateAssertions(result);
            if (addedBackAssertions != null)
                addedBackAssertions(addedBack);
        }

        private void ResetSupport(List<CandidateAllele> candidates)
        {
            foreach (var candidate in candidates)
            {
                candidate.SupportByDirection = new[] {1, 0, 0};
            }
        }

        private CandidateAllele GetBasicInsertion()
        {
            return new CandidateAllele("chr1", 5, "A", "ACGTACGT", AlleleCategory.Insertion) { SupportByDirection = new [] { 1, 0, 0 }};
        }

        private CandidateAllele GetBasicDeletion()
        {
            return new CandidateAllele("chr1", 5, "ACGTACGT", "A", AlleleCategory.Deletion) { SupportByDirection = new[] { 1, 0, 0 } };
        }

        private CandidateAllele GetBasicMNV()
        {
            return new CandidateAllele("chr1", 5, "TGCATGCA", "ACGTACGT", AlleleCategory.Mnv) { SupportByDirection = new [] { 1, 0, 0 }};
        }

        private Mock<IAlleleSource> GetMockSource(List<CandidateAllele> candidatesAdded)
        {
            var source = new Mock<IAlleleSource>();

            if (candidatesAdded != null)
                source.Setup(s => s.AddCandidates(It.IsAny<IEnumerable<CandidateAllele>>()))
                    .Callback((IEnumerable<CandidateAllele> c) => candidatesAdded.AddRange(c));
            source.Setup(s => s.GetAlleleCount(It.IsAny<int>(), It.IsAny<AlleleType>(), It.IsAny<DirectionType>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(1);

            return source;
        }

        private enum TestRead
        {
            FullAnchored,
            FullOpenLeft,
            FullOpenRight,
            SmallOpenLeft,
            SmallOpenRight,
            SmallerOpenLeft,
            SmallerOpenRight, 
            SnvOpenLeft,
            SnvOpenRight,
            SnvClosedLeft
        }

        private Dictionary<TestRead, CandidateAllele> GetInsertionTestSuite()
        {
            var insertionFullAnchored = GetBasicInsertion();
            var insertionFullOpenLeft = GetBasicInsertion();
            insertionFullOpenLeft.OpenOnLeft = true;
            var insertionFullOpenRight = GetBasicInsertion();
            insertionFullOpenRight.OpenOnRight = true;
            var insertionSmallOpenRight = GetBasicInsertion();
            insertionSmallOpenRight.OpenOnRight = true;
            insertionSmallOpenRight.AlternateAllele = insertionSmallOpenRight.AlternateAllele.Substring(0, 4);
            var insertionSmallOpenLeft = GetBasicInsertion();
            insertionSmallOpenLeft.OpenOnLeft = true;
            insertionSmallOpenLeft.AlternateAllele = insertionSmallOpenLeft.AlternateAllele.Substring(4);
            var insertionSmallerOpenRight = GetBasicInsertion();
            insertionSmallerOpenRight.OpenOnRight = true;
            insertionSmallerOpenRight.AlternateAllele = insertionSmallerOpenRight.AlternateAllele.Substring(0, 2);
            var insertionSmallerOpenLeft = GetBasicInsertion();
            insertionSmallerOpenLeft.OpenOnLeft = true;
            insertionSmallerOpenLeft.AlternateAllele = insertionSmallerOpenLeft.AlternateAllele.Substring(6);

            var lookup = new Dictionary<TestRead, CandidateAllele>();

            lookup.Add(TestRead.FullAnchored, insertionFullAnchored);
            lookup.Add(TestRead.FullOpenLeft, insertionFullOpenLeft);
            lookup.Add(TestRead.FullOpenRight, insertionFullOpenRight);
            lookup.Add(TestRead.SmallOpenLeft, insertionSmallOpenLeft);
            lookup.Add(TestRead.SmallOpenRight, insertionSmallOpenRight);
            lookup.Add(TestRead.SmallerOpenLeft, insertionSmallerOpenLeft);
            lookup.Add(TestRead.SmallerOpenRight, insertionSmallerOpenRight);

            return lookup;
        }

        private Dictionary<TestRead, CandidateAllele> GetDeletionTestSuite()
        {
            var deletionFullAnchored = GetBasicDeletion();
            var deletionFullOpenLeft = GetBasicDeletion();
            deletionFullOpenLeft.OpenOnLeft = true;
            var deletionFullOpenRight = GetBasicDeletion();
            deletionFullOpenRight.OpenOnRight = true;
            var deletionSmallOpenRight = GetBasicDeletion();
            deletionSmallOpenRight.OpenOnRight = true;
            deletionSmallOpenRight.ReferenceAllele = deletionSmallOpenRight.ReferenceAllele.Substring(0, 4);
            var deletionSmallOpenLeft = GetBasicDeletion();
            deletionSmallOpenLeft.OpenOnLeft = true;
            deletionSmallOpenLeft.ReferencePosition = deletionSmallOpenLeft.ReferencePosition + 4;
            deletionSmallOpenLeft.ReferenceAllele = deletionSmallOpenLeft.ReferenceAllele.Substring(4);
            deletionSmallOpenLeft.AlternateAllele = deletionSmallOpenLeft.ReferenceAllele.Substring(0,1);
            var deletionSmallerOpenRight = GetBasicDeletion();
            deletionSmallerOpenRight.OpenOnRight = true;
            deletionSmallerOpenRight.ReferenceAllele = deletionSmallerOpenRight.ReferenceAllele.Substring(0, 2);
            var deletionSmallerOpenLeft = GetBasicDeletion();
            deletionSmallerOpenLeft.OpenOnLeft = true;
            deletionSmallerOpenLeft.ReferencePosition = deletionSmallerOpenLeft.ReferencePosition + 6;
            deletionSmallerOpenLeft.ReferenceAllele = deletionSmallerOpenLeft.ReferenceAllele.Substring(6);
            deletionSmallerOpenLeft.AlternateAllele = deletionSmallerOpenLeft.ReferenceAllele.Substring(0,1);

            var lookup = new Dictionary<TestRead, CandidateAllele>();

            lookup.Add(TestRead.FullAnchored, deletionFullAnchored);
            lookup.Add(TestRead.FullOpenLeft, deletionFullOpenLeft);
            lookup.Add(TestRead.FullOpenRight, deletionFullOpenRight);
            lookup.Add(TestRead.SmallOpenLeft, deletionSmallOpenLeft);
            lookup.Add(TestRead.SmallOpenRight, deletionSmallOpenRight);
            lookup.Add(TestRead.SmallerOpenLeft, deletionSmallerOpenLeft);
            lookup.Add(TestRead.SmallerOpenRight, deletionSmallerOpenRight);

            return lookup;
        }

        private Dictionary<TestRead, CandidateAllele> GetMnvTestSuite()
        {
            var fullAnchored = GetBasicMNV();
            var fullOpenLeft = GetBasicMNV();
            fullOpenLeft.OpenOnLeft = true;
            var fullOpenRight = GetBasicMNV();
            fullOpenRight.OpenOnRight = true;
            var smallOpenRight = GetBasicMNV();
            smallOpenRight.OpenOnRight = true;
            smallOpenRight.ReferenceAllele = smallOpenRight.ReferenceAllele.Substring(0, 4);
            smallOpenRight.AlternateAllele = smallOpenRight.AlternateAllele.Substring(0, 4);
            var smallOpenLeft = GetBasicMNV();
            smallOpenLeft.OpenOnLeft = true;
            smallOpenLeft.ReferenceAllele = smallOpenLeft.ReferenceAllele.Substring(4);
            smallOpenLeft.AlternateAllele = smallOpenLeft.AlternateAllele.Substring(4);
            smallOpenLeft.ReferencePosition += 4;
            var smallerOpenRight = GetBasicMNV();
            smallerOpenRight.OpenOnRight = true;
            smallerOpenRight.ReferenceAllele = smallerOpenRight.ReferenceAllele.Substring(0, 2);
            smallerOpenRight.AlternateAllele = smallerOpenRight.AlternateAllele.Substring(0, 2);
            var smallerOpenLeft = GetBasicMNV();
            smallerOpenLeft.OpenOnLeft = true;
            smallerOpenLeft.ReferenceAllele = smallerOpenLeft.ReferenceAllele.Substring(6);
            smallerOpenLeft.AlternateAllele = smallerOpenLeft.AlternateAllele.Substring(6);
            smallerOpenLeft.ReferencePosition += 6;
            var snvOpenRight = new CandidateAllele("chr1", 5, "T", "A", AlleleCategory.Snv) { SupportByDirection = new[] { 1, 0, 0 } };
            snvOpenRight.OpenOnRight = true;
            var snvOpenLeft = new CandidateAllele("chr1", 12, "A", "T", AlleleCategory.Snv) { SupportByDirection = new[] { 1, 0, 0 } };
            snvOpenLeft.OpenOnLeft = true;

            var snvClosedLeft = new CandidateAllele("chr1", 12, "A", "T", AlleleCategory.Snv) { SupportByDirection = new[] { 1, 0, 0 } };

            var lookup = new Dictionary<TestRead, CandidateAllele>();

            lookup.Add(TestRead.FullAnchored, fullAnchored);
            lookup.Add(TestRead.FullOpenLeft, fullOpenLeft);
            lookup.Add(TestRead.FullOpenRight, fullOpenRight);
            lookup.Add(TestRead.SmallOpenLeft, smallOpenLeft);
            lookup.Add(TestRead.SmallOpenRight, smallOpenRight);
            lookup.Add(TestRead.SmallerOpenLeft, smallerOpenLeft);
            lookup.Add(TestRead.SmallerOpenRight, smallerOpenRight);
            lookup.Add(TestRead.SnvOpenLeft, snvOpenLeft);
            lookup.Add(TestRead.SnvOpenRight, snvOpenRight);
            lookup.Add(TestRead.SnvClosedLeft, snvClosedLeft);

            return lookup;
        }

        private void ExecuteEndednessTest(List<CandidateAllele> candidates, bool expectOpenOnLeft, bool expectOpenOnRight)
        {
            var collapser = new VariantCollapser(null, false, null, 0, 0);
            var mockSource = GetMockSource(null);

            var forward = candidates.Select(candidate => candidate.DeepCopy()).ToList();

            var result = collapser.Collapse(forward, mockSource.Object, null);
            Assert.Equal(result[0].OpenOnLeft, expectOpenOnLeft);
            Assert.Equal(result[0].OpenOnRight, expectOpenOnRight);
        }
    }
}
