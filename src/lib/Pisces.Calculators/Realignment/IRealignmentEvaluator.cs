using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.Models;
using ReadRealignmentLogic.Models;

namespace Gemini.Realignment
{
    public interface IRealignmentEvaluator
    {
        BamAlignment GetFinalAlignment(BamAlignment origBamAlignment, out bool changed, out bool forcedSoftclip, out bool confirmed, out bool sketchy,
            List<PreIndel> selectedIndels = null, List<PreIndel> existingIndels = null,
            bool assumeImperfect = true, List<HashableIndel> confirmedAccepteds = null, List<PreIndel> mateIndels = null, RealignmentState state = null);

        Dictionary<HashableIndel, int[]> GetIndelOutcomes();
    }
}