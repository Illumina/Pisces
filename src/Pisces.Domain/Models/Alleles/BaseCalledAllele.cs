using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class BaseCalledAllele : BaseAllele
    {
        public Genotype Genotype { get; set; }

        // quality metrics
        public int GenotypeQscore { get; set; }
        public int VariantQscore { get; set; }
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
            ReadCollapsedCounts = new int[Constants.NumReadCollapsedTypes];
        }

    }
}