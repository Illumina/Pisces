using System;
using System.Collections.Generic;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Types;

namespace Pisces.Domain.Models.Alleles
{
    public class CalledAllele : BaseAllele
    {
        public Genotype Genotype { get; set; }

        // quality metrics
        public int GenotypeQscore { get; set; }
        public int VariantQscore { get; set; }
        public float FractionNoCalls { get; set; }
        public List<FilterType> Filters { get; set; }
        public StrandBiasResults StrandBiasResults { get; set; }

        public int NoiseLevelApplied { get; set; }

        // coverage & freq metrics
        public int TotalCoverage { get; set; }
		public double SumOfBaseQuality { get; set; }
		//for extended variants, coverage is not an exact value. 
		//Its an estimate based on the depth over the length of the variant.
		//In particular, the depth by direction does not always allocate neatly to an integer value.
		public int[] EstimatedCoverageByDirection { get; set; }

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

        public int ReferenceSupport { get; set; }

        public float RefFrequency
        {
            get { return TotalCoverage == 0 ? 0f : Math.Min((float)ReferenceSupport / TotalCoverage, 1); }
        }


        public CalledAllele()
        {
            Filters = new List<FilterType>();
            EstimatedCoverageByDirection = new int[Constants.NumDirectionTypes];
            StrandBiasResults = new StrandBiasResults();
            SupportByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCounts = new int[Constants.NumReadCollapsedTypes];
            Genotype = Genotype.HomozygousRef;
            Type = AlleleCategory.Reference;
        }

        public CalledAllele(AlleleCategory type)
        {
            Filters = new List<FilterType>();
            EstimatedCoverageByDirection = new int[Constants.NumDirectionTypes];
            StrandBiasResults = new StrandBiasResults();
            SupportByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCounts = new int[Constants.NumReadCollapsedTypes];
            Genotype = Genotype.HeterozygousAltRef;
            Type = type;
            if (Type == AlleleCategory.Reference)
                Genotype = Genotype.HomozygousRef;
        }

        public bool IsSameAllele(CalledAllele otherAllele)
        {
            return ((otherAllele.Chromosome == Chromosome)
                && (otherAllele.Coordinate == Coordinate)
                && (otherAllele.Alternate == Alternate)
                && (otherAllele.Reference == Reference));
        }

        public static CalledAllele DeepCopy(CalledAllele originalAllele)
        {
            if (originalAllele == null)
                return null;

            var allele = new CalledAllele
            {
                Chromosome = originalAllele.Chromosome,
                Coordinate = originalAllele.Coordinate,
                Reference = originalAllele.Reference,
                Alternate = originalAllele.Alternate,
                Genotype = originalAllele.Genotype,
                GenotypeQscore = originalAllele.GenotypeQscore,
                VariantQscore = originalAllele.VariantQscore,
                FractionNoCalls = originalAllele.FractionNoCalls,
                NoiseLevelApplied = originalAllele.NoiseLevelApplied,
                TotalCoverage = originalAllele.TotalCoverage,
                AlleleSupport = originalAllele.AlleleSupport,
                ReferenceSupport = originalAllele.ReferenceSupport,
                Type = originalAllele.Type,
                SupportByDirection = new int[Constants.NumDirectionTypes],
                EstimatedCoverageByDirection = new int[Constants.NumDirectionTypes],
                ReadCollapsedCounts = new int[Constants.NumReadCollapsedTypes],
                StrandBiasResults = StrandBiasResults.DeepCopy(originalAllele.StrandBiasResults),
                Filters = new List<FilterType>()
            };

            foreach (var filter in originalAllele.Filters)
                allele.Filters.Add(filter);

            for (int i = 0; i < Constants.NumDirectionTypes; i++)
            {
                allele.SupportByDirection[i] = originalAllele.SupportByDirection[i];
                allele.EstimatedCoverageByDirection[i] = originalAllele.EstimatedCoverageByDirection[i];
                allele.ReadCollapsedCounts[i] = originalAllele.ReadCollapsedCounts[i];
               
            }
            return allele;
        }

    }
}