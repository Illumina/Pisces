using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Pisces.Domain.Models.Alleles;
using Pisces.IO;

namespace VennVcf
{
    class VennVcfFormatter : VcfFormatter
    {
        public VennVcfFormatter() { }
        bool DebugMode = false;

        public VennVcfFormatter(VcfWriterConfig Config, bool debugMode)
        {
            _config = Config;
            UpdateFrequencyFormat();
            DebugMode = debugMode;
        }

        //adding special tags for VennVcf debugging
        public override StringBuilder[] AddCustomTags(IEnumerable<CalledAllele> variants, StringBuilder[] formatAndSampleStringBuilder)
        {
            if (!DebugMode)
                return formatAndSampleStringBuilder;

            var firstVar = variants.First(); //we only ever use a single variant for this method.

            if (variants.Count() > 1)
                throw new InvalidDataException("VennVcf AddCustomTags method should not be used with a variant list.");

            if (firstVar is AggregateAllele)
            {
                var myAggregateAllele = (AggregateAllele)firstVar;
                var originalAlleles = myAggregateAllele.ComponentAlleles;

                for (int i = 0; i < originalAlleles.Count(); i++)
                {
                    var originalAllele = originalAlleles[i];
                    formatAndSampleStringBuilder[0].Append(":VF" + i);

                    if (originalAllele == null)
                    {
                        formatAndSampleStringBuilder[1].Append(":NA");
                    }
                    else
                    {
                        var variantFrequencyString = GetFrequencyString(new List<CalledAllele>() { originalAllele }, false, originalAllele.TotalCoverage);
                        formatAndSampleStringBuilder[1].Append(string.Format(":{0}", variantFrequencyString));
                    }
                }
                for (int i = 0; i < originalAlleles.Count(); i++)
                {
                    var originalAllele = originalAlleles[i];
                    formatAndSampleStringBuilder[0].Append(":AD" + i);
                    if (originalAllele == null)
                    {
                        formatAndSampleStringBuilder[1].Append(":NA");
                    }
                    else
                    {
                        formatAndSampleStringBuilder[1].Append(string.Format(":{0}", originalAlleles[i].AlleleSupport));
                    }
                }

                for (int i = 0; i < originalAlleles.Count(); i++)
                {
                    var originalAllele = originalAlleles[i];
                    formatAndSampleStringBuilder[0].Append(":DP" + i);
                    if (originalAllele == null)
                    {
                        formatAndSampleStringBuilder[1].Append(":NA");
                    }
                    else
                    {
                        formatAndSampleStringBuilder[1].Append(string.Format(":{0}", originalAlleles[i].TotalCoverage));
                    }
                }
            }
            return formatAndSampleStringBuilder;
        }

    }
}
