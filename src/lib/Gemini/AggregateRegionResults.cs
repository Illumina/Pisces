using System.Collections.Generic;
using Alignment.Domain.Sequencing;

namespace Gemini
{
    public class AggregateRegionResults
    {
        public List<BamAlignment> AlignmentsReadyToBeFlushed;
        public EdgeState EdgeState = new EdgeState();
    }
}