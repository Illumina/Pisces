using System;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using Xunit;

namespace VariantQualityRecalibration.Tests
{
    public class EdgeIssueCountDataTests
    {
        [Fact]
        public void DidWeDetectAnEdge()
        {

            int bufferLength = 7;
            int testAlleleIndex = 3;
            List<CalledAllele> trailingAlleleBuffer = new List<CalledAllele> { };

            for (int i = 0; i < bufferLength; i++)
            {
                trailingAlleleBuffer.Add(TestUtilities.TestHelper.CreateDummyAllele("chr1", 10 + i, "A", "G", 100, 10));
            }

            //no edge
            Assert.False(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //a dip next to the test allele 
            //Ie, the test allele is higher than something adjacent to it, so its a guestimate
            //that it might be on or near an amplicon edge
            trailingAlleleBuffer[2].TotalCoverage = 2;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            // the test allele is on a bump
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 400;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 100;

            //a bulge next to the test allele
            //Ie, the test allele is lower than something adjacent to it,so its a guestimate
            //that it might be below and not on the amplicon edge
            trailingAlleleBuffer[2].TotalCoverage = 400;
            Assert.False(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //a chr change
            trailingAlleleBuffer[2].TotalCoverage = 100;
            trailingAlleleBuffer[2].Chromosome = "chrM";
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));
            trailingAlleleBuffer[2].Chromosome = "chr1"; //put back change

            //some variants have the same loci, and there is no edge
            trailingAlleleBuffer[0].ReferencePosition = 12;
            trailingAlleleBuffer[1].ReferencePosition = 12;
            trailingAlleleBuffer[2].ReferencePosition = 12;
            Assert.False(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //some variants have the same loci, and there is an edge
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 400;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //all variants have the same loci, and there is an edge
            trailingAlleleBuffer[testAlleleIndex].ReferencePosition = 12;
            trailingAlleleBuffer[4].ReferencePosition = 12;
            trailingAlleleBuffer[5].ReferencePosition = 12;
            trailingAlleleBuffer[6].ReferencePosition = 12;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //all variants have the same loci, and there is no edge
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 100;
            Assert.False(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //there is a loci hole (wierd, but possible). Thats an edge.
            trailingAlleleBuffer[4].ReferencePosition = 14;
            trailingAlleleBuffer[5].ReferencePosition = 14;
            trailingAlleleBuffer[6].ReferencePosition = 14;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));


            //edge-drop at the left
            trailingAlleleBuffer[4].ReferencePosition = 13;
            trailingAlleleBuffer[5].ReferencePosition = 14;
            trailingAlleleBuffer[6].ReferencePosition = 15;
            trailingAlleleBuffer[0].TotalCoverage = 10;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //edge-drop at the right
            trailingAlleleBuffer[0].TotalCoverage = 100;
            trailingAlleleBuffer[6].TotalCoverage = 10;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

            //the test variant IS the high spot, and its lower to both sides
            trailingAlleleBuffer[6].TotalCoverage = 100;
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 400;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));
            trailingAlleleBuffer[testAlleleIndex].TotalCoverage = 100;

            //an empty spot in the buffer
            trailingAlleleBuffer[6] = null;
            Assert.True(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));
           
            //the testAllele is null
            trailingAlleleBuffer[6] = trailingAlleleBuffer[2];
            trailingAlleleBuffer[testAlleleIndex] = null;
            Assert.False(EdgeIssueCountData.DidWeDetectAnEdge(testAlleleIndex, trailingAlleleBuffer));

        }
    }
}