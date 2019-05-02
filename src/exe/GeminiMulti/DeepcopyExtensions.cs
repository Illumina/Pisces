using BamStitchingLogic;
using Gemini.Utility;

namespace GeminiMulti
{
    public static class DeepcopyExtensions
    {
        public static StitcherOptions DeepCopy(this StitcherOptions options)
        {
            return new StitcherOptions()
            {
                Debug = options.Debug,
                DebugSummary = options.DebugSummary,
                DontStitchHomopolymerBridge = options.DontStitchHomopolymerBridge,
                FilterDuplicates = options.FilterDuplicates,
                FilterForProperPairs = options.FilterForProperPairs,
                FilterMinMapQuality = options.FilterMinMapQuality,
                FilterPairLowMapQ = options.FilterPairLowMapQ,
                FilterPairUnmapped = options.FilterPairUnmapped,
                FilterUnstitchablePairs = options.FilterUnstitchablePairs,
                IdentifyDuplicates = options.IdentifyDuplicates,
                MinBaseCallQuality = options.MinBaseCallQuality,
                NumThreads = options.NumThreads,
                SortMemoryGB = options.SortMemoryGB,
                MaxReadLength = options.MaxReadLength,
                IgnoreReadsAboveMaxLength = options.IgnoreReadsAboveMaxLength,
                MaxNumDisagreeingBases = options.MaxNumDisagreeingBases,
                NifyDisagreements = options.NifyDisagreements,
                NifyUnstitchablePairs = options.NifyUnstitchablePairs,
                ThreadByChromosome = options.ThreadByChromosome,
                StitchGappedPairs = options.StitchGappedPairs,
                StitchProbeSoftclips = options.StitchProbeSoftclips,
                UseSoftClippedBases = options.UseSoftClippedBases,
                KeepUnpairedReads = options.KeepUnpairedReads,
            };
        }

        public static GeminiOptions DeepCopy(this GeminiOptions options)
        {
            return new GeminiOptions()
            {
                GenomeContextSize = options.GenomeContextSize,
                IndelsCsvName = options.IndelsCsvName,
                Debug = options.Debug,
                StitchOnly = options.StitchOnly,
                GenomePath = options.GenomePath,
                SamtoolsPath = options.SamtoolsPath,
                TrustSoftclips = options.TrustSoftclips,
                SkipStitching = options.SkipStitching,
                KeepBothSideSoftclips = options.KeepBothSideSoftclips,
                SkipAndRemoveDups = options.SkipAndRemoveDups,
                KeepProbeSoftclip = options.KeepProbeSoftclip,
                KeepUnmergedBams = options.KeepUnmergedBams,
                IsWeirdSamtools = options.IsWeirdSamtools,
                UseHygeaComparer = options.UseHygeaComparer,
                AllowRescoringOrigZero = options.AllowRescoringOrigZero,
                IndexPerChrom = options.IndexPerChrom,
                SoftclipUnknownIndels = options.SoftclipUnknownIndels,
                RemaskMessySoftclips = options.RemaskMessySoftclips,
                SkipEvidenceCollection = options.SkipEvidenceCollection,
                FinalIndelsOverride = options.FinalIndelsOverride,
                ReadCacheSize = options.ReadCacheSize,
                MessySiteThreshold = options.MessySiteThreshold,
                MessySiteWidth = options.MessySiteWidth,
                CollectDepth = options.CollectDepth,
                ImperfectFreqThreshold = options.ImperfectFreqThreshold,
                IndelRegionFreqThreshold = options.IndelRegionFreqThreshold,
                RegionDepthThreshold = options.RegionDepthThreshold,
                NumConcurrentRegions = options.NumConcurrentRegions,
                LogRegionsAndRealignments = options.LogRegionsAndRealignments,
                LightDebug = options.LightDebug,
                AvoidLikelySnvs = options.AvoidLikelySnvs,
                RecalculateUsableSitesAfterSnowball = options.RecalculateUsableSitesAfterSnowball,
                SortPerChrom = options.SortPerChrom,
                ForceHighLikelihoodRealigners = options.ForceHighLikelihoodRealigners,
                RequirePositiveOutcomeForSnowball = options.RequirePositiveOutcomeForSnowball,
                RegionSize = options.RegionSize
            };
        }

        public static IndelFilteringOptions DeepCopy(this IndelFilteringOptions options)
        {
            return new IndelFilteringOptions
            {
                FoundThreshold = options.FoundThreshold,
                MinAnchor = options.MinAnchor,
                BinSize = options.BinSize,
                StrictFoundThreshold = options.StrictFoundThreshold,
                StrictAnchorThreshold = options.StrictAnchorThreshold,
                MaxMess = options.MaxMess,
            };
        }

        public static RealignmentAssessmentOptions DeepCopy(this RealignmentAssessmentOptions options)
        {
            return new RealignmentAssessmentOptions
            {
                CheckSoftclipsForMismatches = options.CheckSoftclipsForMismatches,
                TrackActualMismatches = options.TrackActualMismatches
            };
        }

        public static GeminiSampleOptions DeepCopy(this GeminiSampleOptions options)
        {
            return new GeminiSampleOptions()
            {
                InputBam = options.InputBam,
                IntermediateDir = options.IntermediateDir,
                OutputBam = options.OutputBam,
                OutputFolder = options.OutputFolder,
                RefId = options.RefId
            };
        }

        public static RealignmentOptions DeepCopy(this RealignmentOptions options)
        {
            return new RealignmentOptions()
            {
                CategoriesForRealignment = options.CategoriesForRealignment,
                CategoriesForSnowballing = options.CategoriesForSnowballing,
                MaskPartialInsertion = options.MaskPartialInsertion,
                MaxIndelSize = options.MaxIndelSize,
                MinimumUnanchoredInsertionLength = options.MinimumUnanchoredInsertionLength,
                NumSubSamplesForSnowballing = options.NumSubSamplesForSnowballing,
                PairAwareEverything = options.PairAwareEverything,
                RemaskMessySoftclips = options.RemaskMessySoftclips
            };
        }

    }
}