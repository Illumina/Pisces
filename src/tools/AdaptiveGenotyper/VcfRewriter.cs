using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Pisces.IO.Sequencing;
using Pisces.Domain.Types;
using Common.IO.Utility;
using Common.IO;
using Pisces.IO;
using Pisces.Genotyping;

//TODO: This class does too many things. should separate VcfReWriting from Recalibrating work,  and reuse VariantQualityRecalibration VcfRewriter (stick in a common lib, Recalibration.Util)
namespace AdaptiveGenotyper
{
    public class VcfRewriter
    {
        // Static fields
        private const int _vcfHeaderOffset = 5;
        private const int MinVq = 20;
        private static readonly PloidyModel SamplePloidy = PloidyModel.DiploidByAdaptiveGT;
        private static readonly List<double[]> _defaultMeans = new List<double[]>
        {
                new double[] { 0.99, 0.005, 0.005 },
                new double[] { 0.98, 0.01, 0.01 }
        };

        // Internally used fields
        private readonly List<RecalibratedVariantsCollection> Variants;
        private readonly List<double[]> ModelMeans;
        private readonly List<double[]> Priors;

        public VcfRewriter(List<RecalibratedVariantsCollection> variants, List<double[]> means,
            List<double[]> priors)
        {
            Variants = variants;
            ModelMeans = means;
            Priors = priors;
        }
        public VcfRewriter(List<RecalibratedVariantsCollection> variants, List<double[]> means)
            : this(variants, means, _defaultMeans)
        { }

        public void Rewrite(string vcfIn, string outDir, string quotedCommandLineString)
        {
            Logger.WriteToLog("Rewriting VCF.");
            string vcfFileName = Path.GetFileName(vcfIn);
            if (vcfFileName.Contains("genome."))
                vcfFileName = vcfFileName.Replace("genome", "recal");
            else
                vcfFileName = vcfFileName.Replace(".vcf", ".recal.vcf");

            string vcfOut = Path.Combine(outDir, vcfFileName);

            if (File.Exists(vcfOut))
                File.Delete(vcfOut);

            try
            {
                RewriteVcf(vcfIn, vcfOut, quotedCommandLineString);

                if (File.Exists(vcfOut))
                {
                    Logger.WriteToLog("The following vcf was recalibrated: " + vcfIn);
                }

            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Recalibrate failed for " + vcfIn);
                Logger.WriteToLog("Exception: " + ex);
            }
        }

        private void RewriteVcf(string vcfIn, string vcfOut, string quotedCommandLineString)
        {

            using (VcfReader reader = new VcfReader(vcfIn))
            using (StreamWriter writer = new StreamWriter(new FileStream(vcfOut, FileMode.CreateNew)))
            {
                writer.NewLine = "\n";
                List<string> headerLines = reader.HeaderLines;
                WriteHeaders(writer, headerLines, quotedCommandLineString);


                var originalVar = new VcfVariant();
                var lastVar = new VcfVariant();
                List<string> buffer = new List<string>();
                while (reader.GetNextVariant(originalVar))
                {

                    // Check for multiallelic locus or deletion
                    if (originalVar.ReferencePosition == lastVar.ReferencePosition &&
                            originalVar.ReferenceName == lastVar.ReferenceName)
                        originalVar = ProcessMultiallelicVariant(reader, ref lastVar, originalVar, ref buffer);

                    // Check if within deletion
                    if (lastVar.ReferenceAllele != null &&
                            originalVar.ReferenceAllele != null &&
                            originalVar.ReferencePosition < lastVar.ReferencePosition + lastVar.ReferenceAllele.Length &&
                            originalVar.ReferenceName == lastVar.ReferenceName &&
                            lastVar.Genotypes[0]["GT"] != "0/0")
                        originalVar = ProcessDeletion(reader, lastVar, originalVar, ref buffer);

                    // Check if the the deletion or multiallelic variant reached the end of the file
                    if (originalVar.InfoFields == null)
                        break;
                    else if (originalVar.Filters.ToLower().Contains("lowdp"))
                        continue;

                    // Use somatic call for ChrM
                    // Change here if adding a male option
                    else if (GenotypeCreator.GetPloidyForThisChr(SamplePloidy, null, originalVar.ReferenceName)
                       != PloidyModel.DiploidByAdaptiveGT)
                    {
                        if (originalVar.Genotypes[0]["GT"] != "0/0" && originalVar.Genotypes[0]["GT"] != "0/." &&
                            originalVar.Quality > MinVq)
                        {
                            originalVar.Genotypes[0]["GQ"] = originalVar.Quality.ToString();
                            buffer.Add(originalVar.ToString());
                            lastVar = originalVar;
                            originalVar = new VcfVariant();
                        }
                        continue;
                    }

                    if (buffer.Count > 500)
                    {
                        writer.Write(string.Join('\n', buffer) + "\n");
                        buffer.Clear();
                    }

                    string key = originalVar.ReferenceName + ":" + originalVar.ReferencePosition.ToString();

                    int variantType = 0;
                    if (VariantReader.GetVariantType(originalVar) == VariantType.Indel)
                        variantType = 1;

                    if (Variants.Count > 0 && Variants[variantType].ContainsKey(key))
                        UpdateVariant(originalVar, Variants[variantType][key].MixtureModelResult);
                    else
                        UpdateVariant(originalVar);

                    if (originalVar.Genotypes[0]["GT"] != "0/0")
                        buffer.Add(originalVar.ToString());

                    lastVar = originalVar;
                    originalVar = new VcfVariant();
                }

                writer.Write(string.Join('\n', buffer));
            }
        }

        private VcfVariant ProcessDeletion(VcfReader reader, VcfVariant deletionVar, VcfVariant originalVar,
            ref List<string> buffer)
        {
            while (true)
            {
                if (originalVar.Genotypes[0]["GT"].Contains("1")) // skips variants that are called somatic 0/0
                {
                    // Call current variant that's within the deletion
                    string key = originalVar.ReferenceName + ":" + originalVar.ReferencePosition.ToString();

                    int variantType = 0;
                    if (VariantReader.GetVariantType(originalVar) == VariantType.Indel)
                        variantType = 1;

                    if (Variants.Count > 0 && Variants[variantType].ContainsKey(key))
                        UpdateVariant(originalVar, Variants[variantType][key].MixtureModelResult);
                    else if (originalVar.ReferenceName.Any(char.IsDigit))
                        UpdateVariant(originalVar);

                    // Correct the GT; changes 1/1 calls too
                    if (originalVar.Genotypes[0]["GT"].Contains("1") && deletionVar.Filters == "PASS")
                    {
                        originalVar.Genotypes[0]["GT"] = "1/.";
                        buffer.Add(originalVar.ToString());
                    }
                }

                VcfVariant lastVar = originalVar;
                originalVar = new VcfVariant();
                reader.GetNextVariant(originalVar);
                if (originalVar.ReferencePosition == lastVar.ReferencePosition &&
                        originalVar.ReferenceName == lastVar.ReferenceName)
                    originalVar = ProcessMultiallelicVariant(reader, ref lastVar, originalVar, ref buffer);

                if (originalVar.ReferencePosition > deletionVar.ReferencePosition + deletionVar.ReferenceAllele.Length - 1 ||
                    originalVar.ReferenceName != deletionVar.ReferenceName)
                    break;
            }
            return originalVar;
        }

        private VcfVariant ProcessMultiallelicVariant(VcfReader reader, ref VcfVariant lastVar, VcfVariant originalVar,
            ref List<string> buffer)
        {
            List<VcfVariant> variants = new List<VcfVariant>() { lastVar, originalVar };

            // Use VF to find the two major variants
            List<double> vf = new List<double>() { double.Parse(lastVar.Genotypes[0]["VF"]),
                double.Parse(originalVar.Genotypes[0]["VF"])};
            int[] topIndices = new int[] { 0, 1 };
            Array.Sort(vf.ToArray(), topIndices);
            Array.Reverse(topIndices);

            // Keep track of ref vf
            // NB: refVf is only approximate and could be negative if ref alleles are different lengths
            double refVf = 1 - vf[0] - vf[1];

            int currIndex = 2;
            while (true)
            {
                originalVar = new VcfVariant();
                reader.GetNextVariant(originalVar);
                if (originalVar.ReferencePosition != lastVar.ReferencePosition ||
                        originalVar.ReferenceName != lastVar.ReferenceName)
                    break;

                // Handle variant and update top 2 in VF
                variants.Add(originalVar);
                double newVf = double.Parse(originalVar.Genotypes[0]["VF"]);
                vf.Add(newVf);
                if (newVf > vf[topIndices[0]])
                {
                    topIndices[1] = topIndices[0];
                    topIndices[0] = currIndex;
                }
                else if (newVf > vf[topIndices[1]])
                    topIndices[1] = currIndex;

                refVf = refVf - vf[currIndex];
                currIndex++;
                lastVar = originalVar;
            }

            VcfVariant variantToWrite;
            string originalGT = variants[0].Genotypes[0]["GT"];

            if (variants[0].ReferenceName.Any(char.IsDigit))
            {
                // Find new category with model
                UpdateVariant(variants[topIndices[0]]);
                if (variants[topIndices[0]].Genotypes[0]["GT"] == "0/1") // 1/2 or 0/1
                {
                    double newVf = vf[topIndices[0]];
                    UpdateVariant(variants[topIndices[1]]);

                    if (variants[topIndices[1]].Genotypes[0]["GT"] != "0/0") // 1/2
                        variantToWrite = UpdateMultiAllelicVariant(variants[topIndices[0]], variants[topIndices[1]]);
                    else // 0/1
                        variantToWrite = variants[topIndices[0]];

                    // Determine if site is multi-allelic, but only for SNPs
                    string multiAllelicFilter = new VcfFormatter().MapFilter(FilterType.MultiAllelicSite);
                    if (variantToWrite.ReferenceAllele.Length == 1 &&
                        variantToWrite.VariantAlleles.First().Length == 1 &&
                        variantToWrite.VariantAlleles.Last().Length == 1 &&
                        vf[topIndices[0]] + vf[topIndices[1]] < 0.8 &&
                        vf[topIndices[0]] + refVf < 0.8)
                    {
                        variantToWrite.Genotypes[0]["GT"] = "./.";
                        if (variantToWrite.Filters != "PASS")
                            variantToWrite.Filters = variantToWrite.Filters + ";" + multiAllelicFilter;
                        else
                            variantToWrite.Filters = multiAllelicFilter;
                    }
                }
                else // 0/0 or 1/1
                    variantToWrite = variants[topIndices[0]];
            }
            else // Consolidate somatic calls
            {
                variantToWrite = variants[0];
                variantToWrite.Genotypes[0]["GQ"] = variantToWrite.Quality.ToString();
            }

            // Delete the last line that was written if it was written
            if (originalGT != "0/0" && buffer.Count > 0)
                buffer.RemoveAt(buffer.Count - 1);

            // Append last line
            if (variantToWrite.Genotypes[0]["GT"] != "0/0")
                buffer.Add(variantToWrite.ToString());

            lastVar = variantToWrite;
            return originalVar;
        }

        private VcfVariant UpdateMultiAllelicVariant(VcfVariant variant1, VcfVariant variant2)
        {
            VcfVariant variantToWrite;
            int ad1, ad2, dp;
            if (variant1.ReferenceAllele.Length == variant2.ReferenceAllele.Length)
            {
                variantToWrite = variant1;
                variantToWrite.VariantAlleles = new string[]
                    { variant1.VariantAlleles[0], variant2.VariantAlleles[0] };
                ad1 = int.Parse(variant1.Genotypes[0]["AD"].Split(',').Last());
                ad2 = int.Parse(variant2.Genotypes[0]["AD"].Split(',').Last());
            }
            else // Deletion case; need to merge ref alleles
            {
                // Step 1: take longer length ref
                VcfVariant otherVariant;
                bool ordered = true;
                if (variant1.ReferenceAllele.Length > variant2.ReferenceAllele.Length)
                {
                    variantToWrite = variant1;
                    otherVariant = variant2;
                }
                else
                {
                    variantToWrite = variant2;
                    otherVariant = variant1;
                    ordered = false;
                }

                // Step 2: reformat the other alt--take alt + ending substring of the longer ref
                string newAlt = otherVariant.VariantAlleles[0] +
                    variantToWrite.ReferenceAllele.Substring(otherVariant.ReferenceAllele.Length,
                    variantToWrite.ReferenceAllele.Length - otherVariant.ReferenceAllele.Length);

                // Step 3: determine order of alt and AD
                if (ordered) // variantToWrite is also the max VF
                {
                    variantToWrite.VariantAlleles = new string[] { variantToWrite.VariantAlleles[0], newAlt };
                    ad1 = int.Parse(variantToWrite.Genotypes[0]["AD"].Split(',').Last());
                    ad2 = int.Parse(otherVariant.Genotypes[0]["AD"].Split(',').Last());
                }
                else
                {
                    variantToWrite.VariantAlleles = new string[] { newAlt, variantToWrite.VariantAlleles[0] };
                    ad1 = int.Parse(otherVariant.Genotypes[0]["AD"].Split(',').Last());
                    ad2 = int.Parse(variantToWrite.Genotypes[0]["AD"].Split(',').Last());
                }
            }
            variantToWrite.Genotypes[0]["AD"] = ad1.ToString() + "," + ad2.ToString();
            variantToWrite.Genotypes[0]["GT"] = "1/2";

            dp = VariantReader.ParseDepth(variantToWrite);
            if (variantToWrite.ReferenceAllele.Length > 1 || variantToWrite.VariantAlleles.Any(x => x.Length > 1))
            {
                dp = Math.Max(int.Parse(variantToWrite.Genotypes[0]["AD"].Split(',').First()) +
                    int.Parse(variantToWrite.Genotypes[0]["AD"].Split(',').Last()), dp);

                if (variantToWrite.Genotypes[0].ContainsKey("DP"))
                    variantToWrite.Genotypes[0]["DP"] = dp.ToString();
                if (variantToWrite.InfoFields.ContainsKey("DP"))
                    variantToWrite.InfoFields["DP"] = dp.ToString();
            }

            variantToWrite.Genotypes[0]["VF"] = ((float)(ad1 + ad2) / dp).ToString("0.000");
            UpdateMultiAllelicQScores(variantToWrite);
            return variantToWrite;
        }

        private void UpdateMultiAllelicQScores(VcfVariant variant)
        {
            // Determine model types
            int modelType1, modelType2;
            if (variant.ReferenceAllele.Length == variant.VariantAlleles[0].Length)
                modelType1 = 0;
            else
                modelType1 = 1;

            if (variant.ReferenceAllele.Length == variant.VariantAlleles[1].Length)
                modelType2 = 0;
            else
                modelType2 = 1;

            // Calculate posteriors based on multinomial distribution
            int dp = VariantReader.ParseDepth(variant);
            int[] temp = Array.ConvertAll(variant.Genotypes[0]["AD"].Split(','), x => int.Parse(x));
            int[] ad = new int[3];
            ad[2] = temp[1];
            ad[1] = temp[0];
            ad[0] = dp - ad[1] - ad[2];
            if (ad[0] < 0)
            {
                variant.Genotypes[0]["GP"] = "0,0,0,0,0,0";
                variant.Genotypes[0]["GQ"] = "0";
                return;
            }

            //RecalibratedVariant recal = MixtureModel.GetMultinomialQScores(ad, dp, 
            //    new List<double[]> { means[modelType1], means[modelType2] });

            var mixtureModelResult = MixtureModel.GetMultinomialQScores(ad, dp,
                new List<double[]> { ModelMeans[modelType1], ModelMeans[modelType2] });

            // Update variant genotypes fields
            variant.Genotypes[0]["GP"] = string.Join(',', mixtureModelResult.GenotypePosteriors.Select(x => x.ToString("0.00")).ToArray());
            variant.Genotypes[0]["GQ"] = mixtureModelResult.QScore.ToString();
        }



        private void UpdateVariant(VcfVariant originalVar)
        {
            int modelType = 0;
            if (VariantReader.GetVariantType(originalVar) == VariantType.Indel)
                modelType = 1;

            var recal = new RecalibratedVariantsCollection();
            recal.AddLocus(originalVar);

            MixtureModel mm = new MixtureModel(recal.Ad, recal.Dp, ModelMeans[modelType], Priors[modelType]);
            mm.UpdateClusteringAndQScore();
            UpdateVariant(originalVar, mm.PrimaryResult);
        }

        private void UpdateVariant(VcfVariant originalVar, MixtureModelResult mixtureModelResult)
        {
            UpdateGenotype(originalVar, mixtureModelResult.GenotypeCategory);
            List<string> tagOrder = originalVar.GenotypeTagOrder.OfType<string>().ToList();
            if (!tagOrder.Contains("GP"))
            {
                tagOrder.Add("GP");
                originalVar.GenotypeTagOrder = tagOrder.ToArray();
            }

            if (!originalVar.Genotypes[0].ContainsKey("GP"))
                originalVar.Genotypes[0].Add("GP", string.Join(',', mixtureModelResult.GenotypePosteriors.Select(x => x.ToString("0.00")).ToArray()));

            originalVar.Genotypes[0]["GQ"] = mixtureModelResult.QScore.ToString();
        }

        private void UpdateGenotype(VcfVariant originalVar, SimplifiedDiploidGenotype category)
        {
            if (category == SimplifiedDiploidGenotype.HomozygousRef)
            {
                originalVar.Genotypes[0]["GT"] = "0/0";
            }
            else if (category == SimplifiedDiploidGenotype.HeterozygousAltRef)
            {
                if (VariantReader.GetVariantType(originalVar) == VariantType.NoVariant)
                {
                    originalVar.Genotypes[0]["GT"] = "0/0";
                }
                else if (VariantReader.GetVariantType(originalVar) == VariantType.Snv)
                {   // SNP case
                    originalVar.Genotypes[0]["GT"] = "0/1";
                }
                else if (VariantReader.GetVariantType(originalVar) == VariantType.Indel)
                {   // Insertion case
                    originalVar.Genotypes[0]["GT"] = "0/1";
                }
            }
            else if (category == SimplifiedDiploidGenotype.HomozygousAlt)
            {
                if (VariantReader.GetVariantType(originalVar) == VariantType.NoVariant)
                {
                    originalVar.Genotypes[0]["GT"] = "0/0";
                }
                else if (VariantReader.GetVariantType(originalVar) == VariantType.Snv)
                {   // SNP case
                    originalVar.Genotypes[0]["GT"] = "1/1";
                }
                else if (VariantReader.GetVariantType(originalVar) == VariantType.Indel)
                {   // Insertion case
                    originalVar.Genotypes[0]["GT"] = "1/1";
                }
            }
        }

        private void WriteHeaders(StreamWriter writer, List<string> headerLines, string quotedCommandLineString)
        {
            foreach (string headerLine in headerLines.Take(_vcfHeaderOffset))
                writer.WriteLine(headerLine);

            var currentAssembly = Assembly.GetEntryAssembly().GetName();
            var version = currentAssembly.Version;
            writer.WriteLine("##AdaptiveGenotyper=AdaptiveGenotyper " + version);

            if (!string.IsNullOrEmpty(quotedCommandLineString))
                writer.WriteLine("##AdaptiveGenotyper_cmdline=" + quotedCommandLineString);

            bool filterAdded = false;
            VcfWriterConfig config = new VcfWriterConfig { PloidyModel = PloidyModel.DiploidByThresholding };
            string filter = (new VcfFormatter(config)).GenerateFilterStringsByType()[FilterType.MultiAllelicSite];

            bool gpAdded = false;
            string gp = "##FORMAT=<ID=GP,Number=G,Type=Float,Description=\"Genotype Posterior\">";

            for (var i = _vcfHeaderOffset; i < headerLines.Count; i++)
            {
                writer.WriteLine(headerLines[i]);

                // Check if multiallelic filter already exists
                if (headerLines[i].Contains("##FILTER"))
                {
                    if (headerLines[i] == filter)
                        filterAdded = true;

                    if (!headerLines[i + 1].Contains("##FILTER") && !filterAdded)
                        writer.WriteLine(filter);
                }

                if (headerLines[i].Contains("##FORMAT"))
                {
                    if (headerLines[i] == gp)
                        gpAdded = true;

                    if (!headerLines[i + 1].Contains("##FORMAT") && !gpAdded)
                        writer.WriteLine(gp);
                }
            }
        }
    }
}
