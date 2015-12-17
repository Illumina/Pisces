using System;
using System.Collections.Generic;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic.Calculators;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Models.Alleles
{
    public class BaseCalledAllele : IAllele
    {
        public string Chromosome { get; set; }
        public int Coordinate { get; set; }
        public string Reference { get; set; }
        public string Alternate { get; set; }

        public AlleleCategory Type { get; set; }
        public Genotype Genotype { get; set; }

        // quality metrics
        public int Qscore { get; set; }
        public float FractionNoCalls { get; set; }
        public List<FilterType> Filters { get; set; }
        public StrandBiasResults StrandBiasResults { get; set; }

        // coverage & freq metrics
        public int TotalCoverage { get; set; }
        public int[] TotalCoverageByDirection { get; set; }

        public int[] SupportByDirection { get; set; }
        public int AlleleSupport { get; set; }
        public int NumNoCalls { get; set; }

        public float Frequency
        {
            get { return TotalCoverage == 0 ? 0f : Math.Min((float)AlleleSupport / TotalCoverage, 1); }
        }

        public void AddFilter(FilterType filter)
        {
            if (!Filters.Contains(filter)) Filters.Add(filter);    
        }

        public BaseCalledAllele()
        {
            Filters = new List<FilterType>();
            TotalCoverageByDirection = new int[Constants.NumDirectionTypes];
            StrandBiasResults = new StrandBiasResults();
            SupportByDirection = new int[Constants.NumDirectionTypes];
        }

    }
}