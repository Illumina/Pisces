using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Pisces.IO.Sequencing;
using Pisces.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Options;

namespace VennVcf
{

    public enum VariantComparisonCase
    {
        AgreedOnReference,          //both probe pools say "ref"
        AgreedOnAlternate,          //both pools say the same alternate
        OneReferenceOneAlternate,   //one  pools says ref, and one says alternate
        CanNotCombine               //they call different alternates
    }

    public class VennProcessor
    {    
       
        private SampleAggregationParameters _SampleAggregationOptions = new SampleAggregationParameters();
        private Dictionary<string, VennVcfWriter> _vennDiagramWriters = new Dictionary<string, VennVcfWriter>();
        private VennVcfOptions _parameters;
        private string _outDir;
        private string[] _inputPaths = new[] { "", "" };
        private string[] InputFileNames = new[]{"", ""};
        private string[] InputSampleNames = new[]{"", ""};
        private string[] InputSampleNums = new[]{"", ""};
        ConsensusBuilder consensusBuilder;
        private string consensusFilePath;

        public string ConsensusFilePath
        {
            get { return consensusFilePath; }
        }
      
        public VennProcessor(string[] inputPaths, VennVcfOptions parameters )
        {
            string outDir = parameters.OutputDirectory;
            string consensusFileName = parameters.ConsensusFileName;
            _parameters = parameters;
            _inputPaths = inputPaths;
            _outDir = outDir;
            for (int i = 0; i < inputPaths.Length; i++)
            {
                InputFileNames[i] = Path.GetFileName(inputPaths[i]);
                GuessSampleNameFromVcf(InputFileNames[i], out InputSampleNames[i], out InputSampleNums[i]);
            }

            if (!String.IsNullOrEmpty(consensusFileName))
            {
                //writeConsensusFile = true;
                consensusFilePath = Path.Combine(outDir, consensusFileName);
                consensusBuilder = new ConsensusBuilder(consensusFilePath, parameters);
            }

        }


        public static bool IsVcfFileName(string vcfFileName)
        {
            string SampleName;
            string SampleNumber;
            return GuessSampleNameFromVcf(vcfFileName, out SampleName, out SampleNumber);
        }

        public static bool GuessSampleNameFromVcf(string vcfFileName, out string SampleName, out string SampleNumber)
        {
            Regex vcfRegex = new Regex(@"^(.+)_S(\d+)(.genome)?(.cftr)?.vcf(.gz)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Match vcfMatch = vcfRegex.Match(vcfFileName);
            SampleName = "Sample";
            SampleNumber = "X";
            if (vcfMatch.Success)
            {
                SampleName = vcfMatch.Groups[1].Value;
                SampleNumber = "S" + vcfMatch.Groups[2].Value;
            }
            else
            {
                string trimmedName = vcfFileName.Replace(".vcf", "").Replace(".gz", "").Replace(".genome", "");
                SampleName = trimmedName;
                SampleNumber = trimmedName;
            }

            return vcfMatch.Success;
        }

        /// <summary>
        /// perfom a Venn split between two samples
        /// </summary>
        /// <param name="sampleName"></param>
        /// <param name="consensusFilePath"></param>
        /// <param name="inputPaths"></param>
        /// <param name="outputTwoSampleResults"></param>
        public void DoPairwiseVenn(bool mFirst)
        {
            bool doConsensus = (consensusBuilder != null);
            bool requireGenotypes = false;

            using (VcfReader ReaderA = new VcfReader(_inputPaths[0], requireGenotypes))
            using (VcfReader ReaderB = new VcfReader(_inputPaths[1], requireGenotypes))
            {
                if (doConsensus)
                    consensusBuilder.OpenConsensusFile(ReaderA.HeaderLines);

               OpenVennDiagramStreams(ReaderA.HeaderLines);

                //read the first variant from each gvcf file...
                var currentAllele = new CalledAllele();
                var backLogPoolAVcfVariant = new VcfVariant();
                var backLogPoolBVcfVariant = new VcfVariant();

                var backLogExistPoolA = ReaderA.GetNextVariant(backLogPoolAVcfVariant);
                var backLogExistPoolB = ReaderB.GetNextVariant(backLogPoolBVcfVariant);

                var backLogPoolAAlleles = backLogExistPoolA ? Extensions.Convert(new List<VcfVariant> { backLogPoolAVcfVariant }).ToList() : null;
                var backLogPoolBAlleles = backLogExistPoolB ? Extensions.Convert(new List<VcfVariant> { backLogPoolBVcfVariant }).ToList() : null;

                //keep reading and processing until we are done with both gvcfs
                while (true)
                {
                    try
                    {

                        //1) Get the next set of variants. Pull from the backlog first,
                        //choosing all the variants at the first available position.
                        var coLocatedPoolAAlleles = new List<CalledAllele>();
                        var coLocatedPoolBAlleles = new List<CalledAllele>();

                        //We need to set up which location to look at next.  
                        //Choose the first one from the backlog.

                        if (backLogExistPoolA || backLogExistPoolB)
                        {

                            if (backLogExistPoolA && backLogExistPoolB)
                            {
                                int OrderResult = AlleleComparer.OrderAlleles(
                                    backLogPoolAAlleles.First(), backLogPoolBAlleles.First(), mFirst);
                                if (OrderResult < 0)
                                {
                                    currentAllele.Chromosome = backLogPoolAAlleles.First().Chromosome;
                                    currentAllele.ReferencePosition = backLogPoolAAlleles.First().ReferencePosition;
                                }
                                else
                                {
                                    currentAllele.Chromosome = backLogPoolBAlleles.First().Chromosome;
                                    currentAllele.ReferencePosition = backLogPoolBAlleles.First().ReferencePosition;
                                }
                            }
                            else if (backLogExistPoolB)
                            {
                                currentAllele.Chromosome = backLogPoolBAlleles.First().Chromosome;
                                currentAllele.ReferencePosition = backLogPoolBAlleles.First().ReferencePosition;
                            }
                            else //if (backLogExistPoolA)
                            {
                                currentAllele.Chromosome = backLogPoolAAlleles.First().Chromosome;
                                currentAllele.ReferencePosition = backLogPoolAAlleles.First().ReferencePosition;
                            }

                            //assemble lists of co-located variants at the position of the current variant
                            coLocatedPoolAAlleles = AssembleColocatedList(ReaderA, currentAllele, mFirst,
                                ref backLogExistPoolA, ref backLogPoolAAlleles);

                            coLocatedPoolBAlleles = AssembleColocatedList(ReaderB, currentAllele, mFirst,
                                ref backLogExistPoolB, ref backLogPoolBAlleles);

                        } //else, if there is nothing in either backlog, the colocated-variant list should stay empty.

                        //2) Now we have finshed reading out all the co-located variants...
                        //We need organize them into pairs, to know which allele to compare with which.
                        var Pairs = SelectPairs(coLocatedPoolAAlleles, coLocatedPoolBAlleles);
                        var ConsensusVariants = new List<CalledAllele>();
                        CalledAllele lastConsensusReferenceCall = null;

                        //3) For each pair, combine them and mark if biased or not.
                        for (int PairIndex = 0; PairIndex < Pairs.Count; PairIndex++)
                        {
                            var VariantA = Pairs[PairIndex][0];
                            var VariantB = Pairs[PairIndex][1];

                            var ComparisonCase = GetComparisonCase(VariantA, VariantB);


                            //add VarA and VarB to appropriate venn diagram files.
                            WriteVarsToVennFiles(ComparisonCase, VariantA, VariantB);                              
                            AggregateAllele Consensus = null;

                            if (doConsensus)
                            {
                                Consensus = consensusBuilder.CombineVariants(
                                    VariantA, VariantB, ComparisonCase);


                                //Its possible for multiallelic sites, a pair of variants could
                                //end up as a concensus reference. And we already may have 
                                //called a reference for this loci already.
                                //we might have some cleaning up to do...
                                if (Consensus.Genotype == Pisces.Domain.Types.Genotype.HomozygousRef)
                                {

                                    //this is the first time we see a reference at this loci
                                    if (lastConsensusReferenceCall == null)
                                    {
                                        lastConsensusReferenceCall = Consensus;
                                        //its OK to fall through and add our Consensus variant to the list.
                                    }

                                        //Else, if we have already called a reference variant 
                                    // for this loci already
                                    // we want to merge the results from this reference with the old one.
                                    // *before* we write it to file.
                                    else
                                    {
                                        //the chr, pos, ref, alt,and depth should be correct. 
                                        //We'll merge the filters,
                                        //and take the max SB and PB. (where a higher value indicates worse value, so we stay conservative)
                                        lastConsensusReferenceCall.Filters = ConsensusBuilder.CombineFilters(lastConsensusReferenceCall, Consensus);

                                        lastConsensusReferenceCall.StrandBiasResults = new Pisces.Domain.Models.BiasResults()
                                        { GATKBiasScore = Math.Max(lastConsensusReferenceCall.StrandBiasResults.GATKBiasScore, Consensus.StrandBiasResults.GATKBiasScore) };

                                        lastConsensusReferenceCall.PoolBiasResults = new Pisces.Domain.Models.BiasResults()
                                        { GATKBiasScore = Math.Max(lastConsensusReferenceCall.PoolBiasResults.GATKBiasScore, Consensus.PoolBiasResults.GATKBiasScore) };

                                        //we are going to take the min Q and NL score, to be conservative
                                        lastConsensusReferenceCall.NoiseLevelApplied = Math.Min(lastConsensusReferenceCall.NoiseLevelApplied, Consensus.NoiseLevelApplied);                                      
                                        lastConsensusReferenceCall.GenotypeQscore = Math.Min(lastConsensusReferenceCall.GenotypeQscore, Consensus.GenotypeQscore);
                                        lastConsensusReferenceCall.VariantQscore = Math.Min(lastConsensusReferenceCall.VariantQscore, Consensus.GenotypeQscore);

                                        continue;
                                    }
                                }

                                ConsensusVariants.Add(Consensus);
                            }
                        }

                        //4) Write out the results to file. (this will be a list of co-located variants)

                        if (doConsensus)
                            consensusBuilder.WriteConsensusVariantsToFile(ConsensusVariants);

                        //we assembled everyone and no one is left.
                        if ((backLogPoolAAlleles == null)
                            && (backLogPoolBAlleles == null))
                            break;
                    }
                    catch (Exception ex)
                    {
                        OnError(string.Format("Fatal error encountered comparing paired sample vcfs; Check {0}, position {1}.  Exception: {2}",
                            currentAllele.Chromosome, currentAllele.ReferencePosition, ex));
                        throw;
                    }

                } //close assemble list


            } //close usings

            if (doConsensus)
                consensusBuilder.CloseConsensusFile();

            CloseVennDiagramStreams();

        }

        private void CloseVennDiagramStreams()
        {            
            foreach (VennVcfWriter sw in _vennDiagramWriters.Values)
            {
                sw.Dispose();
            }
        }


        private void OpenVennDiagramStreams(List<string> vcfHeaderLines)
        {

            VcfWriterInputContext basicContext = new VcfWriterInputContext();
            VcfWriterConfig basicConfig = new VcfWriterConfig(_parameters.VariantCallingParams, _parameters.VcfWritingParams,
             _parameters.BamFilterParams, null, false, false);


            _vennDiagramWriters.Add("AnotB", new VennVcfWriter(GetVennFileName(_outDir, "not",0,1),basicConfig, basicContext, vcfHeaderLines, null, debugMode: _parameters.DebugMode ));
            _vennDiagramWriters.Add("AandB", new VennVcfWriter(GetVennFileName(_outDir, "and",0,1), basicConfig, basicContext, vcfHeaderLines, null, debugMode: _parameters.DebugMode));
            _vennDiagramWriters.Add("BnotA", new VennVcfWriter(GetVennFileName(_outDir, "not",1,0), basicConfig, basicContext, vcfHeaderLines, null, debugMode: _parameters.DebugMode));
            _vennDiagramWriters.Add("BandA", new VennVcfWriter(GetVennFileName(_outDir, "and",1,0), basicConfig, basicContext, vcfHeaderLines, null, debugMode: _parameters.DebugMode));

            foreach (VennVcfWriter writer in _vennDiagramWriters.Values)
            {
                writer.WriteHeader();
            }

        }

        private string GetVennFileName(string OutDir, string LogicOpAsString, int thisIndex, int otherIndex)
        {

            if (InputSampleNames[thisIndex] == InputSampleNames[otherIndex])
            {
                if (InputSampleNums[thisIndex] != InputSampleNums[otherIndex])
                {
                    return Path.Combine(OutDir,
                        string.Format("{0}_{1}_{2}_{3}.vcf", InputSampleNames[thisIndex], InputSampleNums[thisIndex], LogicOpAsString,
                            InputSampleNums[otherIndex]));
                }
                else
                {
                    return Path.Combine(OutDir,
                        string.Format("{0}_{1}_dir{2}_{3}_{4}_dir{5}.vcf", InputSampleNames[thisIndex], InputSampleNums[thisIndex], thisIndex, LogicOpAsString,
                            InputSampleNums[otherIndex], otherIndex));
                }
            }
            else
                return Path.Combine(OutDir, string.Format("{0}_{1}_{2}.vcf", InputSampleNames[thisIndex], LogicOpAsString, InputSampleNames[otherIndex]));
        }

        private void WriteVarsToVennFiles(VariantComparisonCase ComparisonCase, 
            CalledAllele VariantA, CalledAllele VariantB)
        {
            AggregateAllele AggregateA = AggregateAllele.SafeCopy(VariantA, new List<CalledAllele> { VariantA, VariantB });
            AggregateAllele AggregateB = AggregateAllele.SafeCopy(VariantB, new List<CalledAllele> { VariantB, VariantA });

            if (ComparisonCase == VariantComparisonCase.AgreedOnAlternate)
            {
                _vennDiagramWriters["AandB"].Write(new List<AggregateAllele>() { AggregateA });
                _vennDiagramWriters["BandA"].Write(new List<AggregateAllele>() { AggregateB });
            }

            if ((ComparisonCase == VariantComparisonCase.OneReferenceOneAlternate)
                || (ComparisonCase == VariantComparisonCase.CanNotCombine))
            {
                if ((VariantA != null) && (VariantA.Type != Pisces.Domain.Types.AlleleCategory.Reference))
                   _vennDiagramWriters["AnotB"].Write(new List<CalledAllele>() { VariantA });

                if ((VariantB != null) && (VariantB.Type != Pisces.Domain.Types.AlleleCategory.Reference))
                    _vennDiagramWriters["BnotA"].Write(new List<CalledAllele>() { VariantB });
            }
        }


        /// <summary>
        /// Step forward with the reader, assembling a list of variants at your CurrentVariant position.
        /// </summary>
        /// <param name="Reader"></param>
        /// <param name="CurrentVariant"></param>
        /// <param name="BackLogExists"></param>
        /// <param name="TheBackLog"></param>
        /// <returns></returns>
        private static List<CalledAllele> AssembleColocatedList(
            VcfReader Reader, CalledAllele CurrentVariant, bool mFirst,
            ref bool BackLogExists, ref List<CalledAllele> TheBackLog)
        {

            List<CalledAllele> CoLocatedVariants = new List<CalledAllele>();
            bool ContinueReadA = true;

            while (ContinueReadA)
            {
                var NextVariantList = new List<CalledAllele>();
           
                if (BackLogExists)
                {
                    NextVariantList =  TheBackLog;
                    BackLogExists = false;
                }
                else
                {
                    VcfVariant NextVariant = new VcfVariant();
                    ContinueReadA = Reader.GetNextVariant(NextVariant); 

                    if (!ContinueReadA) 
                        break;

                    NextVariantList = Extensions.Convert(new List<VcfVariant> { NextVariant }).ToList();
                    
                }

                // VarOrde =  -1 if Current comes first, 0 if co-located.
                int VarOrder = (AlleleComparer.OrderAlleles(CurrentVariant, NextVariantList.First(), mFirst));

                switch (VarOrder)
                {
                    case 0: //the variant we just got is at out current position
                        CoLocatedVariants.AddRange(NextVariantList);
                        break;
                    case -1: //the variant we just got is after our current position, and needs to go to the backlog.
                        TheBackLog = NextVariantList; //NextVariant;
                        ContinueReadA = false;
                        BackLogExists = true;
                        break;
                    default: // 
                    {
                        throw new InvalidDataException("Vcf needs to be ordered.");
                    }
                }
            }

            if (!BackLogExists)
                TheBackLog = null;

            return CoLocatedVariants;
        }

        /// <summary>
        /// given lists of co-located variants from each pool, figure out how to combine them, pairwise.
        /// </summary>
        /// <param name="PoolAVariants"></param>
        /// <param name="PoolBVariants"></param>
        /// <returns></returns>
        public static List<CalledAllele[]> SelectPairs(List<CalledAllele> PoolAVariants, List<CalledAllele> PoolBVariants)
        {
            var results = new List<CalledAllele[]>();

            //This covers ref+ref, ref+{alt,alt',alt''..}  cases.
            if ((PoolAVariants.Count == 1) && (PoolAVariants[0].AlternateAllele == "."))
            {
                for (int i = 0; i < PoolBVariants.Count; i++)
                {
                    results.Add(new CalledAllele[] { PoolAVariants[0], PoolBVariants[i] });
                }

                if (PoolBVariants.Count == 0)
                    results.Add(new CalledAllele[] { PoolAVariants[0], null });

            }
            else if ((PoolBVariants.Count == 1) && (PoolBVariants[0].AlternateAllele == "."))
            {
                for (int i = 0; i < PoolAVariants.Count; i++)
                {
                    results.Add(new CalledAllele[] { PoolAVariants[i], PoolBVariants[0] });
                }

                if (PoolAVariants.Count == 0)
                    results.Add(new CalledAllele[] { null, PoolBVariants[0]});

            }

            //this covers alt+alt, alt+alt' and  alt+ {alt,alt',alt''..}  cases.
            //If there are multiple entries in BOTH lists then we know *neither* pool called the reference.
            // What remains is to pair up the calls that agree, and leave the disagreements alone.
            else
            {
                List<int> IndexForMatchedPoolBAlleles = new List<int>();

                for (int i = 0; i < PoolAVariants.Count; i++)
                {
                    bool FoundMatch = false;

                    for (int j = 0; j < PoolBVariants.Count; j++)
                    {
                        if (
                            (PoolAVariants[i].ReferenceAllele == PoolBVariants[j].ReferenceAllele) &&
                            (PoolAVariants[i].AlternateAllele == PoolBVariants[j].AlternateAllele))
                        {
                            results.Add(new CalledAllele[] { PoolAVariants[i], PoolBVariants[j] });
                            IndexForMatchedPoolBAlleles.Add(j);
                            FoundMatch = true;
                            break;
                        }

                    }

                    //if we get here, no agreement was found for PoolAVariants[i]
                    if (!FoundMatch)
                        results.Add(new CalledAllele[] { PoolAVariants[i], null });
                }

                for (int j = 0; j < PoolBVariants.Count; j++)
                {
                    //if we get here, no agreement was found for PoolBVariants[j]
                    if (!IndexForMatchedPoolBAlleles.Contains(j))
                        results.Add(new CalledAllele[] { null, PoolBVariants[j]});
                }

            }
            return results;
        }

        public static VariantComparisonCase GetComparisonCase(CalledAllele VariantA, CalledAllele VariantB)
        {

            //if we have two or more different alternates, it should have already have been split up, upstream, 
            //into two or more entries to be processed ie, (alt, null) and (alt' ,nul).
            //So if we have a (VariantB == null) or (VariantA == null) situation, we know its TwoDifferentAlternates;
            if ((VariantB == null) || (VariantA == null))
                return VariantComparisonCase.CanNotCombine;

            //sanity checking - these input should never be allowed.
            if (VariantA.Chromosome != VariantB.Chromosome)
                throw new InvalidDataException("Check input variants to var compare algs.");

            if (VariantA.ReferencePosition != VariantB.ReferencePosition)
                throw new InvalidDataException("Check input variants to var compare algs.");

          
            //now several reasonable cases remain. we are only accepting the following cases as "possible to combine"
            //
            // . + . -> .        (treat as twos refs)
            // ref + . -> ref    (treat as twos refs)
            // alt + . -> alt    (treat as one ref, one alt)
            // Ref + ref -> ref
            // alt + alt -> alt (0/1 or 1/1) 
            // ref + alt -> alt (filtered)

            //this case below, is not allowed.
            // alt + alt' -> alt/alt' (filtered -> should not come into this algorithm)
            // intead this will go through the algorithm twice, as one (alt + null) and another (null + alt')

            //the no-calls GT will be preverved by the CombineGT algorthm.

            bool RefA = (VariantA.Type == Pisces.Domain.Types.AlleleCategory.Reference);
            bool RefB = (VariantB.Type == Pisces.Domain.Types.AlleleCategory.Reference);
            if (RefA && RefB)
                return VariantComparisonCase.AgreedOnReference;

            if ((RefA && !RefB) || (!RefA && RefB))
                return VariantComparisonCase.OneReferenceOneAlternate;

            if (VariantA.IsSameAllele(VariantB))
                return VariantComparisonCase.AgreedOnAlternate;
            else
            {
                //we could have different variants *or* different references (ie, in the case of a deletion)
                // (this should not happen - these should never be submitted for comparison to this alg.)
               throw new InvalidDataException("Check input variants to var compare algs.");
            }
        }

        protected void OnLog(string message)
        {
            Console.WriteLine(message);
        }

        protected void OnError(string message)
        {
            Console.WriteLine(message);
        }
    }


}
