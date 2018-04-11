using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.IO.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Utility;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;

namespace Psara
{
    public class GeometricFilter
    {

        Dictionary<string, List<Region>> _regionsByChr = new Dictionary<string, List<Region>>();
        GeometricFilterParameters.InclusionModel _mode = GeometricFilterParameters.InclusionModel.ByStartPosition;
        public string _currentChr = "";
        public ChrIntervalSet _currentChrIntervalSet;
       
        public GeometricFilter(GeometricFilterParameters parameters)
        {
            IntervalFileToRegion.ParseIntervalFile(parameters.RegionOfInterestPath, _regionsByChr);
            _mode = parameters.InclusionStrategy;
        }

        public List<CalledAllele> DoFiltering(List<CalledAllele> alleles)
        {
            var result = new List<CalledAllele>();

            if (alleles.Count == 0)
                return result;

            var chrName = alleles[0].Chromosome;



            if (chrName != _currentChr)
            {
                if (_regionsByChr.ContainsKey(chrName))
                {
                    _currentChrIntervalSet = new ChrIntervalSet(_regionsByChr[chrName], chrName);
                }
                else
                {
                    _currentChrIntervalSet = new ChrIntervalSet(new List<Region>() { }, chrName);
                    return result;
                }

            }


            if (_currentChrIntervalSet.Intervals.Count == 0)
                return result;


            switch (_mode)
            {
                case GeometricFilterParameters.InclusionModel.ByOverlap:
                    {
                        throw new ArgumentException("Option GeometricFilterParameters.InclusionModel.ByOverlap not currently supported.");                    
                    }

                case GeometricFilterParameters.InclusionModel.Expanded:
                    {
                        result = DoFilteringByExpandingRegion(alleles, _currentChrIntervalSet);
                        break;
                    }

                default:
                    {
                        result = DoFilteringByStartPosition(alleles, _currentChrIntervalSet);
                        break;
                    }

            }

            return result;
        }


        public List<CalledAllele> DoFilteringByStartPosition(List<CalledAllele> alleles, ChrIntervalSet chrIntervalSet)
        {
            //these should all be co-located alleles
            var testAllele = alleles[0];
            var result = new List<CalledAllele> { };

            if (chrIntervalSet.ContainsPosition(testAllele.ReferencePosition))
            {
                result = alleles;
            }

            chrIntervalSet.SetCleared(testAllele.ReferencePosition);

            return result;
        }

        public List<CalledAllele> DoFilteringByExpandingRegion(List<CalledAllele> alleles, ChrIntervalSet chrIntervalSet)
        {
            //these should all be co-located alleles
            var emptyResult = new List<CalledAllele> { };
            var testAllele = alleles[0];


            if (chrIntervalSet.ContainsPosition(testAllele.ReferencePosition))
            {
                chrIntervalSet.SetCleared(testAllele.ReferencePosition);
                return alleles;
            }
            else //we already know the start positons are NOT in the interval. Now check the rest of the bases.
            {
                bool expandInterval = false;

                foreach (var allele in alleles)
                {
                    int startPosPlusOne = allele.ReferencePosition +1;
                    int endPos = allele.ReferencePosition + allele.ReferenceAllele.Length - 1;
                   
                    for (int internalPosition = startPosPlusOne; internalPosition <= endPos; internalPosition++)
                    {
                        if (chrIntervalSet.ContainsPosition(internalPosition))
                        {
                            chrIntervalSet.ExpandInterval(internalPosition, testAllele.ReferencePosition);
                            expandInterval = true;
                            break;
                        }
                    }
                }

                chrIntervalSet.SetCleared(testAllele.ReferencePosition);

                if (expandInterval)
                    return alleles;
                else
                    return emptyResult;
            }

        }
    }
}
