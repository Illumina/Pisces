using System;
using System.IO;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;

namespace VariantQualityRecalibration
{
    public class EdgeIssueCountData : CountData
    {
        private readonly int _bufferLength = 5;
        private readonly int _testIndex = 2;

        List<CalledAllele> _trailingAlleleBuffer = new List < CalledAllele >  { };

        public EdgeIssueCountData(int extentOfEdgeRegion)
        {
            //ie, if "extentOfEdgeRegion" is 2, we look two bases up and downstream
            //around the variant in question.
            //E - E - V - E - E

            _bufferLength = extentOfEdgeRegion*2 + 1;

            for (int i = 0; i < _bufferLength; i++)
                _trailingAlleleBuffer.Add(null);

            _testIndex = extentOfEdgeRegion;
        }


        public bool Add(CalledAllele nextVariant, string issueLogPath)
        {
           
            UpdateBuffer(nextVariant);

            //Check the buffer, and see if we passed an amplicon edge in our window.
            //If we did, then we are interested in this window. In particular, at the variant in the middle of it.

            if(DidWeDetectAnEdge(_testIndex, _trailingAlleleBuffer))
            {
                var AlleleOfInterest = _trailingAlleleBuffer[_testIndex];  //we keep this as a class, to do the math on
               
                var category = MutationCategoryUtil.GetMutationCategory(AlleleOfInterest.ReferenceAllele, AlleleOfInterest.AlternateAllele);
                NumPossibleVariants++;

                if (category != MutationCategory.Reference)
                {
                    CountsByCategory[category]++;

                    File.AppendAllText(issueLogPath, AlleleOfInterest + Environment.NewLine);


                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Looks to the left and right of the test allele, and see if it is within a stone's thow of an edge.
        /// This method is only public/static to make it easy to test. 
        /// Conventions in this method for handling empty spots in the array buffer:
        /// If the test allele is null - we say no edge
        /// If anything is null to the left or right, but there is a test allele, we say an edge is detected.
        /// </summary>
        /// <returns></returns>
        public static bool DidWeDetectAnEdge(int testIndex, List<CalledAllele> trailingAlleleBuffer)
        {
           var testAllele = trailingAlleleBuffer[testIndex];
           
            if (testAllele == null)
                return false;

            if (testAllele.TotalCoverage == 0)
                return false;

            for (int i = 0; i < trailingAlleleBuffer.Count; i++)
            {
                if (i == testIndex)
                    continue; //skip the test allele

                var bufferAllele = trailingAlleleBuffer[i];

                //look for discontinuities

                //are we at the start or end of the file?
                if (bufferAllele == null)
                    return true;

                //is there a coverage drop?
                if (bufferAllele.TotalCoverage < 0.5 * testAllele.TotalCoverage)
                    return true;

                //did we switch chr?
                if (bufferAllele.Chromosome != testAllele.Chromosome)
                    return true;

                //did we leave one amplicon and start another? the loci should continue to be equal or adjacent
                var distanceBetweenTestAlleleAndIndexAllele = testAllele.ReferencePosition - bufferAllele.ReferencePosition;
                var maxAllowedDistanceBetweenPositions = testIndex - i;
                bool thisIndexInFrontOfTestIndex = (maxAllowedDistanceBetweenPositions > 0);

                if (thisIndexInFrontOfTestIndex)
                {
                    if (distanceBetweenTestAlleleAndIndexAllele > maxAllowedDistanceBetweenPositions)
                        return true;
                }
                else // -2 << -1 will trigger
                {
                    if (distanceBetweenTestAlleleAndIndexAllele < maxAllowedDistanceBetweenPositions)
                        return true;
                }


            }

            return false;
        }

        private void UpdateBuffer(CalledAllele variant)
        {
            for (int i = 1; i < _bufferLength; i++)
            {
                _trailingAlleleBuffer[i - 1] = _trailingAlleleBuffer[i];
            }

             _trailingAlleleBuffer[_bufferLength - 1] = variant;
 
        }
    }
}
