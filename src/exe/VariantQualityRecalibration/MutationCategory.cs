using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.IO.Sequencing;
using Common.IO.Utility;

namespace VariantQualityRecalibration

{

    public enum MutationCategory
    {
        AtoC = 1,       //
        AtoG,          //
        AtoT,          //
        CtoA,          //
        CtoG,          //
        CtoT,          // "C-> U-> T" deamidization
        GtoA,          // deamidization
        GtoC,          //
        GtoT,          //
        TtoA,          //
        TtoC,          //
        TtoG,          //
        Insertion,     //
        Deletion,
        Reference,
        Other
    }

    public class MutationCategoryUtil
    {


        public static MutationCategory GetMutationCategory(string EnumString)
        {
            return (MutationCategory)Enum.Parse(typeof(MutationCategory), EnumString);
        }

        public static MutationCategory GetMutationCategory(
            VcfVariant variant)
        {

            if (variant.VariantAlleles.Length == 0)
                return MutationCategory.Reference;


            //Be gentle here. This isnt enough that we need to crash a run. 
            //This would only happen for VQR run on germline / crushed mode.
            if (variant.VariantAlleles.Length > 1)
            {
                // throw new ArgumentException("This method is expecting only one variant allele per variant entry");
                Logger.WriteToLog("This method is expecting only one variant allele per variant entry, and we found " + variant.ToString());
                Logger.WriteToLog("Skipping variant");
                return MutationCategory.Other;
            }

            return GetMutationCategory(variant.ReferenceAllele, variant.VariantAlleles[0]);
        }

        public static MutationCategory GetMutationCategory(string referenceAllele, string alternateAllele)
        {

            int refLength = referenceAllele.Length;
            int altLength = alternateAllele.Length;

            if (refLength > altLength)
                return MutationCategory.Deletion;

            if (refLength < altLength)
                return MutationCategory.Insertion;

            if ((refLength != 1) || (altLength != 1))
                return MutationCategory.Other;

            if ((alternateAllele == ".")
                || (alternateAllele == referenceAllele))
                return MutationCategory.Reference;

            var enumString = referenceAllele + "to" + alternateAllele;

            foreach (MutationCategory mutation in GetAllMutationCategories())
            {
                if (enumString.ToLower() == mutation.ToString().ToLower())
                    return mutation;
            }

            return MutationCategory.Other;
        }

        public static List<MutationCategory> GetAllMutationCategories()
        {
            var Categories =
                Enum.GetValues(typeof(MutationCategory)).OfType<MutationCategory>().ToList();

            return Categories;
        }

        public static bool IsValidCategory(string testString)
        {
            var categoriesAsStrings = GetAllMutationCategories().Select(s => s.ToString());
            if (categoriesAsStrings.Contains(testString))
                return true;

            return false;
        }


    }

}
