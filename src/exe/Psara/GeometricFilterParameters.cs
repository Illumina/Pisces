using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.NDesk.Options;
using CommandLine.IO;

namespace Psara
{
    public class GeometricFilterParameters
    {
        public string RegionOfInterestPath = "";
        public InclusionModel InclusionStrategy = InclusionModel.ByStartPosition;
        public ExclusionModel ExclusionStrategy = ExclusionModel.Prune;

        public string FilterTag = "OffTarget";

        public enum InclusionModel  //how to include alleles at the boundary
        {
            ByStartPosition, //hard cutoff at the loci of interest. Only (and ALL) variants that start inside the region of interest will be reported 
            ByOverlap,       //Greedy cutoff. All variants that overlap (including start or end points in the interval) are considered in the region of interest
            Expanded         //we use the start postion method, but automatically back it up to include all variants that will overlap the start of the region of interest. 
        };


        public enum ExclusionModel //once we decided an allele is to be EXCLUDED, do we add a new filter tag to it, or prune it?
        {
            Filter, //not currently supported
            Prune
        };


        public void Validate()
        {
            if ((RegionOfInterestPath != null) && !(File.Exists(RegionOfInterestPath)))
            {
                throw new ArgumentException(string.Format("Region of Interest path does not exist. {0}", RegionOfInterestPath));
            }

        }

    }


    public class GeometricFilterParsingMethods
    {

        public static Dictionary<string, OptionSet> GetParsingMethods(GeometricFilterParameters options)
        {
            var requiredOps = new OptionSet
            {
                {
                    "ROI=",
                    OptionTypes.STRING + " Full path for region of interest file.",
                    value => options.RegionOfInterestPath = value
                },
            };
            var commonOps = new OptionSet
            {

                {
                    "InclusionModel=",
                    OptionTypes.STRING + " How to determine if variants are in the region of interest. Use 'start' for by variant start position, and 'expand' to automatically expand the reporting region to include any variants which overlap the input ROI.",
                    value=> options.InclusionStrategy = ConvertToInclusionModel(value)
                },
               
            };

            var optionDict = new Dictionary<string, OptionSet>
            {
                { "REQUIRED",requiredOps},
                {"COMMON",commonOps },

            };




            return optionDict;
        }


        private static GeometricFilterParameters.InclusionModel ConvertToInclusionModel(string value)
        {
            switch (value.ToLower())
            {
                case "start":
                    return (GeometricFilterParameters.InclusionModel.ByStartPosition);
                case "overlap":
                    throw new ArgumentException(string.Format("Overlap option not yet supported"));
                case "expand":
                    return (GeometricFilterParameters.InclusionModel.Expanded);
                default:
                    throw new ArgumentException(string.Format("Unable to understand desired inclusion model {0}", value));

            }

        }
    }
}
