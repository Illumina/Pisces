using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.IO.Sequencing;
using Pisces.IO.Sequencing;
using Pisces.Domain.Types;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;

namespace Pisces.IO
{
    public interface IAlleleSource
    {
        IEnumerable<CalledAllele> GetVariants();
        bool GetNextVariants(out List<CalledAllele> alleles, out string varString);
        bool GetNextVariants(out List<CalledAllele> alleles);
    }


    /// <summary>
    ///     AlleleReader is a single-sample vcf reader.
    /// </summary>
    public class AlleleReader : IAlleleSource, IDisposable
    {
        #region members
        public List<string> HeaderLines = new List<string>();
        protected const string PlaceholderAllele = "<M>";
        private bool _IsDisposed;
        private bool _IsOpen;
        private GzipReader _Reader;
        public List<string> Samples = new List<string>();
        private static char[] _InfoSplitChars = new char[] { ';' };
        private readonly object lockObj = new object();
        private string _vcfpath ;
        private bool _shouldTrimComplexAlleles = false;
        #endregion

        //Note, if you want to trim complex allels, that would change
        //chr2	50765521	.	GGAAGGCT -> GGAAGG
        //to
        //chr2	50765526	.	GCT -> G
        //This may affect ordering.

        // constructor
        public AlleleReader(string vcfPath, bool shouldTrimComplexAlleles= false, bool skipHeader = false)
        {
  
            _IsOpen = false;
            _vcfpath = vcfPath;
            _shouldTrimComplexAlleles = shouldTrimComplexAlleles;
            Open(vcfPath, skipHeader);
        }

        #region IDisposable
        // Note: These two pages explain IDisposable in great detail and give a picture for Why We Do Things This Way:
        // http://stackoverflow.com/questions/538060/proper-use-of-the-idisposable-interface
        // http://msdn.microsoft.com/en-us/library/system.idisposable(v=vs.110).aspx
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // destructor
        ~AlleleReader()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (lockObj)
            {
                if (!_IsDisposed)
                {
                    _IsDisposed = true;
                    Close();
                }
            }
        }
        #endregion

        //if we pick up the allele <M>, its not a real variant. skip it.
        protected static bool IsPlaceHolderAllele(CalledAllele allele)
        {
            return (allele.AlternateAllele == PlaceholderAllele);
        }

        /// <summary>
        ///     populates a called allele object given an array of vcf columns
        /// </summary>
        protected static void ConvertColumnsToVariant(bool shouldTrimComplexAlleles, string[] cols, CalledAllele allele, int alleleIndex)
        {
            bool shouldOutputRcCounts = true;
            bool shouldOutputTsCounts = true;
           
            if ((cols == null) || (cols.Length == 0))
            {
                allele = null;
                return;
            }

            //set defaults.
            var genotypeQscore = 0;
            var referenceSupport = 0;
            var altSupport = 0;
            var genotypeString = "";
            var totalCoverage = 0;

            var variantQuality = 0.0;
            var numAlts = 1;
            var noiseLevel = 0;
            var fractionNocalls = 0f;
            var strandBiasInGATKScaleCoords = -100f;
            var tsCounts = new List<string>();
            //

            //read in simple data
            allele.Chromosome = cols[VcfCommon.ChromIndex];
            allele.ReferencePosition = int.Parse(cols[VcfCommon.PosIndex]);
            allele.ReferenceAllele = cols[VcfCommon.RefIndex];
            allele.Filters = VcfVariantUtilities.MapFilterString(cols[VcfCommon.FilterIndex]);



            bool gotQual = double.TryParse(cols[VcfCommon.QualIndex], out variantQuality); // CFTR uses a ".", which is not actually legal... (actually, vcf 4.1 does allow the missing value "." here. Strelka uses it)
            if (gotQual)
                allele.VariantQscore = (int)variantQuality;

            // parse the variant alleles
            var variantAlleles = cols[VcfCommon.AltIndex].Split(',');
            allele.AlternateAllele = variantAlleles[alleleIndex];
            var isRef = (allele.AlternateAllele == ".");

            if (isRef)
                numAlts = 0;
            else
                numAlts = variantAlleles.Count();


            // parse the info field data (presume, single  sample)
            Dictionary<string, string> InfoFields = ParseInfoFields(cols);

            // parse the genotype data (presume, single  sample)
            List<Dictionary<string, string>> Genotypes = ParseGenotypeData(cols);

            //get more complex allele data...

            if (InfoFields.ContainsKey("DP"))
                totalCoverage = Int32.Parse(InfoFields["DP"]);

            if ((Genotypes.Count > 0) && (Genotypes[0] != null))
            {

                if (Genotypes[0].ContainsKey("GQ"))
                    genotypeQscore = Int32.Parse(Genotypes[0]["GQ"]);
                else if (Genotypes[0].ContainsKey("GQX"))
                    genotypeQscore = Int32.Parse(Genotypes[0]["GQX"]);

                if (Genotypes[0].ContainsKey("GT"))
                    genotypeString = Genotypes[0]["GT"];

                if (Genotypes[0].ContainsKey("NL"))
                    noiseLevel = Int32.Parse(Genotypes[0]["NL"]);

                if (Genotypes[0].ContainsKey("NC"))
                    fractionNocalls = float.Parse(Genotypes[0]["NC"]);

                if (Genotypes[0].ContainsKey("SB"))
                    strandBiasInGATKScaleCoords = float.Parse(Genotypes[0]["SB"]);

                var ADstrings = new string[] { "0", "0" };

                if (Genotypes[0].ContainsKey("AD"))
                    ADstrings = Genotypes[0]["AD"].Split(',');

                referenceSupport = int.Parse(ADstrings[0]);

                //by default alt support is 0. 
                if ((!isRef) && (ADstrings.Length > 1))
                {
                    altSupport = int.Parse(ADstrings[1]);
                }

                if (shouldOutputRcCounts)
                {
                    if (Genotypes[0].ContainsKey("US"))
                        tsCounts = Genotypes[0]["US"].Split(',').ToList();
                }

                allele.Genotype = VcfVariantUtilities.MapGTString(genotypeString, numAlts);

                //note this awkward vcf line (pisces)
                //"chr4\t10\t.\tAA\tGA,G\t0\tPASS\tDP=5394\tGT:GQ:AD:DP:VF:NL:SB:NC\t1/2:0:2387,2000:5394:0.8133:23:0.0000:0.0000";
                //and this one
                //chr2    19946216.ATGTGTG ATG,ATGTG,A 0   PASS metal = platinum; cgi =.; bwa_freebayes = HD:0,LOOHD: 0; bwa_platypus =.; bwa_gatk3 = HD:2,LOOHD: 2; cortex =.; isaac2 = HD:1,LOOHD: 1; dist2closest = 192 GT  1 | 2

                if ((numAlts >= 2) && (Genotypes[0].ContainsKey("AD")))
                {

                    if (ADstrings.Count() <= numAlts) //in this case we never expressedly gave the ref support, so we have to derive it.
                    {
                        int totalAltCount = 0;

                        for (int altIndex = 0; altIndex < numAlts; altIndex++)
                        {
                            var altSupportAtIndex = int.Parse(ADstrings[altIndex]);
                            totalAltCount += altSupportAtIndex;

                            if (altIndex == alleleIndex)
                                altSupport = altSupportAtIndex;
                        }
                        referenceSupport = Math.Max(0, totalCoverage - totalAltCount);
                    }
                }


            }

            var strandBiasResults = new BiasResults();
            strandBiasResults.GATKBiasScore = strandBiasInGATKScaleCoords;





            //set the remaining data
            allele.TotalCoverage = totalCoverage;
            allele.AlleleSupport = isRef ? referenceSupport : altSupport;
            allele.ReferenceSupport = referenceSupport;
            allele.GenotypeQscore = genotypeQscore;
            allele.NoiseLevelApplied = noiseLevel;
            allele.StrandBiasResults = strandBiasResults;
            allele.IsForcedToReport = allele.Filters.Contains(FilterType.ForcedReport);

            //set the derived values
            allele.SetType();
            allele.ForceFractionNoCalls(fractionNocalls);

            //rescue attempt for complex types, ie ACGT -> ACGTGG.
            //Get the simplest form of the allele
            if ((allele.Type == AlleleCategory.Unsupported) && shouldTrimComplexAlleles)
                VcfVariantUtilities.TrimUnsupportedAlleleType(allele);

            if (tsCounts.Count != 0)
                VcfVariantUtilities.FillInCollapsedReadsCount(shouldOutputRcCounts, shouldOutputTsCounts, allele, tsCounts);


        }

        private static List<Dictionary<string, string>> ParseGenotypeData(string[] cols)
        {
            var Genotypes = new List<Dictionary<string, string>>();
            if (cols.Length > VcfCommon.GenotypeIndex) // Genotype columns present
            {
                string GenotypeTagString = cols[VcfCommon.FormatIndex];
                string[] GenotypeTagOrder = GenotypeTagString.Split(':');


                string genotypeColumn = cols[VcfCommon.GenotypeIndex];
                if (genotypeColumn == ".")
                {
                    Genotypes.Add(null);
                }
                else
                {
                    string[] genotypeCols = genotypeColumn.Split(':');
                    Genotypes.Add(ParseGenotype(GenotypeTagOrder, genotypeCols));
                }

            }

            return Genotypes;
        }

        private static Dictionary<string, string> ParseInfoFields(string[] cols)
        {
            var InfoFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string InfoData = cols[VcfCommon.InfoIndex];
            if (InfoData == ".") InfoData = ""; // Special case: a "." in the INFO field should be treated like an empty string.
            string[] infoCols = InfoData.Split(_InfoSplitChars, StringSplitOptions.RemoveEmptyEntries);

            int numInfoCols = infoCols.Length;
            var InfoTagOrder = new string[numInfoCols];


            for (int infoColIndex = 0; infoColIndex < numInfoCols; infoColIndex++)
            {
                string infoField = infoCols[infoColIndex];
                string[] infoFieldKvp = infoField.Split('=');
                InfoTagOrder[infoColIndex] = infoFieldKvp[0];
                InfoFields[infoFieldKvp[0]] = (infoFieldKvp.Length == 1 ? null : infoFieldKvp[1]);
            }

            return InfoFields;
        }


        /// <summary>
        ///     closes the vcf file
        /// </summary>
        private void Close()
        {
            if (!_IsOpen) return;
            _IsOpen = false;
            _Reader.Close();
        }

        /// <summary>
        /// Loop over variants like this: foreach (VcfVariant variant in reader.GetVariants())
        /// </summary>
        public IEnumerable<CalledAllele> GetVariants()
        {
            // sanity check: make sure the file is open
            if (!_IsOpen) yield break;

            while (true)
            {
                // grab the next vcf line
                string line = _Reader.ReadLine();
                if (line == null) break;

                // split the columns and assign them to VcfVariant
                string[] cols = line.Split('\t');
                var allelesStrings = cols[VcfCommon.AltIndex].Split(',');

                var numAlleles = allelesStrings.Length;

                for (int index = 0; index < allelesStrings.Length; index++)
                {
                    CalledAllele variant = new CalledAllele();

                    // convert the columns to a variant
                    ConvertColumnsToVariant(_shouldTrimComplexAlleles, cols, variant, index);

                    if (!IsPlaceHolderAllele(variant))
                        yield return variant;
                }
            }
        }


        /// <summary>
        /// Load variant but keep unparsed line around
        /// </summary>
        public bool GetNextVariants(out List<CalledAllele> alleles, out string line)
        {
            line = null;
            alleles = null;

            // sanity check: make sure the file is open
            if (!_IsOpen) return false;

            // grab the next vcf line
            line = _Reader.ReadLine();
            if (line == null) return false;

            alleles = VcfLineToAlleles(line);


            if (alleles.Count == 0)
                return false;

            return true;
        }

        /*
        /// <summary>
        /// Given an input 'hanging" variant, get all the variants from the next lines that are colocated.
        /// If there are no other co-clocated variants, or the we hit the end of the file, return a list of one.
        /// Return the (now closed) colocated group, and give the next "hanging variant.
        /// </summary>
        public List<AlleleLine> CloseColocatedGroup(List<AlleleLine> incomingHangingVariants,
            out List<AlleleLine> outgoingHangingVariants,
            out List<string> lines)
        {
            
            
            outgoingHangingVariants = new List<AlleleLine>();
            lines = new List<string>();

            List<AlleleLine> coLocatedGroup = (incomingHangingVariants==null)? new List<AlleleLine>() { } : incomingHangingVariants;

            // sanity check: make sure the file is open
            if (!_IsOpen)
                throw new FileNotFoundException("File " + _vcfpath + " is not available for reading." );

            while (true)
            {
                if (!_IsOpen)
                    throw new FileNotFoundException("File " + _vcfpath + " has become unavailable for reading.");
                
                // grab the next vcf line
                var line = _Reader.ReadLine();
                if (line == null) return coLocatedGroup;
                lines.Add(line);

                var newVcfAlleles = VcfLineToAlleles(line);

                if (newVcfAlleles.Count == 0)
                    return coLocatedGroup;


                //else, we found something!
                if ((coLocatedGroup.Count==0) || (newVcfAlleles[0].Allele.IsCoLocatedAllele(coLocatedGroup[0].Allele)))
                {
                    coLocatedGroup.AddRange(newVcfAlleles);
                }
                else //we need to close and start the next group
                {
                    //the next batch of alleles should go in another group
                    outgoingHangingVariants = newVcfAlleles;
                    return coLocatedGroup;
                }
            }
        }*/


        /// <summary>
        /// Given an input 'hanging" variant vcf line, get all the next lines that are colocated.
        /// If there are no other co-clocated variants, or the we hit the end of the file, return a list of one.
        /// Return the (now closed) colocated group, and give the next "hanging variant line.
        /// Dont bother parsing into the allele class (to save time).
        /// </summary>
        public List<string> CloseColocatedLines(string incomingHangingVariantLine,
            out string outgoingHangingVariantLine)
        {

            outgoingHangingVariantLine = null;

            List<string> coLocatedLines = (incomingHangingVariantLine == null) ? new List<string>() { } : new List<string>() { incomingHangingVariantLine };

            // sanity check: make sure the file is open
            if (!_IsOpen)
                throw new FileNotFoundException("File " + _vcfpath + " is not available for reading.");

            while (true)
            {
                if (!_IsOpen)
                    throw new FileNotFoundException("File " + _vcfpath + " has become unavailable for reading.");

                // grab the next vcf line
                var line = _Reader.ReadLine();
                if (line == null) return coLocatedLines;

                //var newVcfAlleles = VcfLineToAlleles(line);
                var newVcfAlleles = line;

                if (newVcfAlleles.Length == 0)
                    return coLocatedLines;

                
                //else, we found something!
                if ((coLocatedLines.Count == 0) || (VcfLineToLociHash(newVcfAlleles) == VcfLineToLociHash(coLocatedLines[0])))
                {
                    coLocatedLines.Add(newVcfAlleles);
                }
                else //we need to close and start the next group
                {
                    //the next batch of alleles should go in another group
                    outgoingHangingVariantLine = newVcfAlleles;
                    return coLocatedLines;
                }
            }
        }


        /// <summary>
        /// Given a list of lines from a vcf, convert them to alleles
        /// </summary>
        public static List<CalledAllele> VcfLinesToAlleles(List<string> lines)
        {

            var alleles = new List<CalledAllele>();

            foreach (var line in lines)
            {
                var newVcfAlleles = VcfLineToAlleles(line);
                alleles.AddRange(newVcfAlleles);
            }

            return alleles;
        }

        public static string VcfLineToLociHash(string line)
        {
            string[] cols = line.Split('\t');
            return string.Format("{0}_{1}", cols[VcfCommon.ChromIndex], cols[VcfCommon.PosIndex]);
        }

        public static List<CalledAllele> VcfLineToAlleles(string line, bool shouldTrimComplexAlleles=false)
        {
            string[] cols = line.Split('\t');
            var alleles = new List<CalledAllele>();
            var allelesStrings = cols[VcfCommon.AltIndex].Split(',');

            var numAlleles = allelesStrings.Length;

            for (int index = 0; index < allelesStrings.Length; index++)
            {
                CalledAllele allele = new CalledAllele();

                // convert the columns to a variant
                ConvertColumnsToVariant(shouldTrimComplexAlleles, cols, allele, index);

                if (!IsPlaceHolderAllele(allele))
                    alleles.Add(allele);
            }

            return alleles;
        }



        /// <summary>
        ///     Retrieves the next available variant and returns false if no variants are available.
        /// </summary>
        public bool GetNextVariants(out List<CalledAllele> alleles)
        {
            string line = "dummy";
            var result = GetNextVariants(out alleles, out line);
            return result;
        }



        /// <summary>
        ///     opens the vcf file and reads the header
        /// </summary>
        private void Open(string vcfPath, bool skipHeader)
        {
            // sanity check: make sure the vcf file exists
            if (!File.Exists(vcfPath))
            {
                throw new FileNotFoundException(string.Format("The specified vcf file ({0}) does not exist.", vcfPath));
            }

            _Reader = new GzipReader(vcfPath);
            _IsOpen = true;
            if (skipHeader)
            {
                Samples.Add("Sample");
            }
            else
            {
                ParseHeader();
            }
        }

        /// <summary>
        ///     parse a sample genotype column and returns the corresponding dictionary
        /// </summary>
        private static Dictionary<string, string> ParseGenotype(string[] genotypeFormatTags, string[] genotypeCols)
        {
            Dictionary<string, string> genotypeMap = new Dictionary<string, string>();
            // sanity check: make sure we have the same number of columns
            if (genotypeFormatTags.Length < genotypeCols.Length)
            {
                throw new InvalidDataException(string.Format(
                        "VCF parse error: Expected the same number of columns in the genotype format column ({0}) as in the sample genotype column ({1}).",
                        genotypeFormatTags.Length, genotypeCols.Length));
            }
            for (int colIndex = 0; colIndex < genotypeCols.Length; colIndex++)
            {
                genotypeMap[genotypeFormatTags[colIndex]] = genotypeCols[colIndex].Trim().Replace("\"", "");
            }

            return genotypeMap;
        }

        /// <summary>
        ///     reads the vcf header
        /// </summary>
        private void ParseHeader()
        {
            // store the header
            string line;

            while (true)
            {
                // grab the next line - stop if we have reached the main header or read the entire file
                line = _Reader.ReadLine().Trim();
                if ((line == null) || line.StartsWith(VcfCommon.ChromosomeHeader)) break;
                HeaderLines.Add(line);
            }

            // sanity check
            if ((line == null) || !line.StartsWith(VcfCommon.ChromosomeHeader))
            {
                throw new InvalidDataException(
                    string.Format("Could not find the vcf header (starts with {0}). Is this a valid vcf file?",
                                  VcfCommon.ChromosomeHeader));
            }

            // establish how many samples we have
            string[] headerCols = line.Split('\t');
            HeaderLines.Add(line);
            int sampleCount = headerCols.Length - VcfCommon.GenotypeIndex;

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Samples.Add(headerCols[VcfCommon.GenotypeIndex + sampleIndex]);
            }
        }

        /// <summary>
        /// Load a list of all variants in a file.  This is memory-intensive; don't do this for whole-genome vcf files!
        /// </summary>
        public static List<CalledAllele> GetAllVariantsInFile(string vcfPath)
        {
            List<CalledAllele> allVariants = new List<CalledAllele>();
            using (AlleleReader reader = new AlleleReader(vcfPath))
            {
                foreach (CalledAllele variant in reader.GetVariants())
                {
                    allVariants.Add(variant);
                }
            }
            return allVariants;
        }

        public static List<string> GetAllHeaderLines(string vcfPath)
        {
            List<string> header;
            using (var reader = new AlleleReader(vcfPath))
            {
                header = reader.HeaderLines;
            }

            return header;
        }
        /// <summary>
        ///     Returns the actual position within the vcf file (used when making our ad-hoc index)
        /// </summary>
        public long Position()
        {
            return _Reader.GetCurrentPosition();
        }
    }
}