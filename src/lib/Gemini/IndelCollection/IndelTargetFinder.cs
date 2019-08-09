using System.Collections.Generic;
using Alignment.Domain.Sequencing;
using Gemini.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Gemini.IndelCollection
{
    public class IndelTargetFinder
    {
        public List<PreIndel> FindIndels(BamAlignment read, string chromosomeName, int minBaseCallQuality = 10)
        {
            var candidates = new List<PreIndel>();

            var startIndexInRead = 0;
            var startIndexInReference = read.Position;

            for (var cigarOpIndex = 0; cigarOpIndex < read.CigarData.Count; cigarOpIndex++)
            {
                var operation = read.CigarData[cigarOpIndex];
                switch (operation.Type)
                {
                    case 'I':
                        var insertionQualityGoodEnough = true;
                        var totalQualities = 0;
                        var qualitiesNotGoodEnough = 0;
                        for (int i = 0; i < operation.Length; i++)
                        {
                            var indexInRead = startIndexInRead + i;
                            if (indexInRead > read.Qualities.Length - 1)
                            {
                                // TODO invalid, throw?
                                break;
                            }
                            var qualAtBase = read.Qualities[indexInRead];
                            totalQualities += (int)qualAtBase;

                            if (qualAtBase < minBaseCallQuality)
                            {
                                qualitiesNotGoodEnough++;
                            }
                        }

                        if (qualitiesNotGoodEnough / (float)operation.Length > 0.1)
                        {
                            insertionQualityGoodEnough = false;
                        }

                        // TODO check whether positions are off by one

                        var referenceBase = "N";
                        var insertion = new PreIndel(new CandidateAllele(chromosomeName, startIndexInReference, referenceBase, referenceBase + read.Bases.Substring(startIndexInRead, (int)operation.Length), AlleleCategory.Insertion));
                        if (insertion != null && insertionQualityGoodEnough)
                        {
                            candidates.Add(new PreIndel(insertion)
                            {
                                LeftAnchor = (int)(cigarOpIndex > 0 && read.CigarData[cigarOpIndex - 1].Type == 'M' ? read.CigarData[cigarOpIndex - 1].Length : 0),
                                RightAnchor = (int)(cigarOpIndex < read.CigarData.Count - 1 && read.CigarData[cigarOpIndex + 1].Type == 'M' ? read.CigarData[cigarOpIndex + 1].Length : 0),
                                AverageQualityRounded = totalQualities / (int)operation.Length // Loss of fraction here is ok
                            });
                        }
                        break;
                    case 'D':
                        // Note that this checks both the quality of the base preceding the deletion and the base after the deletion, or if there is no base after the deletion, counts that as low quality.
                        var deletionQualityGoodEnough = read.Qualities[startIndexInRead] >= minBaseCallQuality &&
                                                        (startIndexInRead + 1 < read.Qualities.Length && read.Qualities[startIndexInRead + 1] >= minBaseCallQuality);

                        // TODO this is not legit for the ref bases but going to do this for now. May not even really need to care about the reference base, honestly.
                        var referenceBases = new string('N', (int)operation.Length + 1);
                        var deletion = new PreIndel(new CandidateAllele(chromosomeName, startIndexInReference, referenceBases, "N", AlleleCategory.Deletion));
                        if (deletion != null && deletionQualityGoodEnough)
                            candidates.Add(new PreIndel(deletion)
                            {
                                LeftAnchor = (int)(cigarOpIndex > 0 && read.CigarData[cigarOpIndex - 1].Type == 'M' ? read.CigarData[cigarOpIndex - 1].Length : 0),
                                RightAnchor = (int)(cigarOpIndex < read.CigarData.Count - 1 && read.CigarData[cigarOpIndex + 1].Type == 'M' ? read.CigarData[cigarOpIndex + 1].Length : 0),
                                AverageQualityRounded = (read.Qualities[startIndexInRead] + (read.Qualities.Length > startIndexInRead + 2 ? read.Qualities[startIndexInRead + 1] : 0)) / 2
                            });
                        break;
                    default:
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