using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Logic
{
    public class CandidateVariantFinder : ICandidateVariantFinder
    {
        protected readonly int _minimumBaseCallQuality;
        private readonly int _maxLengthMnv;
        private readonly int _maxLengthInterveningRef;
        private readonly bool _callMnvs;
        private readonly int _welllAnchoredAnchorSize;

        public CandidateVariantFinder(int qualityCutoff, int maxLengthMnv, int maxLengthInterveningRef, bool callMnvs, int welllAnchoredAnchorSize = 5)
        {
            _minimumBaseCallQuality = qualityCutoff;
            _maxLengthMnv = maxLengthMnv;
            _maxLengthInterveningRef = maxLengthInterveningRef;
            _callMnvs = callMnvs;
            _welllAnchoredAnchorSize = welllAnchoredAnchorSize;
        }

        public IEnumerable<CandidateAllele> FindCandidates(Read read, string refChromosome, string chromosomeName)
        {
            return ProcessCigarOps(read, refChromosome, read.Position, chromosomeName);
        }

        private IEnumerable<CandidateAllele> ProcessCigarOps(Read alignment, string refChromosome, int readStartPosition, string chromosomeName)
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

            try
            {
                Annotate(candidates, alignment);
            }
            catch
            {
                throw new Exception($"Failed to process variants for {alignment.Name} ... {alignment.CigarData}");
            }

            return candidates;
        }

        private static bool BasesMatch(char refBase, char readBase)
        {
            return refBase == readBase; // ref and read bases are always upper case
        }

        protected IEnumerable<CandidateAllele> ExtractSnvsFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            var candidateSingleNucleotideAlleles = new List<CandidateAllele>();
            var variantLengthSoFar = 0;
            var interveningRefLengthSoFar = 0;
            var openLeft = false;

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
                        interveningRefLengthSoFar, candidateSingleNucleotideAlleles, openLeft, true);  // note we ended because of poor quality, mark open ended
                    variantLengthSoFar = 0;
                    interveningRefLengthSoFar = 0;
                    openLeft = true;
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
                                interveningRefLengthSoFar, candidateSingleNucleotideAlleles, openLeft, false);
                            variantLengthSoFar = 0;
                            interveningRefLengthSoFar = 0;
                            openLeft = false;
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
                                candidateSingleNucleotideAlleles, openLeft, false);
                            variantLengthSoFar = 1;
                            interveningRefLengthSoFar = 0;
                            openLeft = false;
                        }
                    }
                }

            }
            //Flush if we've gotten to the end
            FlushVariant(alignment, refChromosome, opStartIndexInRead + ((int)operationLength) - variantLengthSoFar, 
                opStartIndexInReference + ((int)operationLength) - variantLengthSoFar, chromosomeName, variantLengthSoFar, 
                interveningRefLengthSoFar, candidateSingleNucleotideAlleles, openLeft, false);


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

        private void FlushVariant(Read alignment, string refChromosome, int variantStartIndexInRead, int variantStartIndexInReference, string chromosomeName, int variantLengthSoFar, int interveningRefLengthSoFar, 
            List<CandidateAllele> candidateSingleNucleotideAlleles, bool openLeft, bool openRight)
        {
            if (interveningRefLengthSoFar >= 1) //We tried to extend with refs but there was nothing to add after it. Need to pop that back out to ref calls.
            {
                //Adjust the mnv/rewind our index to account for the extra ref base(s) we just released
                variantLengthSoFar -= interveningRefLengthSoFar;
                openRight = false;
            }

            if (variantLengthSoFar >= 1)
            {
                //Adjust index into read/reference by moving back to the beginning of the candidate mnv
                var referenceBases = refChromosome.Substring(variantStartIndexInReference, variantLengthSoFar);
                var readBases = alignment.Sequence.Substring(variantStartIndexInRead, variantLengthSoFar);
                var candidate = CreateMnvSnv(alignment, variantStartIndexInRead, chromosomeName, variantStartIndexInReference, referenceBases, readBases);
                candidate.OpenOnLeft = openLeft;
                candidate.OpenOnRight = openRight;
                candidateSingleNucleotideAlleles.Add(candidate);
            }
        }

        private CandidateAllele CreateMnvSnv(Read alignment, int variantStartIndexInRead, string chromosomeName,
            int variantStartIndexInReference, string referenceBases, string readBases)
        {
            var variantLength = referenceBases.Length;

            var variant = variantLength > 1 ?
                Create(AlleleCategory.Mnv, chromosomeName, variantStartIndexInReference + 1, referenceBases, readBases, alignment, variantStartIndexInRead, _welllAnchoredAnchorSize)
                : Create(AlleleCategory.Snv, chromosomeName, variantStartIndexInReference + 1, referenceBases, readBases, alignment, variantStartIndexInRead, _welllAnchoredAnchorSize);

            return variant;
        }

        protected CandidateAllele ExtractInsertionFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            if (opStartIndexInReference - 1 >= refChromosome.Length || opStartIndexInReference == 0)
                return null;

            var referenceBases = refChromosome.Substring(opStartIndexInReference - 1, 1);
            var addedBases = alignment.Sequence.Substring(opStartIndexInRead, (int)operationLength);

            var chromosome = chromosomeName;
            var coordinate = opStartIndexInReference;  // coordinate is one base before actual variant
            var reference = referenceBases;
            var alternate = referenceBases + addedBases;

            //Do not create a variant if the quality isn't good enough
            var qualityGoodEnough = alignment.Qualities[opStartIndexInRead] >= _minimumBaseCallQuality;
            if (!qualityGoodEnough)
                return null;

            var candidate = Create(AlleleCategory.Insertion, chromosome, coordinate, reference, alternate, alignment, opStartIndexInRead, _welllAnchoredAnchorSize);

            return candidate;
        }

        protected CandidateAllele ExtractDeletionFromOperation(Read alignment, string refChromosome, int opStartIndexInRead, uint operationLength, int opStartIndexInReference, string chromosomeName)
        {
            if (opStartIndexInReference + operationLength >= refChromosome.Length)
                return null;

            var referenceBases = refChromosome.Substring(opStartIndexInReference - 1, (int)operationLength + 1);
            var readBases = refChromosome.Substring(opStartIndexInReference - 1, 1);


            //Do not create a variant if the qualities aren't good enough
            //GB: This logic  is correcting for the case where you have a deletion at the end of a read, but does that happen? Wouldn't that just be the end of the read... -- just defensive coding
            //TJD: We are seeing indels from non-std workflows, where the indel is the only elements in the cigar string. Presumably this is from hard clipping?
            //Currently we warn the user during the cigar validation step. But after that we should continue gracefully w/out
            //array out of bounds issues or otherwise throwing.
            bool qualitiesGoodEnough = CheckDeletionQuality(alignment, opStartIndexInRead, _minimumBaseCallQuality);

            if (!qualitiesGoodEnough) return null;
            var chromosome = chromosomeName;
            var coordinate = opStartIndexInReference;  // coordinate is one base before actual variant
            var reference = referenceBases;
            var alternate = readBases;

            var candidate = Create(AlleleCategory.Deletion, chromosome, coordinate, reference, alternate, alignment, opStartIndexInRead, _welllAnchoredAnchorSize);

            return candidate;
        }

        public static bool CheckDeletionQuality(Read alignment, int opStartIndexInRead, int MinimumBaseCallQuality)
        {

            //Defensive coding. If someone sends through a cigar string that is only a deletion, (ie, no bases and no q scores)
            //then we warn them (up stream of here,in the cigar validation step) 
            //and the Deletion itself will end up with a zero quality score.
            if (alignment.Qualities.Length == 0)
                return false;

            var qualityOfBaseAfterDel = ( opStartIndexInRead < alignment.Qualities.Length ) ? alignment.Qualities[opStartIndexInRead] : alignment.Qualities[opStartIndexInRead - 1];

            //set this just incase there is no preceding base.  defensive coding. 
            var qualityOfBaseBeforeDel = qualityOfBaseAfterDel;
            if (opStartIndexInRead > 0) //if we do have data before the indel, grab it.
            {
                qualityOfBaseBeforeDel = alignment.Qualities[opStartIndexInRead - 1];
            }

            //tjd: we tried using the average, but over all it hurt accuracy.
            //Keeping this line here so we dont try it again.
            //var qualitiesGoodEnough = ((qualityOfBaseBeforeDel + qualityOfBaseAfterDel) / 2.0) >= MinimumBaseCallQuality;

            //keeping stringent for deletions seems best.
            var qualitiesGoodEnough = ((qualityOfBaseBeforeDel >= MinimumBaseCallQuality) && (qualityOfBaseAfterDel >= MinimumBaseCallQuality));

            return qualitiesGoodEnough;
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
        /// <param name="wellAnchoredAnchorSize"></param>
        /// <returns></returns>
        public static CandidateAllele Create(AlleleCategory type, string chromosome, int coordinate, string reference,
            string alternate, Read alignment, int startIndexInRead, int wellAnchoredAnchorSize)
        {
            var candidate = new CandidateAllele(chromosome, coordinate, reference, alternate, type);
            var alleleSupportDirection = GetSupportDirection(candidate, alignment, startIndexInRead);
            candidate.SupportByDirection[(int)alleleSupportDirection]++;
            var anchor = Math.Min(coordinate - alignment.Position, alignment.EndPosition - coordinate);
            if (anchor > Math.Min(wellAnchoredAnchorSize - 1, alternate.Length -1))
            {
                candidate.WellAnchoredSupportByDirection[(int) alleleSupportDirection]++;
            }

            if (alignment.IsCollapsedRead())
            {
                ReadCollapsedType? collapsedType = alignment.GetReadCollapsedType(alleleSupportDirection);
                if(collapsedType.HasValue) // ignore non-proper read pairs
                {
                    switch (collapsedType.Value)
                    {
                        case ReadCollapsedType.DuplexNonStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.DuplexNonStitched]++;
                            break;
                        case ReadCollapsedType.DuplexStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.DuplexStitched]++;
                            break;
                        case ReadCollapsedType.SimplexStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexStitched]++;
                            break;
                        case ReadCollapsedType.SimplexReverseStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexStitched]++;
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexReverseStitched]++;
                            break;
                        case ReadCollapsedType.SimplexForwardStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexStitched]++;
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexForwardStitched]++;
                            break;
                        case ReadCollapsedType.SimplexNonStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexNonStitched]++;
                            break;
                        case ReadCollapsedType.SimplexReverseNonStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexNonStitched]++;
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexReverseNonStitched]++;
                            break;
                        case ReadCollapsedType.SimplexForwardNonStitched:
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexNonStitched]++;
                            candidate.ReadCollapsedCountsMut[(int) ReadCollapsedType.SimplexForwardNonStitched]++;
                            break;
                        default:
                            throw new Exception();
                    }
                }
            }
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
                return bamAlignment.SequencedBaseDirectionMap[startIndexInRead];

            var leftAnchorIndex = startIndexInRead - 1;
            var rightAnchorIndex = candidate.Type == AlleleCategory.Deletion ? startIndexInRead : startIndexInRead + candidate.Length;
            var lastIndex = bamAlignment.Sequence.Length - 1;

            if (rightAnchorIndex == 0)
            {
                return bamAlignment.SequencedBaseDirectionMap[rightAnchorIndex];
            }
            if (leftAnchorIndex == lastIndex) // this should only happen for deletion at the end
            {
                return bamAlignment.SequencedBaseDirectionMap[lastIndex];
            }

            if (leftAnchorIndex == rightAnchorIndex - 1)  // for deletions
            {
               
                if (bamAlignment.CigarDirections != null)
                {
                    return GetDeletionDirectionForStitchedRead(bamAlignment, leftAnchorIndex, rightAnchorIndex);
                }

                //fall back to old method if we were not tracking directions "inside" deletions.
                var startDirection = bamAlignment.SequencedBaseDirectionMap[leftAnchorIndex];
                var endDirection = bamAlignment.SequencedBaseDirectionMap[rightAnchorIndex];

                //GB: why do we preferentially do this, below?
                //TD: I believe its arbitrary and also it is what we always used to do.So any changes here would have unknown affects. I think the original point was, if all else fails, at least be predictable.
                return startDirection == DirectionType.Stitched ? endDirection : startDirection;
            }

            DirectionType direction = DirectionType.Forward;
            // otherwise, walk through and if any direction between anchor points is stitched, return stitched
            for (var i = leftAnchorIndex + 1; i < rightAnchorIndex; i ++)
            {
                direction = bamAlignment.SequencedBaseDirectionMap[i];
                if (direction == DirectionType.Stitched)
                    return DirectionType.Stitched;
            }

            // sanity check that we didn't magically transition from F/R or R/F without stitched region
            if (bamAlignment.SequencedBaseDirectionMap[leftAnchorIndex + 1] != bamAlignment.SequencedBaseDirectionMap[rightAnchorIndex - 1])
                throw new InvalidDataException("Alignment error: Found change in direction without encountering stitched direction");

            return direction;
        }



        /// <summary>
        /// Given the read (ie ACGT, where there might be a length-2 deletion inside it, so it is really ACXXGT), and the anchor points (ie,
        /// for A-C-X-X-G-T, which have sequenced-read indexes 0-1-X-X-2-3, the deletion anchor indexes would be 1 and 2), return the DIRECTION
        /// of the deletion.  Note the two deleted bases (XX) are not sequenced and do not have indexes in the sequenced read.  
        /// To get the direction for the deletion, we need to know the directions of the extended read. Ie, 0-1-X-X-2-3 (sequenced) is 0-1-2-3-4-5 (expanded).
        /// The expanded directions might be F-F-S-S-S-R, and it is those directions inside the deletion (expanded index 2 and 3) that we care about for this method.
        /// </summary>
        /// <param name="bamAlignment"></param>
        /// <param name="leftAnchorIndexInSequencedRead">The left anchor point of the deletion (ie,
        /// for A-C-X-X-G-T, which have sequenced-read indexes 0-1-X-X-2-3, the left anchor index would be 1)</param>
        /// <param name="rightAnchorIndexInSequencedRead">The right anchor point of the deletion (ie,
        /// for A-C-X-X-G-T, which have sequenced-read indexes 0-1-X-X-2-3, the right anchor index would be 2)</param>
        /// <returns></returns>
        public static DirectionType GetDeletionDirectionForStitchedRead(Read bamAlignment, int leftAnchorIndexInSequencedRead, int rightAnchorIndexInSequencedRead)
        {
            var expandedBaseDirectionMap = bamAlignment.CigarDirections.Expand();

            var anchorIndexesInExpandedCoordinates=  bamAlignment.SequencedIndexesToExpandedIndexes(
                new int[] { leftAnchorIndexInSequencedRead, rightAnchorIndexInSequencedRead} );

            var indexOfFirstDeletedBaseInExpandedSequenceOfDirections = anchorIndexesInExpandedCoordinates[0] + 1; //one step inside deletion from the left
            var indexOfLastDeletedBaseInExpandedSequenceOfDirections = anchorIndexesInExpandedCoordinates[1] - 1;  //one step inside deletion from the right                

            //Check array bounds before updating the values. I dont think this would ever fail in practice. 
            if ((indexOfFirstDeletedBaseInExpandedSequenceOfDirections >= 0) && (indexOfFirstDeletedBaseInExpandedSequenceOfDirections < expandedBaseDirectionMap.Count) &&
                (indexOfLastDeletedBaseInExpandedSequenceOfDirections >= 0) && (indexOfLastDeletedBaseInExpandedSequenceOfDirections < expandedBaseDirectionMap.Count))
            {
                var startDirection = expandedBaseDirectionMap[indexOfFirstDeletedBaseInExpandedSequenceOfDirections];
                var endDirection = expandedBaseDirectionMap[indexOfLastDeletedBaseInExpandedSequenceOfDirections];

                //GB: why do we preferentially do this, below?
                //TD: I believe its arbitrary and also it is what we always used to do.So any changes here would have unknown affects. I think the original point was, if all else fails, at least be predictable.
                return (startDirection == DirectionType.Stitched) ? endDirection : startDirection;
            }
            else
            {
                //Sanity check. If we get here, we were fed a very strange alignement with odd (pathological?) cigar string /  XD tag that we cannot interpret.
                throw new Exception("Unable to find direction info for deletion. Indexing issue for deletion inside stitched read. Check " + bamAlignment.Name);
            }
        }

      
        /// <summary>
        /// Annotate candidates with whether or not they are open ended.  Candidates can only be open ended if they are at the start or end of the read (no soft clipping).
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="read"></param>
        private void Annotate(List<CandidateAllele> candidates, Read read)
        {
            if (candidates.Count == 0)
                return;

            var firstOperation = read.CigarData[0];
            var lastOperation = read.CigarData[read.CigarData.Count - 1];

            if (firstOperation.Type == 'S')
                firstOperation = read.CigarData[1];

            if (lastOperation.Type == 'S')
                lastOperation = read.CigarData[read.CigarData.Count - 2];

            var maxPosition = read.PositionMap.Max();
            if (maxPosition == -1)
                maxPosition = read.Position - 1; // if no anchor at all to reference, set to one past position

            switch (firstOperation.Type)
            {
                case 'M':
                    // find any snv or mnv that start at first position
                    AnnotateOpen(candidates, true, c =>
                        c.ReferencePosition == read.Position &&
                        (c.Type == AlleleCategory.Mnv || c.Type == AlleleCategory.Snv));
                    break;
                case 'I':
                    AnnotateOpen(candidates, true, c =>
                        c.ReferencePosition == read.Position - 1 && // position reported base before
                        c.Type == AlleleCategory.Insertion);
                    break;
                case 'D':
                    AnnotateOpen(candidates, true, c =>
                        c.ReferencePosition == read.Position - 1 && // position reported base before
                        c.Type == AlleleCategory.Deletion);
                    break;
            }

            switch (lastOperation.Type)
            {
                case 'M':
                    // find any snv or mnv that end in last position of read
                    AnnotateOpen(candidates, false, c =>
                        c.ReferencePosition + c.AlternateAllele.Length - 1 == maxPosition &&
                        (c.Type == AlleleCategory.Mnv || c.Type == AlleleCategory.Snv));
                    break;
                case 'I':
                    AnnotateOpen(candidates, false, c =>
                        c.ReferencePosition == maxPosition && 
                        c.Type == AlleleCategory.Insertion);
                    break;
                case 'D':
                    AnnotateOpen(candidates, false, c =>
                        c.ReferencePosition == maxPosition &&
                        c.Type == AlleleCategory.Deletion);
                    break;
            }
        }

        private void AnnotateOpen(List<CandidateAllele> candidates, bool openOnLeft, Func<CandidateAllele, bool> criteria)
        {
            for (var i = 0; i < candidates.Count; i ++)
            {
                var candidate = candidates[i];

                if (criteria(candidate))
                {
                    if (openOnLeft)
                        candidate.OpenOnLeft = true;
                    else
                    {
                        candidate.OpenOnRight = true;
                    }
                }
            }
        }
    }
}
