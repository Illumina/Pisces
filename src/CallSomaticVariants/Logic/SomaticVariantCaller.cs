using System;
using System.Linq;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Logic;
using CallSomaticVariants.Logic.RegionState;
using CallSomaticVariants.Models;
using CallSomaticVariants.Types;
using CallSomaticVariants.Utility;

namespace CallSomaticVariants
{
    public class SomaticVariantCaller : ISomaticVariantCaller
    {
        private readonly IAlignmentSource _alignmentSource;
        private readonly ICandidateVariantFinder _variantFinder;
        private readonly IAlleleCaller _alleleCaller;
        private readonly IVcfWriter _vcfWriter;
        private readonly IStateManager _stateManager;
        private readonly ChrReference _chrReference;
        private readonly IRegionPadder _regionMapper;
        private int _numCalledAlleles;
        private readonly IStrandBiasFileWriter _biasFileWriter;

        public SomaticVariantCaller(IAlignmentSource alignmentSource, ICandidateVariantFinder variantFinder, IAlleleCaller alleleCaller, 
            IVcfWriter vcfWriter, IStateManager stateManager, ChrReference chrReference, IRegionPadder regionMapper, IStrandBiasFileWriter biasFileWriter)
        {
            _alignmentSource = alignmentSource;
            _variantFinder = variantFinder;
            _alleleCaller = alleleCaller;
            _vcfWriter = vcfWriter;
            _stateManager = stateManager;
            _chrReference = chrReference;
            _regionMapper = regionMapper;
            _biasFileWriter = biasFileWriter;

            if (_alignmentSource.ChromosomeFilter != _chrReference.Name)
            {
                throw new ArgumentException(string.Format("Chromosome filter in alignment source '{0}' does not match to current chromosome '{1}'",_alignmentSource.ChromosomeFilter, _chrReference.Name));
            }
        }

        public void Execute()
        {
            AlignmentSet alignmentSet;
            while ((alignmentSet = _alignmentSource.GetNextAlignmentSet()) != null)
            {
                // find candidate variants
                var candidateVariants = _variantFinder.FindCandidates(alignmentSet, _chrReference.Sequence,
                    _chrReference.Name);

                // track in state manager
                _stateManager.AddCandidates(candidateVariants);
                _stateManager.AddAlleleCounts(alignmentSet);

                // call anything possible to call
                Call(_alignmentSource.LastClearedPosition);
            }

            Call(); // call everything left

            Logger.WriteToLog("Totals: {0} alleles called.", _numCalledAlleles);
        }

        private void Call(int? upToPosition = null)
        {
            var readyBatch = _stateManager.GetCandidatesToProcess(upToPosition, _chrReference);

            // map to intervals if needed
            if (_regionMapper != null)
                _regionMapper.Pad(readyBatch, !upToPosition.HasValue);

            if (readyBatch.HasCandidates)
            {
                // evaluate and call variants (including ref calls if producing gvcf)
                var calledVariants = _alleleCaller.Call(readyBatch, _stateManager);

                _numCalledAlleles += calledVariants.Count();

                // write to vcf
                _vcfWriter.Write(calledVariants);

                if (_biasFileWriter != null)
                {
                    _biasFileWriter.Write(calledVariants);                    
                }
            }

            _stateManager.DoneProcessing(readyBatch);
        }
    }
}