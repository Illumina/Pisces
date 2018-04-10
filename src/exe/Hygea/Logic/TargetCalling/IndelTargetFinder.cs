using System;
using System.Collections.Generic;
using RealignIndels.Interfaces;
using RealignIndels.Models;
using Pisces.Domain.Models;
using Pisces.Domain.Logic;

namespace RealignIndels.Logic.TargetCalling
{
    public class IndelTargetFinder : CandidateVariantFinder, IIndelCandidateFinder
    {
        public IndelTargetFinder(int qualityCutoff) : base(qualityCutoff, 0, 0, false)
        {
        }

        public List<CandidateIndel> FindIndels(Read read, string refChromosome, string chromosomeName)
        {
            var candidates = new List<CandidateIndel>();

            int startIndexInRead = 0;
            int startIndexInReference = read.Position - 1;

            for (var cigarOpIndex = 0; cigarOpIndex < read.CigarData.Count; cigarOpIndex++)
            {
                var operation = read.CigarData[cigarOpIndex];
                switch (operation.Type)
                {
                    case 'I': 
                        var insertion = ExtractInsertionFromOperation(read, refChromosome, startIndexInRead, operation.Length, startIndexInReference, chromosomeName);
                        if (insertion != null)
                            candidates.Add(new CandidateIndel(insertion));
                        break;
                    case 'D':
                        var deletion = ExtractDeletionFromOperation(read, refChromosome, startIndexInRead, operation.Length, startIndexInReference, chromosomeName);
                        if (deletion != null)
                            candidates.Add(new CandidateIndel(deletion));
                        break;
                }

                if (operation.IsReadSpan())
                    startIndexInRead += (int)operation.Length;

                if (operation.IsReferenceSpan())
                    startIndexInReference += (int)operation.Length;
            }

            return candidates;
        }

    }
}
