using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TestUtilities;
using Alignment.Domain.Sequencing;
using Pisces.Interfaces;
using Pisces.Logic;
using Pisces.Logic.Alignment;
using Pisces.Logic.VariantCalling;
using Pisces.Tests.MockBehaviors;
using Pisces.IO.Sequencing;
using TestUtilities.MockBehaviors;
using Pisces.Calculators;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.IO;
using Pisces.Processing.RegionState;
using Xunit;
using Xunit.Extensions;

namespace Pisces.Tests
{
    public class LoadTestResult
    {
        public int Position { get; set; }
        public AlleleType BaseCall { get; set; }
        public DirectionType BaseDirection { get; set; }
        public int Count { get; set; }

    }

    public class StitchedReadLoadingHelper
    {

        public static string[] CheckReadLoading(BamAlignment read, ApplicationOptions options, ChrReference chrInfo, bool isVariant, StitchingScenario scenario)
        {
            string expectedVarLoading = scenario.RefLoading;
            string expectedCandidateDireciton = "0";

            if (isVariant)
            {
                expectedVarLoading = scenario.VarLoading;
                expectedCandidateDireciton = scenario.CandidateDirection;
            }

            var loadingResults = LoadReads(new List<BamAlignment>() { read }, options, chrInfo, isVariant, expectedVarLoading, expectedCandidateDireciton);

            if (loadingResults == null)
                return (new string[] { "total fail to parse variant reads" });

            //coverage check
            var variantReadLoadResult =CheckLoading(scenario, 1, loadingResults.Item1, isVariant);
            var variantReadCandidateDirection = CheckCandidateDirection(isVariant, loadingResults.Item2, expectedCandidateDireciton);


            if (variantReadLoadResult == null)
                return (new string[] { "total fail to check loading" });

            if (variantReadCandidateDirection == null)
                return (new string[] { "total fail to check direction" });

            return new string[] { variantReadLoadResult, variantReadCandidateDirection };
        }

    public static string CheckCandidateDirection(bool expectVariants, List<Domain.Models.Alleles.CandidateAllele> candidateVariants,
            string expectedDirectionString)
        {

            if (expectVariants)
            {
                if (candidateVariants.Count == 0)
                    return "FN";

                //foreach (var dirIndex in candidateVariants[0].SupportByDirection)
                for(int i=0; i<candidateVariants[0].SupportByDirection.Length;i++)
                {
                    DirectionType dirType = (DirectionType)i;
                    if (candidateVariants[0].SupportByDirection[i] == 1)
                        return dirType.ToString()[0].ToString();
                }
                return "FN";
            }
            else
            {
                //Assert.Equal(0, candidateVariants.Count);
                return "0";
            }
        }

        private static DirectionType LetterToDirection(string directionString)
        {
            DirectionType directionType = DirectionType.Stitched;
            switch (directionString)
            {
                case "F":
                    directionType = DirectionType.Forward;
                    break;
                case "R":
                    directionType = DirectionType.Reverse;
                    break;
                default:
                    directionType = DirectionType.Stitched;
                    break;
            }

            return directionType;
        }

        public static string CheckLoading(
                    StitchingScenario scenario, int readNumber, Dictionary<int, List<LoadTestResult>> counts, bool isVariantRead)
        {
            string expectedLoading = scenario.RefLoading.Split(';')[readNumber - 1];

            if (isVariantRead)
                expectedLoading = scenario.VarLoading.Split(';')[readNumber - 1];

            if (expectedLoading == "NA")
                return "NA";


            int startPos = int.Parse(expectedLoading[0].ToString());

            StringBuilder observedLoading = new StringBuilder(startPos.ToString());

            for (int i = 0; i < expectedLoading.Length - 1; i++)
            {
                DirectionType expectedDir = LetterToDirection(expectedLoading[i].ToString());
                int pos = startPos + i;

                if (counts[pos].Count == 0)
                {
                    observedLoading.Append("0");
                }
                else
                    observedLoading.Append(counts[pos][0].BaseDirection.ToString()[0]);
            }

            return observedLoading.ToString();

        }



        public static Tuple<Dictionary<int, List<LoadTestResult>>, List<Domain.Models.Alleles.CandidateAllele>> LoadReads
            (List<BamAlignment> reads, ApplicationOptions options, ChrReference chrRef,
            bool expectedvariants, string expectedLoading, string expectedDirectionString)
        {
            RegionStateManager manager = new RegionStateManager(expectStitchedReads:true);
            var variantFinder = new CandidateVariantFinder(options.MinimumBaseCallQuality, options.MaxSizeMNV, options.MaxGapBetweenMNV, options.CallMNVs);
            var candidateVariants = new List<Domain.Models.Alleles.CandidateAllele>();

            try
            {

                foreach (var b in reads)
                {
                    if (b == null)
                        continue;

                    var r = new Read(chrRef.Name, b);
                    // find candidate variants
                    candidateVariants = variantFinder.FindCandidates(r, chrRef.Sequence, chrRef.Name).ToList();
                    // track in state manager
                    manager.AddCandidates(candidateVariants);
                    manager.AddAlleleCounts(r);
                }

                Dictionary<int, List<LoadTestResult>> countResults = GetCountsFromManager(manager);
                var loadingResults = Tuple.Create(countResults, candidateVariants);

                return loadingResults;
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<int, List<LoadTestResult>> GetCountsFromManager(RegionStateManager manager)
        {
            var directions = Enum.GetValues(typeof(DirectionType));
            var alleles = Enum.GetValues(typeof(AlleleType));
            int maxPosWeCareAbout = 12;

            Dictionary<int, List<LoadTestResult>> countResults = new Dictionary<int, List<LoadTestResult>>();
            for (int pos = 1; pos < maxPosWeCareAbout; pos++)
            {
                List<LoadTestResult> results = new List<LoadTestResult>();
                countResults.Add(pos, results);

                foreach (AlleleType alleleType in alleles)
                {


                    for (int i = 0; i < directions.Length; i++)
                    {
                        int count = manager.GetAlleleCount(pos, alleleType, (DirectionType)i);

                        if (count != 0)
                        {
                            LoadTestResult result = new LoadTestResult()
                            {
                                Position = pos,
                                BaseCall = alleleType,
                                BaseDirection = (DirectionType)i,
                                Count = count
                            };
                            countResults[pos].Add(result);
                        }
                    }

                }
            }

            return countResults;
        }

      }
}

