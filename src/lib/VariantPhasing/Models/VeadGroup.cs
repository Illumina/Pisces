using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VariantPhasing.Models
{
    public class VeadGroup : IComparable<VeadGroup>
    {
        public string Name { get { return _representativeVead.Name; } }
        public int NumVeads { get; private set; }
        public VariantSite[] SiteResults { get { return _representativeVead.SiteResults; } }
        public Vead RepresentativeVead { get { return _representativeVead; } }
        private readonly Vead _representativeVead;
        private int NumSitesInGroup
        {
            get
            {
                var sitesPerVead = RepresentativeVead.SiteResults.Count(vs => vs.HasRefAndAltData);
                return sitesPerVead * NumVeads;
            }
        }

        private int Length
        {
            get
            {
               return RepresentativeVead.SiteResults.Count();
            }
        }

        public VeadGroup(Vead vead)
        {
            _representativeVead = vead;
            NumVeads = 1;
        }

        public void AddSupport(Vead vead)
        {
            NumVeads++;
        }

        public int CompareTo(VeadGroup other)
        {
            return -1 * (NumSitesInGroup.CompareTo(other.NumSitesInGroup));
        }

        public override string ToString()
        {
            return (Name + ": " + _representativeVead.ToVariantSequence());
        }

        public string ToPositions()
        {
            return (Name + ": " + _representativeVead.ToPositionData());
        }

        public static List<VeadGroup[]> PairVeadGroups(List<VeadGroup> reads)
        {
            var pairs = new List<VeadGroup[]>();

            for (var i = 0; i < reads.Count; i++)
            {
                var r1 = reads[i];
                for (var j = i + 1; j < reads.Count; j++)
                {
                    var r2 = reads[j];
                    pairs.Add(new[] { r1, r2 });
                }
            }

            return pairs;
        }

        public static Agreement GetWorstAgreement(List<VeadGroup> veadGroups)
        {
            var worstAgreement = new Agreement {NumAgreement = Int32.MaxValue, NumDisagreement = 0};
            var numVeadGroups = veadGroups.Count;

            for (var i = 0; i < numVeadGroups; i++)
            {
                for (var j = i + 1; j < numVeadGroups; j++)
                {
                    var agreement = new Agreement(veadGroups[i], veadGroups[j]);

                    if (agreement.CompareTo(worstAgreement) < 0)
                    {
                        worstAgreement = agreement;
                    }
                }
            }

            return worstAgreement;

        }


        public static void DepthAtSites(IEnumerable<VeadGroup> vgs, out int[] depths, out int[] nocalls)
        {
            depths = new int[0];
            nocalls = new int[0];
            var veadgroups = vgs;

            if (veadgroups.Any())
            {
                var vgDepths = veadgroups.Select(x => x.ToDepths()).ToList(); //as many arrays as distinct vead groups
                var vgNoCalls = veadgroups.Select(x => x.ToNoCalls()).ToList(); //as many arrays as distinct vead groups

                var numSites = vgDepths.Any() ? vgDepths.First().GetLength(0) : 0;
                depths = new int[numSites];
                nocalls = new int[numSites];

                for (var j = 0; j < vgDepths.Count; j++)
                {
                    var vgDepth = vgDepths[j];
                    var vgNoCall = vgNoCalls[j];

                    for (var i = 0; i < numSites; i++)
                    {
                        depths[i] += vgDepth[i]; //total depth count
                        nocalls[i] += vgNoCall[i]; //total nocall count
                    }
                }
            }
        }

        //every vead in this group is identical..
        public int[] ToDepths()
        {
            var depth = new int[Length] ;

            for (var i = 0; i < Length; i++)
            {
                if (_representativeVead.SiteResults[i].HasRefAndAltData)
                    depth[i] = NumVeads;
            }

            return depth;
        }

        public int[] ToNoCalls()
        {
            var noCalls = new int[Length];

            for (var i = 0; i < Length; i++)
            {
                if (!_representativeVead.SiteResults[i].HasRefAndAltData)

                    noCalls[i] = NumVeads;

            }

            return noCalls;
        }
    }
}
