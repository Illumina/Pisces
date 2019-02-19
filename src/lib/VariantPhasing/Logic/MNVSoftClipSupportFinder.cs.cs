using Pisces.Domain.Interfaces;
using VariantPhasing.Models;
using VariantPhasing.Logic;
using System.Linq;
using Pisces.Domain.Options;
using Pisces.Domain.Models;
using Pisces.Calculators;
using VariantPhasing.Interfaces;
using Common.IO.Utility;

namespace VariantPhasing.Logic
{
    public class MNVSoftClipSupportFinder
    {
        private readonly IAlignmentExtractor _alignmentExtractor;
        private readonly IMNVClippedReadComparator _mnvClippedReadComparator;
        private readonly int _qNoiseLevel;
        private readonly int _maxQscore;
        private readonly int _minSizeForClipRescue;

        public MNVSoftClipSupportFinder(IAlignmentExtractor alignmentExtractor, IMNVClippedReadComparator mnvClippedReadComparator,
               int qNoiseLevel, int maxQscore, int minSizeForClipRescue)
        {
            _alignmentExtractor = alignmentExtractor;
            _mnvClippedReadComparator = mnvClippedReadComparator;
            _qNoiseLevel = qNoiseLevel;
            _maxQscore = maxQscore;
            _minSizeForClipRescue = minSizeForClipRescue;
        }

        public void SupplementSupportWithClippedReads(CallableNeighborhood neighborhood)
        {
            var neighbors = neighborhood.VcfVariantSites;
            var refName = neighbors.First().ReferenceName;
            _alignmentExtractor.Jump(refName);

            Logger.WriteToLog("Supplementing candidate variant support with soft clipped reads.");

            //var readFilter = new NeighborhoodReadFilter(_options);
            //var clippedReadComparator = new ClippedReadComparator();
            //var mnvClippedReadComparator = new MNVClippedReadComparator(scReadFilter);
            Read read = new Read();
            while (true)
            {
                if (!_alignmentExtractor.GetNextAlignment(read))
                {
                    break; // no more reads
                }

                // Check if clipped part matches alternate allele of any candidate variant
                foreach (var mnv in neighborhood.CandidateVariants)
                {
                    // Do not boost support for SNVs and short MNVs
                    if (mnv.ReferenceAllele.Length + mnv.AlternateAllele.Length < _minSizeForClipRescue)
                    {
                        continue;
                    }
                    if (_mnvClippedReadComparator.DoesClippedReadSupportMNV(read, mnv))
                    {
                        // Nima: in current implementation, same read can support multiple candidate variants. 
                        // In future we may want to "assign" reads to only one candidate variant.
                        // Risk: reads that support an MNV, may also support candidate variants. This can lead to false positives.
                        mnv.AlleleSupport++;
                        mnv.SoftClipAlleleSupport++;
                    }
                }

                if (read.Position > neighborhood.LastPositionOfInterestWithLookAhead)
                {
                    break;
                }

            }
            // Update Q score before moving on
            // Nima: Q score will be calculated twice for some variants 
            // (once in PhasedVariantExtractor.cs>Create() , and another time here)
            foreach (var mnv in neighborhood.CandidateVariants)
            {
                mnv.VariantQscore = VariantQualityCalculator.AssignPoissonQScore(mnv.AlleleSupport, mnv.ReferenceSupport, _qNoiseLevel, _maxQscore);
                Logger.WriteToLog("Added soft clip support of {0} to MNV: {1}.", mnv.AlleleSupport - mnv.SoftClipAlleleSupport, mnv.ToString());
            }
        }
    }
}
