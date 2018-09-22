using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
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

        public int PhaseSetIndex = -1;

        public List<FilterType> Filters { get; set; }
        public BiasResults StrandBiasResults { get; set; }
        //public BiasResults PoolBiasResults { get; set; }

        public int NoiseLevelApplied { get; set; }

        // coverage & freq metrics
        public int TotalCoverage { get; set; }
		public double SumOfBaseQuality { get; set; }
		//for extended variants, coverage is not an exact value. 
		//Its an estimate based on the depth over the length of the variant.
		//In particular, the depth by direction does not always allocate neatly to an integer value.
		public int[] EstimatedCoverageByDirection { get; set; }
        // collapsed counts - optionally used
        public int[] ReadCollapsedCountTotal { get; set; }
        public int[] SupportByDirection { get; set; }
        public int[] WellAnchoredSupportByDirection { get; set; }
        public int AlleleSupport { get; set; }
        public int NumNoCalls { get; set; }
        public float FractionNoCalls { get; set; }
		public bool IsForcedToReport { get; set; }
        public int ConfidentCoverageStart { get; set; }
        public int SuspiciousCoverageStart { get; set; }
        public int ConfidentCoverageEnd { get; set; }
        public int SuspiciousCoverageEnd { get; set; }
        public int WellAnchoredSupport { get; set; }
        public double UnanchoredCoverageWeight { get; set; }

        public float Frequency
        {
            get { return TotalCoverage == 0 ? 0f : Math.Min((float)AlleleSupport / TotalCoverage, 1); }
        }

        public bool IsNocall
        {
            get {
                return (Genotype == Genotype.Alt12LikeNoCall ||
                  Genotype == Genotype.AltLikeNoCall ||
                   Genotype == Genotype.HemizygousNoCall ||
                   Genotype == Genotype.RefLikeNoCall);
            }
        }

        public bool IsRefType
        {
            get
            {
                return (Type== AlleleCategory.Reference);
            }
        }

        public bool HasARefAllele
        {
            get
            {
                return (Genotype == Genotype.RefAndNoCall ||
                  Genotype == Genotype.HomozygousRef ||
                  Genotype == Genotype.HemizygousRef ||
                   Genotype == Genotype.HeterozygousAltRef);
            }
        }

        public bool HasAnAltAllele
        {
            get
            {
                return (Genotype == Genotype.AltAndNoCall ||
                  Genotype == Genotype.HomozygousAlt ||
                     Genotype == Genotype.HeterozygousAlt1Alt2 ||
                   Genotype == Genotype.HeterozygousAltRef);
            }
        }

        public void ForceFractionNoCalls(float myFractionNoCalls)
        {
            FractionNoCalls = myFractionNoCalls;
            NumNoCalls = (int)(FractionNoCalls * TotalCoverage / (1f - FractionNoCalls));
        }
        public void SetFractionNoCalls()
        {
            var allReads = (float)(TotalCoverage + NumNoCalls);
            if (allReads == 0)
                FractionNoCalls = 0;
            else
                FractionNoCalls = (NumNoCalls / allReads);
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
            StrandBiasResults = new BiasResults();
            SupportByDirection = new int[Constants.NumDirectionTypes];
            WellAnchoredSupportByDirection= new int[Constants.NumDirectionTypes];
            ReadCollapsedCountsMut = new int[Constants.NumReadCollapsedTypes];
            ReadCollapsedCountTotal = new int[Constants.NumReadCollapsedTypes];
            Genotype = Genotype.HomozygousRef;
            Type = AlleleCategory.Reference;
        }

        public CalledAllele(AlleleCategory type)
        {
            Filters = new List<FilterType>();
            EstimatedCoverageByDirection = new int[Constants.NumDirectionTypes];
            StrandBiasResults = new BiasResults();
            SupportByDirection = new int[Constants.NumDirectionTypes];
            WellAnchoredSupportByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCountsMut = new int[Constants.NumReadCollapsedTypes];
            ReadCollapsedCountTotal = new int[Constants.NumReadCollapsedTypes];
            Genotype = Genotype.HeterozygousAltRef;
            Type = type;
            if (Type == AlleleCategory.Reference)
                Genotype = Genotype.HomozygousRef;
        }

        public CalledAllele(CalledAllele originalAllele)
        {
            Chromosome = originalAllele.Chromosome;
            ReferencePosition = originalAllele.ReferencePosition;
            ReferenceAllele = originalAllele.ReferenceAllele;
            AlternateAllele = originalAllele.AlternateAllele;
            Genotype = originalAllele.Genotype;
            GenotypeQscore = originalAllele.GenotypeQscore;
            VariantQscore = originalAllele.VariantQscore;
            NumNoCalls = originalAllele.NumNoCalls;
            NoiseLevelApplied = originalAllele.NoiseLevelApplied;
            TotalCoverage = originalAllele.TotalCoverage;
            AlleleSupport = originalAllele.AlleleSupport;
            WellAnchoredSupport = originalAllele.WellAnchoredSupport;
            ReferenceSupport = originalAllele.ReferenceSupport;
            Type = originalAllele.Type;
            SupportByDirection = new int[Constants.NumDirectionTypes];
            WellAnchoredSupportByDirection = new int[Constants.NumDirectionTypes];
            EstimatedCoverageByDirection = new int[Constants.NumDirectionTypes];
            ReadCollapsedCountsMut = new int[Constants.NumReadCollapsedTypes];
            ReadCollapsedCountTotal = new int[Constants.NumReadCollapsedTypes];
            StrandBiasResults = BiasResults.DeepCopy(originalAllele.StrandBiasResults);
            UnanchoredCoverageWeight = originalAllele.UnanchoredCoverageWeight;
            ConfidentCoverageStart = originalAllele.ConfidentCoverageStart;
            ConfidentCoverageEnd = originalAllele.ConfidentCoverageEnd;
            SuspiciousCoverageStart = originalAllele.SuspiciousCoverageStart;
            SuspiciousCoverageEnd = originalAllele.SuspiciousCoverageEnd;

            Filters = new List<FilterType>();
            
            foreach (var filter in originalAllele.Filters)
                Filters.Add(filter);

            for (int i = 0; i < Constants.NumDirectionTypes; i++)
            {
                SupportByDirection[i] = originalAllele.SupportByDirection[i];
                WellAnchoredSupportByDirection[i] = originalAllele.WellAnchoredSupportByDirection[i];
                EstimatedCoverageByDirection[i] = originalAllele.EstimatedCoverageByDirection[i];
            }

            for (int i = 0; i < Constants.NumReadCollapsedTypes; i++)
            {
                ReadCollapsedCountsMut[i] = originalAllele.ReadCollapsedCountsMut[i];
                ReadCollapsedCountTotal[i] = originalAllele.ReadCollapsedCountTotal[i];
            }
        }

        public bool IsSameAllele(CalledAllele otherAllele)
        {
            return ((otherAllele.Chromosome == Chromosome)
                && (otherAllele.ReferencePosition == ReferencePosition)
                && (otherAllele.AlternateAllele == AlternateAllele)
                && (otherAllele.ReferenceAllele == ReferenceAllele));
        }

        public static CalledAllele DeepCopy(CalledAllele originalAllele)
        {
            if (originalAllele == null)
                return null;

            return new CalledAllele(originalAllele); 
        }
    }
}