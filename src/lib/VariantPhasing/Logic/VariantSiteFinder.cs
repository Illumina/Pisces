using System.Collections.Generic;
using Pisces.Domain.Logic;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using VariantPhasing.Models;
using VariantPhasing.Types;

namespace VariantPhasing.Logic
{
    public class VariantSiteFinder : CandidateVariantFinder
    {
        public VariantSiteFinder(int qualityCutoff)
            : base(qualityCutoff, 0, 0, true)
        {
        }

        public Dictionary<SubsequenceType, List<VariantSite>> FindVariantSites(Read read, string chromosomeName)
        {
            var pseudoRefChromosome = new string('R', (int) read.CigarData.GetReferenceSpan() + 100);
            var dict = new Dictionary<SubsequenceType, List<VariantSite>>()            
            {
                {SubsequenceType.DeletionSequence, new List<VariantSite>()},
                {SubsequenceType.MatchOrMismatchSequence, new List<VariantSite>()},
                {SubsequenceType.InsertionSquence, new List<VariantSite>()}
            };

            var startIndexInRead = 0;
            var startIndexInPseudoReference = 1;

            for (var cigarOpIndex = 0; cigarOpIndex < read.CigarData.Count; cigarOpIndex++)
            {
                var operation = read.CigarData[cigarOpIndex];
                CandidateAllele candidate = null;
                var type = SubsequenceType.MatchOrMismatchSequence;
                var found = false;
                switch (operation.Type)
                {
                    case 'I':
                        candidate = ExtractInsertionFromOperation(read, pseudoRefChromosome, startIndexInRead, operation.Length, startIndexInPseudoReference, chromosomeName);
                        type = SubsequenceType.InsertionSquence;
                        found = true;
                        break;
                    case 'D':
                        candidate = ExtractDeletionFromOperation(read, pseudoRefChromosome, startIndexInRead, operation.Length, startIndexInPseudoReference, chromosomeName);
                        type = SubsequenceType.DeletionSequence;
                        found = true;
                        break;
                    case 'M':
                        candidate = ExtractGappedMnv(read, pseudoRefChromosome, startIndexInRead, (int)operation.Length,
                            startIndexInPseudoReference, chromosomeName);
                        type = SubsequenceType.MatchOrMismatchSequence;
                        found = true;
                        break;
                }

                if (found)
                {
                    dict[type].Add(MapCandidateVariant(read.Position, candidate, candidate == null, startIndexInPseudoReference));
                }

                if (operation.IsReadSpan())
                    startIndexInRead += (int)operation.Length;

                if (operation.IsReferenceSpan())
                    startIndexInPseudoReference += (int)operation.Length;
            }

            return dict;
        }

        private CandidateAllele ExtractGappedMnv(Read alignment, string refChromosome, int opStartIndexInRead, int operationLength, int opStartIndexInReference, 
            string chromosomeName)
        {
            var readseq = "";
            for (var i = 0; i < operationLength; i++)
            {
                var qualityGoodEnough = alignment.Qualities[opStartIndexInRead + i] >= _minimumBaseCallQuality;

                var readBase = !qualityGoodEnough ? 'N' : alignment.Sequence[opStartIndexInRead + i];
                readseq += readBase;

                if (opStartIndexInReference + i >= refChromosome.Length)
                    break;
            }
            
            var referenceBases = refChromosome.Substring(opStartIndexInReference + 1, operationLength);

            return Create(AlleleCategory.Mnv, chromosomeName, opStartIndexInReference + 1, referenceBases, readseq, alignment,
                opStartIndexInRead, 0);

        }

        private VariantSite MapCandidateVariant(int readPosition, CandidateAllele allele, bool failed, int refPosition)
        {
            var positionAdjustment = failed ? refPosition : allele.ReferencePosition;

            var position = readPosition - 2 + positionAdjustment;

            return new VariantSite(position)
            {
                VcfReferenceAllele = failed ? "N" : allele.ReferenceAllele, 
                VcfAlternateAllele = failed ? "N" : allele.AlternateAllele
            };

        }

    }
}