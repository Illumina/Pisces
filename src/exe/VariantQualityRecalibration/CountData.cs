using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;

namespace VariantQualityRecalibration
{
    public class CountData
    {
        public Dictionary<MutationCategory, double> CountsByCategory { get; } = new Dictionary<MutationCategory, double>();

        public double NumPossibleVariants { get; set; } = 0;

        public CountData()
        {
            var categories = MutationCategoryUtil.GetAllMutationCategories();

            foreach (var nucleotideTransitionCategory in categories)
            {
                CountsByCategory.Add(nucleotideTransitionCategory, 0);
            }

        }



        public double TotalMutations
        {
            get
            {
                double total = 0;
                foreach (double count in CountsByCategory.Values)
                    total += count;

                return total;
            }

        }


        public double ObservedMutationRate
        {
            get
            {
                if (NumPossibleVariants == 0)
                    return 0;

                return (TotalMutations / NumPossibleVariants);
            }
        }

        public double MutationRateForCategory(MutationCategory cat)
        {
            if (CountsByCategory.ContainsKey(cat))
            {
                return ((double)CountsByCategory[cat]) / NumPossibleVariants;
            }
            else
                return 0;
        }
        public bool Add(VcfVariant variant)
        {
            if (variant == null)
                return false;

            var category = MutationCategoryUtil.GetMutationCategory(variant);
            NumPossibleVariants++;

            if (category != MutationCategory.Reference)
            {
                CountsByCategory[category]++;

                return true;
            }


            return false;
        }


        /// <summary>
        /// These days we mostly use gvcf. So we can get the total num loci by looking at the gvcf and counting all the positions.
        /// HOWEVER sometimes we want to recalibrate a vcf. In that case, we want the mutation rate but dont know how many loci the
        /// varcalling was done over. This is how we force that number in.
        /// </summary>
        /// <param name="lociCount"></param>
        public void ForceTotalPossibleMutations(int lociCount)
        {
            if (lociCount > 0)
                NumPossibleVariants = lociCount;
            else
            {
                //leave it unchanged. Use the value we derived from the gvcf file.
            }
        }
    }
}