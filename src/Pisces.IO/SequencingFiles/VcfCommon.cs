using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Pisces.IO.Sequencing
{
    public static class VcfCommon
    {
        #region header titles

        public const string ChromosomeHeader = "#CHROM";
        public const string FilterTag = "##FILTER=";
        public const string FormatTag = "##FORMAT=";
        public const string InfoTag = "##INFO=";
        public const string SourceTag = "##source=";

        #endregion

        #region storage classes

        private enum NumberClasses
        {
            None,
            Period,
            Single,
            Allele,
            Genotype
        }

        private enum StorageClasses
        {
            Float,
            Integer,
            String
        }

        #endregion

        #region column indexes

        public const int ChromIndex = 0;
        public const int PosIndex = 1;
        public const int IDIndex = 2;
        public const int RefIndex = 3;
        public const int AltIndex = 4;
        public const int QualIndex = 5;
        public const int FilterIndex = 6;
        public const int InfoIndex = 7;
        public const int FormatIndex = 8;
        public const int GenotypeIndex = 9;

        #endregion

        private const int MinNumColumns = 9;
    }

    public enum VariantType
    {
        Complex = 0, // A variant type we don't otherwise categorize - e.g. ref ATG, alt GTACC
        Reference = 1,
        SNV = 2,
        Insertion = 3,
        Deletion = 4,
        SNVInsertion = 5,
        SNVDeletion = 6,
        InsertionDeletion = 7,
        MNP = 8, // multiple nucleotide polymorphism
        Missing = 255 // missing genotype data e.g. .
    }

    /// <summary>
    ///     VcfVariant stores all of the data from one line of VCF data
    ///     Let's keep the info fields and genotypes stored as strings
    ///     for now.
    /// </summary>
    public class VcfVariant
    {
        #region members
        public string Filters;
        public string[] GenotypeTagOrder = null;
        public List<Dictionary<string, string>> Genotypes;
        public string Identifier;
        public Dictionary<string, string> InfoFields;
        public string[] InfoTagOrder = null;
        public double Quality;
        public bool HasQuality = true;
        public string ReferenceAllele;
        public string ReferenceName;
        public int ReferencePosition;
        public VariantType VarType; // Represents the type of the variant (both alleles).  Deprecated!
        public VariantType VarType1; // Variant type of allele 1
        public VariantType VarType2; // Variant type of allele 2
        public string[] VariantAlleles;
        #endregion

        #region ToString

        /// <summary>
        ///     Re-encode the information in the vcf variant into a vcf line
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t", ReferenceName, ReferencePosition, Identifier, ReferenceAllele);

            // write the alternate alleles
            int numVariantAlleles = VariantAlleles.Length;
            sb.Append(VariantAlleles[0]);

            for (int variantIndex = 1; variantIndex < numVariantAlleles; variantIndex++)
            {
                sb.AppendFormat(",{0}", VariantAlleles[variantIndex]);
            }

            if (HasQuality)
                sb.AppendFormat("\t{0:0.##}", Quality);
            else
                sb.Append("\t.");

            sb.AppendFormat("\t{0}\t", Filters);

            // write the info column
            bool needSemicolon = false;
            foreach (string infoField in InfoTagOrder)
            {
                if (needSemicolon) sb.Append(';');

                string infoValue;
                if (!InfoFields.TryGetValue(infoField, out infoValue))
                {
                    throw new ApplicationException(
                        string.Format("Unable to find the dictionary entry for the following info key: {0}", infoField));
                }

                if (infoValue == null)
                {
                    sb.Append(infoField);
                }
                else
                {
                    sb.AppendFormat("{0}={1}", infoField, infoValue);
                }

                needSemicolon = true;
            }
            if (InfoTagOrder.Length == 0) sb.Append("."); // VCF spec says: Any empty fields must be '.' 

            if (Genotypes != null && Genotypes.Any())
            {
                sb.Append('\t');

                // write the format field
                bool needColon = false;
                foreach (string formatField in GenotypeTagOrder)
                {
                    if (needColon) sb.Append(':');
                    sb.Append(formatField);
                    needColon = true;
                }

                // write the sample genotypes
                foreach (Dictionary<string, string> t in Genotypes)
                {
                    sb.Append('\t');
                    if (t == null)
                    {
                        sb.Append(".");
                    }
                    else
                    {
                        Dictionary<string, string> currentGenotype = t;

                        needColon = false;
                        foreach (string formatField in GenotypeTagOrder)
                        {
                            if (needColon) sb.Append(':');

                            string formatValue;
                            if (!currentGenotype.TryGetValue(formatField, out formatValue))
                            {
                                throw new ApplicationException(
                                    string.Format("Unable to find the dictionary entry for the following info key: {0}",
                                                  formatField));
                            }

                            sb.Append(formatValue);
                            needColon = true;
                        }
                    }
                }
            }

            return sb.ToString();
        }

        #endregion

        /*
        #region Batch operation
        public delegate T VcfOperation<out T>(VcfVariant variant);
        public static List<T> OperateOnVariantsInFile<T>(string fileName, VcfOperation<T> operation)
        {
            if (!File.Exists(fileName))
                return null;
            List<T> variantList = new List<T>();

            using (VcfReader reader = new VcfReader(fileName))
            {
                foreach (VcfVariant variant in reader.GetVariants())
                {
                    variantList.Add(operation(variant));
                }
            }
            return variantList;
        }

        #endregion
        */
    }
}