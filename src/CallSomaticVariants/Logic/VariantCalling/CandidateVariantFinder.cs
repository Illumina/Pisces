using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CallSomaticVariants.Interfaces;
using CallSomaticVariants.Models;
using CallSomaticVariants.Models.Alleles;
using CallSomaticVariants.Types;

namespace CallSomaticVariants.Logic.VariantCalling
{
    public class CandidateVariantFinder : ICandidateVariantFinder
    {
        private readonly int _minimumBaseCallQuality;
        private readonly int _maxLengthMnv;
        private readonly int _maxLengthInterveningRef;
        private readonly bool _callMnvs;

        public CandidateVariantFinder(int qualityCutoff, int maxLengthMnv, int maxLengthInterveningRef, bool callMnvs)
        {
            _minimumBaseCallQuality = qualityCutoff;
            _maxLengthMnv = maxLengthMnv;
            _maxLengthInterveningRef = maxLengthInterveningRef;
            _callMnvs = callMnvs;
        }

        public IEnumerable<CandidateAllele> FindCandidates(AlignmentSet alignmentSet, string refChromosome, string chromosomeName)
        {
            var candidates = new List<CandidateAllele>();
            foreach (var bamAlignment in alignmentSet.ReadsForProcessing)
            {
                candidates.AddRange(ProcessCigarOps(bamAlignment, refChromosome, bamAlignment.Position, chromosomeName));
            }
            return candidates;
        }

        private IEnumerable<CandidateAllele> ProcessCigarOps(Read alignment, string refChromosome, int readStartPosition,string chromosomeName)
        {
            var candidates = new List<CandidateAllele>();

            int startIndexInRead = 0;
            int startIndexInReference = readStartPosition - 1;

            for (var cigarOpIndex = 0; cigarOpIndex < alignment.CigarData.Count; cigarOpIndex++)
            {
                var operation = alignment.CigarData[cigarOpIndex];
                switch (operation.Type)
                {
                    case 'S': // soft-clip
                        break;
                    case 'M': //match or mismatch
                        var operationSnvs = ExtractSnvsFromOperation(alignment, refChromosome, startIndexInRead, operation.Length, startIndexInReference, chromosomeName);
                        candidates.AddRange(operationSnvs);
                        break;
                    case 'I': //inesrtion
                        var insertion = ExtractInsertionFromOperation(alignment, refChromosome, startIndexInRead, operation.Length, startIndexInReference, chromosomeName);
                        if (insertion != null)
                            candidates.Add(insertion);
                        break;
                    case 'D':
                        var deletion = ExtractDeletionFromOperation(alignment, refChromosome, startIndexInRead, operation.Length, startIndexInReference, chromosomeName);
                        if (deletion != null)
                            candidates.Add(deletion);
                        break;
                }


                if (operation.IsReadSpan())
                    startIndexInRead += (int)operation.Length;

                if (operation.IsReferenceSpan())
                    startIndexInReference += (int)operation.Length;
            }

            return candidates;
        }

        private static bool BasesMatch(char refBase, char readBase)
        {
            return refBase == readBase; // ref and read bases are always upper case
        }

        private IEnumerable<CandidateAllele> ExtractSnvsFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            var candidateSingleNucleotideAlleles = new List<CandidateAllele>();
            var variantLengthSoFar = 0;
            var interveningRefLengthSoFar = 0;

            for (var i = 0; i < operationLength; i++)
            {
                var qualityGoodEnough = alignment.Qualities[opStartIndexInRead + i] >= _minimumBaseCallQuality;

                var readBase = alignment.Sequence[opStartIndexInRead + i];
                if (opStartIndexInReference + i >= refChromosome.Length)
                    break;
                var refBase = refChromosome[opStartIndexInReference + i];

                var atEndOfOperation = i == (operationLength - 1);
                var startingMnvAtEndOfOperation = (atEndOfOperation && variantLengthSoFar == 0);

                //Do not create/extend a variant if the quality isn't good enough or the allele or ref is an N
                if ((AlleleHelper.GetAlleleType(readBase) == AlleleType.N) || (AlleleHelper.GetAlleleType(refBase) == AlleleType.N) || !qualityGoodEnough)
                {
                    FlushVariant(alignment, refChromosome, opStartIndexInRead + i - variantLengthSoFar,
                        opStartIndexInReference + i - variantLengthSoFar, chromosomeName, variantLengthSoFar,
                        interveningRefLengthSoFar, candidateSingleNucleotideAlleles);
                    variantLengthSoFar = 0;
                    interveningRefLengthSoFar = 0;
                }
                else
                {
                    if (BasesMatch(refBase, readBase))
                    {
                        if (ShouldBuildUpMNV(variantLengthSoFar, interveningRefLengthSoFar, true) &&
                            !startingMnvAtEndOfOperation) //Don't build up an MNV if we're on the last base of operation
                        {
                            variantLengthSoFar++;
                            interveningRefLengthSoFar++;
                        }
                        else
                        {
                            FlushVariant(alignment, refChromosome, opStartIndexInRead + i - variantLengthSoFar,
                                opStartIndexInReference + i - variantLengthSoFar, chromosomeName, variantLengthSoFar,
                                interveningRefLengthSoFar, candidateSingleNucleotideAlleles);
                            variantLengthSoFar = 0;
                            interveningRefLengthSoFar = 0;
                        }
                    }
                    else
                    {
                        if (ShouldBuildUpMNV(variantLengthSoFar, interveningRefLengthSoFar, false) &&
                            !startingMnvAtEndOfOperation) //Don't build up an MNV if we're on the last base of operation
                        {
                            variantLengthSoFar++;
                            interveningRefLengthSoFar = 0;
                        }
                        else
                        {
                            FlushVariant(alignment, refChromosome, opStartIndexInRead + i - variantLengthSoFar,
                                opStartIndexInReference + i - variantLengthSoFar,
                                chromosomeName, variantLengthSoFar, interveningRefLengthSoFar,
                                candidateSingleNucleotideAlleles);
                            variantLengthSoFar = 1;
                            interveningRefLengthSoFar = 0;
                        }
                    }
                }

            }
            //Flush if we've gotten to the end
            FlushVariant(alignment, refChromosome, opStartIndexInRead + ((int)operationLength) - variantLengthSoFar, opStartIndexInReference + ((int)operationLength) - variantLengthSoFar, chromosomeName, variantLengthSoFar, interveningRefLengthSoFar, candidateSingleNucleotideAlleles);


            return candidateSingleNucleotideAlleles;
        }

        private bool ShouldBuildUpMNV(int mnvLengthSoFar, int interveningRefLengthSoFar, bool refCallNext)
        {
            if (!_callMnvs) return false;

            if (refCallNext && mnvLengthSoFar == 0) return false; //Don't start with a ref call

            if ((mnvLengthSoFar + 1) > _maxLengthMnv) return false;

            if ((interveningRefLengthSoFar + (refCallNext ? 1 : 0)) > _maxLengthInterveningRef) return false;

            return true;
        }

        private void FlushVariant(Read alignment, string refChromosome, int variantStartIndexInRead, int variantStartIndexInReference, string chromosomeName, int variantLengthSoFar, int interveningRefLengthSoFar, List<CandidateAllele> candidateSingleNucleotideAlleles)
        {
            if (interveningRefLengthSoFar >= 1) //We tried to extend with refs but there was nothing to add after it. Need to pop that back out to ref calls.
            {
                //Adjust the mnv/rewind our index to account for the extra ref base(s) we just released
                variantLengthSoFar -= interveningRefLengthSoFar;
            }


            if (variantLengthSoFar >= 1)
            {
                //Adjust index into read/reference by moving back to the beginning of the candidate mnv
                var referenceBases = refChromosome.Substring(variantStartIndexInReference, variantLengthSoFar);
                var readBases = alignment.Sequence.Substring(variantStartIndexInRead, variantLengthSoFar);
                var candidate = CreateMnvSnv(alignment, variantStartIndexInRead, chromosomeName, variantStartIndexInReference, referenceBases, readBases);
                candidateSingleNucleotideAlleles.Add(candidate);
            }
        }

        private CandidateAllele CreateMnvSnv(Read alignment, int variantStartIndexInRead, string chromosomeName,
            int variantStartIndexInReference, string referenceBases, string readBases)
        {
            var variantLength = referenceBases.Length;

            var variant = variantLength > 1 ?
                Create(AlleleCategory.Mnv, chromosomeName, variantStartIndexInReference + 1, referenceBases, readBases, alignment, variantStartIndexInRead)
                : Create(AlleleCategory.Snv, chromosomeName, variantStartIndexInReference + 1, referenceBases, readBases, alignment, variantStartIndexInRead);

            return variant;
        }

        private CandidateAllele ExtractInsertionFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            if (opStartIndexInReference - 1 >= refChromosome.Length)
                return null;

            var referenceBases = refChromosome.Substring(opStartIndexInReference - 1, 1);
            var addedBases = alignment.Sequence.Substring(opStartIndexInRead, (int) operationLength);

            var chromosome = chromosomeName;
            var coordinate = opStartIndexInReference;  // coordinate is one base before actual variant
            var reference = referenceBases;
            var alternate = referenceBases + addedBases;

            //Do not create a variant if the quality isn't good enough
            var qualityGoodEnough = alignment.Qualities[opStartIndexInRead] >= _minimumBaseCallQuality;
            if (!qualityGoodEnough)
                return null;

            var candidate = Create(AlleleCategory.Insertion, chromosome, coordinate, reference, alternate, alignment, opStartIndexInRead);

            return candidate;
        }

        private CandidateAllele ExtractDeletionFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            if (opStartIndexInReference + operationLength >= refChromosome.Length)
                return null;

            var referenceBases = refChromosome.Substring(opStartIndexInReference - 1, (int)operationLength + 1);
            var readBases = refChromosome.Substring(opStartIndexInReference - 1, 1);


            //Do not create a variant if the qualities aren't good enough
            var qualityOfBaseAfterDel = opStartIndexInRead < alignment.Qualities.Length ? alignment.Qualities[opStartIndexInRead] : alignment.Qualities[opStartIndexInRead - 1];
            //tjd+: set this just incase there is no preceding base.  some aligners allow deletions at the beginning of the read.. 
            var qualityOfBaseBeforeDel = qualityOfBaseAfterDel;
            if (opStartIndexInRead > 0) //if we do have data before the indel, grab it.
            {
                qualityOfBaseBeforeDel = alignment.Qualities[opStartIndexInRead - 1];
            }
            var qualitiesGoodEnough = (qualityOfBaseBeforeDel >= _minimumBaseCallQuality) &&
                                      (qualityOfBaseAfterDel >= _minimumBaseCallQuality);

            if (!qualitiesGoodEnough) return null;
            var chromosome = chromosomeName;
            var coordinate = opStartIndexInReference;  // coordinate is one base before actual variant
            var reference = referenceBases;
            var alternate = readBases;

            var candidate = Create(AlleleCategory.Deletion, chromosome, coordinate, reference, alternate, alignment, opStartIndexInRead);

            return candidate;
        }

        /// <summary>
        /// Create a new candidate allele from alignment and add support.  Exposed only for unit testing.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="chromosome"></param>
        /// <param name="coordinate"></param>
        /// <param name="reference"></param>
        /// <param name="alternate"></param>
        /// <param name="alignment"></param>
        /// <param name="startIndexInRead"></param>
        /// <returns></returns>
        public static CandidateAllele Create(AlleleCategory type, string chromosome, int coordinate, string reference,
            string alternate, Read alignment, int startIndexInRead)
        {
            var candidate = new CandidateAllele(chromosome, coordinate, reference, alternate, type);
            candidate.SupportByDirection[(int)GetSupportDirection(candidate, alignment, startIndexInRead)]++;

            return candidate;
        }

        /// <summary>
        /// Get the support direction at a particular point in a read, for a given candidate.  Exposed only for unit testing.
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="bamAlignment"></param>
        /// <param name="startIndexInRead"></param>
        /// <returns></returns>
        public static DirectionType GetSupportDirection(CandidateAllele candidate, Read bamAlignment, int startIndexInRead) 
        {
            if (candidate.Type == AlleleCategory.Snv || candidate.Type == AlleleCategory.Reference)
                return bamAlignment.DirectionMap[startIndexInRead];

            var leftAnchorIndex = 0;
            var rightAnchorIndex = 0;

            if (candidate.Type == AlleleCategory.Deletion)
            {
                // deletion has no anchor in stitched read, use base before and after (if at the beginning or end of read, there's only one anchor; use that as value for both sides)
                if (startIndexInRead > 0)
                {
                    leftAnchorIndex = startIndexInRead - 1;
                    rightAnchorIndex = startIndexInRead > bamAlignment.Sequence.Length - 1 ? leftAnchorIndex : startIndexInRead;
                }
                else //we're at the beginning of the read. just take the rightAnchorIndex for both
                {
                    rightAnchorIndex = startIndexInRead;
                    leftAnchorIndex = rightAnchorIndex;
                }
            }
            else if (candidate.Type == AlleleCategory.Mnv)
            {
                leftAnchorIndex = startIndexInRead;
                rightAnchorIndex = startIndexInRead + candidate.Alternate.Length - 1;
            }
            else if (candidate.Type == AlleleCategory.Insertion)
            {
                // insertion has no anchor on reference, use base before and after (if at the beginning or end of read, there's only one anchor; use that as value for both sides)
                if (startIndexInRead > 0)
                {
                    leftAnchorIndex = startIndexInRead - 1;
                    var indexAfterInsertion = startIndexInRead + (candidate.Alternate.Length - candidate.Reference.Length);
                    rightAnchorIndex = indexAfterInsertion > bamAlignment.Sequence.Length - 1 ? leftAnchorIndex : indexAfterInsertion; 
                }
                else //we're at the beginning of the read. just take the rightAnchorIndex for both
                {
                    rightAnchorIndex = startIndexInRead + (candidate.Alternate.Length - candidate.Reference.Length);
                    leftAnchorIndex = rightAnchorIndex;
                } 

            }
            var startDirection = bamAlignment.DirectionMap[leftAnchorIndex];
            var endDirection = bamAlignment.DirectionMap[rightAnchorIndex];

            DirectionType direction;

            //Deletion must be surrounded by stitched area for it to be considered stitched
            //Insertion and MNV needs only one end to be in stitched region to be considered stitched
            if (candidate.Type == AlleleCategory.Deletion)
            {
                if (startDirection == DirectionType.Stitched &&
                    endDirection == DirectionType.Stitched)
                {
                    direction = DirectionType.Stitched;
                }
                else if (startDirection == DirectionType.Forward || endDirection == DirectionType.Forward)
                {
                    direction = DirectionType.Forward;
                }
                else
                {
                    direction = DirectionType.Reverse;
                }
            }
            else
            {
                if (startDirection == DirectionType.Stitched ||
                endDirection == DirectionType.Stitched)
                {
                    direction = DirectionType.Stitched;
                }
                //TODO - the below is essentially the existing Pisces logic of taking the max of the preceding and trailing, I don't know if I like this for determining between forward and reverse
                else if (startDirection == DirectionType.Reverse || endDirection == DirectionType.Reverse)
                {
                    direction = DirectionType.Reverse;
                }
                else
                {
                    direction = DirectionType.Forward;
                }
            }

            return direction;
        }

    }
}
