using System.IO;

namespace VariantPhasing.Models
{
    public class VeadGroupMerger
    {
        public static void MergeProfile1Into2(VariantSite[] newData, VariantSite[] toUpdate)
        {
            var l1 = newData.Length;
            var l2 = toUpdate.Length;
            if (l1 != l2)
            {
                throw new InvalidDataException("VariantSites are not the same length and cannot be merged.");
            }

            for (var i = 0; i < toUpdate.Length; i++)
            {
                {
                    if (newData[i].HasRefData())
                    {
                        toUpdate[i].VcfReferenceAllele = newData[i].VcfReferenceAllele;
                    }

                    if (newData[i].HasAltData())
                    {
                        toUpdate[i].VcfAlternateAllele = newData[i].VcfAlternateAllele;
                    }

                }
            }

        }

    }
}