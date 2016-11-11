using System.Linq;
using System.Text;

namespace VariantPhasing.Models
{

    public class Vead
    {
        public string Name;
        public VariantSite[] SiteResults;
      
        public Vead(string name, VariantSite[] variants)
        {
            Name = name;

            if (variants != null)
            {
                SiteResults = variants.Any() ? new VariantSite[variants.Length] : new VariantSite[0];

                for (var i = 0; i < SiteResults.Length; i++)
                {
                    SiteResults[i] = variants[i].DeepCopy();
                }                
            }
            else SiteResults = new VariantSite[0];
        }

        public string ToVariantSequence()
        {
            return (VariantSite.ArrayToString(SiteResults));      
        }

        public string ToPositionData()
        {
            var sb = new StringBuilder();
            foreach (var t in SiteResults)
            {
                sb.Append(t.VcfReferencePosition + " ");
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return (Name + ": " + ToVariantSequence());
        }

    }

}
