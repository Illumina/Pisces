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
        private int NumSites
        {
            get
            {
                var sitesPerVead = RepresentativeVead.SiteResults.Count(vs => vs.HasRefAndAltData);
                return sitesPerVead * NumVeads;
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
            return -1 * (NumSites.CompareTo(other.NumSites));
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


        public static int[] DepthAtSites(IEnumerable<VeadGroup> vgs)
        {
            var depthAtSites = new int[0];
            var veadgroups = vgs;

            if (veadgroups.Any())
            {
                var vgDepths = veadgroups.Select(x => x.ToDepths()).ToList();
                var depthSites = vgDepths.Any() ? vgDepths.First().Count : 0;
                depthAtSites = new int[depthSites];
                foreach (var vgDepth in vgDepths)
                {
                    for (var i = 0; i < depthSites; i++)
                    {
                        depthAtSites[i] += vgDepth[i];
                    }
                }
            }
            return depthAtSites;
        }

        public List<int> ToDepths()
        {
            var depth = new List<int>();

            for (var i = 0; i < _representativeVead.SiteResults.Length; i++)
            {
                depth.Add(0);
                for (var j = 0; j < NumVeads; j++)
                {
                    if (_representativeVead.SiteResults[i].HasRefAndAltData)
                        depth[i]++;                    
                }
            }

            return depth;
        }
    }
}
