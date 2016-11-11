using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Interfaces;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Processing.Interfaces;
using Pisces.Processing.Utility;

namespace Pisces.Logic
{
    public class SomaticVariantCaller : ISomaticVariantCaller
    {
        private readonly IAlignmentSource _alignmentSource;
        private readonly ICandidateVariantFinder _variantFinder;
        private readonly IAlleleCaller _alleleCaller;
        private readonly IVcfWriter<CalledAllele> _vcfWriter;
        private readonly IStateManager _stateManager;
        private readonly ChrReference _chrReference;
        private readonly IRegionMapper _regionMapper;
        private readonly IStrandBiasFileWriter _biasFileWriter;
        private readonly ChrIntervalSet _intervalSet;

        public SomaticVariantCaller(IAlignmentSource alignmentSource, ICandidateVariantFinder variantFinder, IAlleleCaller alleleCaller, 
            IVcfWriter<CalledAllele> vcfWriter, IStateManager stateManager, ChrReference chrReference, IRegionMapper regionMapper, 
            IStrandBiasFileWriter biasFileWriter, ChrIntervalSet intervalSet = null)
        {
            _alignmentSource = alignmentSource;
            _variantFinder = variantFinder;
            _alleleCaller = alleleCaller;
            _vcfWriter = vcfWriter;
            _stateManager = stateManager;
            _chrReference = chrReference;
            _regionMapper = regionMapper;
            _biasFileWriter = biasFileWriter;
            _intervalSet = intervalSet;
        }

        public void Execute()
        {
            Read read;

            if (_alignmentSource.SourceIsStitched)
                Logger.WriteToLog("Stitched reads detected");

            while ((read = _alignmentSource.GetNextRead()) != null)
            {
                // find candidate variants
                var candidateVariants = _variantFinder.FindCandidates(read, _chrReference.Sequence,
                    _chrReference.Name);

                // track in state manager
                _stateManager.AddCandidates(candidateVariants);
                _stateManager.AddAlleleCounts(read);

                // call anything possible to call
                Call(_alignmentSource.LastClearedPosition);
            }

            Call(); // call everything left

            if (_regionMapper != null)
                _vcfWriter.WriteRemaining(_regionMapper);  // pad any remaining intervals if necessary

            Logger.WriteToLog("Totals: {0} alleles called.  {1} variants collapsed.", 
                _alleleCaller.TotalNumCalled, _alleleCaller.TotalNumCollapsed);
        }

        private void Call(int? upToPosition = null)
        {
            var readyBatch = _stateManager.GetCandidatesToProcess(upToPosition, _chrReference);

            if (readyBatch == null)
                return;

            if (readyBatch.HasCandidates)
            {
                // evaluate and call variants (including ref calls if producing gvcf)
                var BaseCalledAllelesByPosition = _alleleCaller.Call(readyBatch, _stateManager);

                var BaseCalledAlleles = BaseCalledAllelesByPosition.Values.SelectMany(a => a).ToList();

                // write to vcf
                _vcfWriter.Write(BaseCalledAlleles, _regionMapper);

                if (_biasFileWriter != null)
                {
                    _biasFileWriter.Write(BaseCalledAlleles);
                }
            }

            if (_intervalSet != null && readyBatch.MaxClearedPosition.HasValue)
                _intervalSet.SetCleared(readyBatch.MaxClearedPosition.Value);

            _stateManager.DoneProcessing(readyBatch);
        }
    }
}