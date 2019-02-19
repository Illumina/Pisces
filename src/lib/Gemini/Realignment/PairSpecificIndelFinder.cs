using System.Collections.Generic;
using System.Linq;
using Alignment.Domain;
using Alignment.Domain.Sequencing;
using Gemini.IndelCollection;
using Gemini.Interfaces;
using Gemini.Models;

namespace Gemini.Realignment
{
    public class NonPairSpecificIndelFinder : IPairSpecificIndelFinder
    {
        public List<PreIndel> GetPairSpecificIndels(ReadPair readpair, ref int? r1Nm, ref int? r2Nm)
        {
            return null;
        }
    }

    public class PairSpecificIndelFinder : IPairSpecificIndelFinder
    {
        private readonly IChromosomeIndelSource _indelSource;
        private readonly string _chromosome;
        private IndelTargetFinder _targetFinder = new IndelTargetFinder();

        public PairSpecificIndelFinder(string chromosome, IChromosomeIndelSource indelSource)
        {
            _chromosome = chromosome;
            _indelSource = indelSource;
        }

        public List<PreIndel> GetPairSpecificIndels(ReadPair readpair, ref int? r1Nm, ref int? r2Nm)
        {
            int mapQForIndelFInding = 20;
            return PairIndels(readpair.Read1, readpair.Read2, mapQForIndelFInding, ref r1Nm, ref r2Nm);
        }

        private List<PreIndel> PairIndels(BamAlignment origRead1, BamAlignment origRead2,int mapQForIndelFInding,
            ref int? r1Nm, ref int? r2Nm)
        {

            var r1IndelsTotal = _targetFinder.FindIndels(origRead1, _chromosome);
            var r2IndelsTotal = _targetFinder.FindIndels(origRead2, _chromosome);

            var indelsLimitedToR1Indels =
                _indelSource.GetRelevantIndels(origRead1.Position, r1IndelsTotal.ToList());
            var indelsLimitedToR2Indels =
                _indelSource.GetRelevantIndels(origRead1.Position, r2IndelsTotal.ToList());

            if (indelsLimitedToR1Indels.Any(x => x.Key.InMulti) ||
                indelsLimitedToR2Indels.Any(x => x.Key.InMulti))
            {
                // TODO maybe do something different here
                return null;
                //pairIndels.AddRange(r1IndelsTotal);
                //pairIndels.AddRange(r2IndelsTotal);
            }

            if (!indelsLimitedToR1Indels.Any() && !indelsLimitedToR2Indels.Any()) return null;

            var pairIndels = new List<PreIndel>();
            r1Nm = origRead1.GetIntTag("NM");
            r2Nm = origRead1.GetIntTag("NM");


            if (indelsLimitedToR1Indels.Any() &&
                indelsLimitedToR2Indels.Any())
            {
                // If both have high quality indels, take the better of the two
                var significantlyBetterMultiplier = 1.5;
                var r1Score = indelsLimitedToR1Indels.First().Key
                    .Score;
                var r2Score = indelsLimitedToR2Indels.First().Key
                    .Score;

                if (r1Score > (r2Score * significantlyBetterMultiplier))
                {
                    pairIndels.AddRange(r1IndelsTotal);
                }
                else if (r2Score > (r1Score * significantlyBetterMultiplier))
                {
                    pairIndels.AddRange(r2IndelsTotal);
                }
                else if (origRead1.MapQuality > mapQForIndelFInding && origRead1.MapQuality >= origRead2.MapQuality &&
                         r1Nm <= r2Nm)
                {
                    pairIndels.AddRange(r1IndelsTotal);
                }
                else if (origRead2.MapQuality > mapQForIndelFInding)
                {
                    pairIndels.AddRange(r2IndelsTotal);
                }
            }
            else if (indelsLimitedToR1Indels.Any() &&
                     origRead1.MapQuality > mapQForIndelFInding)
            {
                pairIndels.AddRange(r1IndelsTotal);
            }
            else
            {
                pairIndels.AddRange(r2IndelsTotal);
            }

            return pairIndels;

        }
    }
}