using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.Models;

namespace Gemini.Realignment
{
    public interface IRealignmentEvaluator
    {
        BamAlignment GetFinalAlignment(BamAlignment origBamAlignment, out bool changed, out bool forcedSoftclip, List<PreIndel> selectedIndels = null);
    }
}