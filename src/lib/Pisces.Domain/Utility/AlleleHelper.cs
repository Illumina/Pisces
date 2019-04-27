using System;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;

namespace Pisces.Domain.Utility
{
    public static class AlleleHelper
    {
        public static AlleleType GetAlleleType(string alleleString)
        {
            return GetAlleleType(Convert.ToChar(alleleString));
        }
        public static AlleleType GetAlleleType(char alleleChar)
        {
            switch (alleleChar)
            {
                case 'A':
                    return AlleleType.A;
                case 'C':
                    return AlleleType.C;
                case 'G':
                    return AlleleType.G;
                case 'T':
                    return AlleleType.T;
                case 'N':
                    return AlleleType.N;
                default:
                    //be kinder to unknown bases.
                    return AlleleType.N;
                    //throw new ArgumentException(string.Format("Unrecognized allele '{0}'.", alleleChar));
            }
        }

        public static CandidateAllele Map(CalledAllele called)
        {
            var candidateAllele = new CandidateAllele(called.Chromosome, called.ReferencePosition, called.ReferenceAllele,
                called.AlternateAllele, called.Type);

            Array.Copy(called.SupportByDirection, candidateAllele.SupportByDirection, called.SupportByDirection.Length);
            Array.Copy(called.WellAnchoredSupportByDirection, candidateAllele.WellAnchoredSupportByDirection, called.WellAnchoredSupportByDirection.Length);

            if (called.Type != AlleleCategory.Reference)
            {
                for (var i = 0; i < called.ReadCollapsedCountsMut.Length; i++)
                    candidateAllele.ReadCollapsedCountsMut[i] = called.ReadCollapsedCountsMut[i];
            }

            return candidateAllele;
        }

        public static CalledAllele Map(CandidateAllele candidate)
        {           
            var calledAllele = new CalledAllele(candidate.Type);

            calledAllele.AlternateAllele = candidate.AlternateAllele;
            calledAllele.ReferenceAllele = candidate.ReferenceAllele;
            calledAllele.Chromosome = candidate.Chromosome;
            calledAllele.ReferencePosition = candidate.ReferencePosition;
            calledAllele.AlleleSupport = candidate.Support;
            calledAllele.WellAnchoredSupport = candidate.WellAnchoredSupport;
            calledAllele.IsForcedToReport = candidate.IsForcedAllele;

            Array.Copy(candidate.SupportByDirection, calledAllele.SupportByDirection, candidate.SupportByDirection.Length);
            Array.Copy(candidate.WellAnchoredSupportByDirection, calledAllele.WellAnchoredSupportByDirection, candidate.WellAnchoredSupportByDirection.Length);

            if (candidate.Type != AlleleCategory.Reference)
            {
                for (var i = 0; i < candidate.ReadCollapsedCountsMut.Length; i++)
                    calledAllele.ReadCollapsedCountsMut[i] = candidate.ReadCollapsedCountsMut[i];
            }

            if (candidate.SupportByAmplicon.AmpliconNames != null)
            {
                calledAllele.SupportByAmplicon = new Models.AmpliconCounts()
                {
                    AmpliconNames = new string[Constants.MaxNumOverlappingAmplicons],
                    CountsForAmplicon = new int[Constants.MaxNumOverlappingAmplicons]
                };

                Array.Copy(candidate.SupportByAmplicon.AmpliconNames, calledAllele.SupportByAmplicon.AmpliconNames, candidate.SupportByAmplicon.AmpliconNames.Length);
                Array.Copy(candidate.SupportByAmplicon.CountsForAmplicon, calledAllele.SupportByAmplicon.CountsForAmplicon, candidate.SupportByAmplicon.CountsForAmplicon.Length);
            }

            return calledAllele;
        }
    }
}
