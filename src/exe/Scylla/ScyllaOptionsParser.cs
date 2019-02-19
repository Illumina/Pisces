using System.Collections.Generic;
using System.IO;
using Common.IO.Utility;
using CommandLine.Options;
using CommandLine.NDesk.Options;
using VariantPhasing;
using Pisces.Domain.Options;

namespace Scylla
{
    public class ScyllaOptionsParser : BaseOptionParser
    {

        
        public ScyllaOptionsParser()
        {
            Options = new ScyllaApplicationOptions();
        }

        public ScyllaApplicationOptions ScyllaOptions { get => (ScyllaApplicationOptions)Options; }

        public override Dictionary<string, OptionSet> GetParsingMethods()
        {
            var requiredOps = new OptionSet
            {
                {
                    "bam=",
                    OptionTypes.PATH + $"path to bam file",
                    value => ScyllaOptions.BamPath = value
                },
                {
                    "vcf=",
                    OptionTypes.PATH + $" path to input vcf file.",
                    value => ScyllaOptions.VcfPath = value
                }

            };

            var commonOps = new OptionSet
            {
                {
                    "t|maxnumthreads=",
                    OptionTypes.INT + $" Number of threads to use. Default, {ScyllaOptions.NumThreads}",
                    value=>ScyllaOptions.NumThreads = int.Parse(value)
                },
                {
                    "debug=",
                    OptionTypes.BOOL + $" Run the program in debug mode (additional logging).",
                    value=>ScyllaOptions.Debug = bool.Parse(value)
                 },
                {
                 "o|out|outfolder=",
                    OptionTypes.FOLDER + $" output directory",
                    value=> ScyllaOptions.OutputDirectory = value
                },
                {
                 "g|genomefolder=",
                    OptionTypes.FOLDER + $"genome directory. If left unset, reference bases reported inside phased variants will be left as 'R' ",
                    value=> ScyllaOptions.GenomePath = value
                }
            };


            //TODO - move this code
            var clusteringOps = new OptionSet
            {
                {
                    "allowclustermerging=",
                    OptionTypes.BOOL + $" Whether clusters should be allowed to merge, 'true' or 'false'. Default, " +
                    $"{ScyllaOptions.ClusteringParams.AllowClusterMerging}",
                    value=>ScyllaOptions.ClusteringParams.AllowClusterMerging = bool.Parse(value)
                } ,
                {
                    "allowworstfitremoval=",
                    OptionTypes.BOOL + $" Whether a cluster should try to remove and reassign its worst fit, 'true' or 'false'. Default, {ScyllaOptions.ClusteringParams.AllowWorstFitRemoval}",
                    value=>ScyllaOptions.ClusteringParams.AllowWorstFitRemoval = bool.Parse(value)
                },
                {
                    "clusterconstraint=",
                    OptionTypes.INT + $" Constrain the number of clusters to this number, if possible. Analogous to forced ploidy.",
                    value=>ScyllaOptions.ClusteringParams.ClusterConstraint = int.Parse(value)
                }
            };

            //TODO - move this code
            var phasableCriteriaOps = new OptionSet
            {
                {
                    "passingvariantsonly=",
                    OptionTypes.BOOL + $" Whether only passing variants should be allowed to phase, 'true' or 'false'. Default, {ScyllaOptions.PhasableVariantCriteria.PassingVariantsOnly}",
                    value => ScyllaOptions.PhasableVariantCriteria.PassingVariantsOnly = bool.Parse(value)
                },
                {
                    "hetvariantsonly=",
                    OptionTypes.BOOL + $"  Whether only het variants should be allowed to phase, 'true' or 'false'. Default, {ScyllaOptions.PhasableVariantCriteria.HetVariantsOnly}",
                    value=>ScyllaOptions.PhasableVariantCriteria.HetVariantsOnly = bool.Parse(value)
                },
                {
                    "maxnbhdstoprocess=",
                    OptionTypes.INT + $"  A debug option, an integer cap on the number of neighborhoods to process. If -1, all neighborhoods will be processed. Default, {ScyllaOptions.PhasableVariantCriteria.MaxNumNbhdsToProcess} (all)",
                    value=>ScyllaOptions.PhasableVariantCriteria.MaxNumNbhdsToProcess = int.Parse(value)
                },
                {
                    "chr=",
                    OptionTypes.STRING + $"  Array indicating which chromosomes to process (ie, [chr1,chr9]). If empty, all chromosomes will be processed. Default, empty (all)",
                    value=>ScyllaOptions.PhasableVariantCriteria.ChrToProcessArray = OptionHelpers.ListOfParamsToStringArray(value)
                },
                {
                    "nbhd=",
                    OptionTypes.STRING + $" Debug option to specify a nbhd ",
                    value=>ScyllaOptions.PhasableVariantCriteria.FilteredNbhdToProcess = value
                },
                {
                    "dist=",
                    OptionTypes.INT + $" How close variants need to be to chain together. Should be less than read length.",
                    value=>ScyllaOptions.PhasableVariantCriteria.PhasingDistance = int.Parse(value)
                }
            };

            var clippedReadSupportOps = new OptionSet
            {
                               {
                    "usesoftclippedreads=",	
                    OptionTypes.BOOL + $" Extract support from soft clipped reads, 'true' or 'false'. Default, " +
                        $"{ScyllaOptions.SoftClipSupportParams.UseSoftClippedReads}",	
                            value=>ScyllaOptions.SoftClipSupportParams.UseSoftClippedReads = bool.Parse(value)
                 },	
               {
                    "minsizeforcliprescue=",	
                    OptionTypes.INT + $" Minimum size (length of ref allele + alt allele) of MNV to rescue supporting" +
                    $" clipped reads. Default, " +
                    $"{ScyllaOptions.SoftClipSupportParams.MinSizeForClipRescue}",	
                    value=>ScyllaOptions.SoftClipSupportParams.MinSizeForClipRescue = int.Parse(value)
                 }
                            };
            

                        var optionDict = new Dictionary<string, OptionSet>
            {
                {OptionSetNames.Required,requiredOps},
                {OptionSetNames.Common,commonOps },
                {OptionSetNames.Clustering,clusteringOps},
                {OptionSetNames.PhasableCriteria,phasableCriteriaOps },
                {OptionSetNames.ClippedReadSupport,clippedReadSupportOps }
            };

            BamFilterOptionsUtils.AddBamFilterArgumentParsing(optionDict, ScyllaOptions.BamFilterParams);
            VariantCallingOptionsParserUtils.AddVariantCallingArgumentParsing(optionDict, ScyllaOptions.VariantCallingParams);
            VcfWritingParserUtils.AddVcfWritingArgumentParsing(optionDict, ScyllaOptions.VcfWritingParams);

            return optionDict;
        }


        public override void ValidateOptions()
        {

            CheckInputFilenameExists(ScyllaOptions.BamPath, "bam file", "--bam");
            CheckInputFilenameExists(ScyllaOptions.VcfPath, "vcf file", "--vcf");
            if (string.IsNullOrEmpty(ScyllaOptions.OutputDirectory))
            {
                ScyllaOptions.OutputDirectory = Path.GetDirectoryName(ScyllaOptions.VcfPath);

                if (string.IsNullOrEmpty(ScyllaOptions.OutputDirectory))
                {
                    ScyllaOptions.OutputDirectory = Directory.GetCurrentDirectory();//some sensible default
                }
            }
            ScyllaOptions.Validate();
            ScyllaOptions.SetDerivedValues();
        }


    }
}