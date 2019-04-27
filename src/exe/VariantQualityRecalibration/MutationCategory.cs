using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Models.Alleles;
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
           CalledAllele variant)
        {

            if (variant.Type == Pisces.Domain.Types.AlleleCategory.Reference)
                return MutationCategory.Reference;


            return GetMutationCategory(variant.ReferenceAllele, variant.AlternateAllele);
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
