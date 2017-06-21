using System;
using System.Collections.Generic;
using Pisces.Domain.Utility;
using Pisces.Domain.Types;

namespace Pisces.Domain.Options
{
    public class VcfWritingParameters
    {
        public bool OutputGvcfFile = true;
        public bool MitochondrialChrComesFirst = false; // how we order variants in the output vcf (replace some code in VcfNbhd.cs)
        public bool? ForceCrush = null; //override default crush / no crush behavior. (be default, this is governed by the ploidy)
        public bool AllowMultipleVcfLinesPerLoci = true; //to crush or not to crush
        public bool ReportNoCalls = false;
        public bool ReportRcCounts = false;
        public double StrandBiasScoreMinimumToWriteToVCF = -100; // just so we dont have to write negative infinities into vcfs and then they get tagged as "poorly formed"  
        public double StrandBiasScoreMaximumToWriteToVCF = 0;

        public void SetDerivedParameters(VariantCallingParameters varcallParameters)
        {
            if (ForceCrush.HasValue)
            {
                AllowMultipleVcfLinesPerLoci = !((bool)ForceCrush);
                return;
            }

            if (varcallParameters.PloidyModel == PloidyModel.Diploid)
            {
                AllowMultipleVcfLinesPerLoci = false;
            }
            else
                AllowMultipleVcfLinesPerLoci = true;
        }

        public List<string> Parse(string[] arguments)
        {
            var lastArgumentField = string.Empty;
            var usedArguments = new List<string>();

            try
            {
                int argumentIndex = 0;
                while (argumentIndex < arguments.Length)
                {
                    if (string.IsNullOrEmpty(arguments[argumentIndex]))
                    {
                        argumentIndex++;
                        continue;
                    }
                    string value = null;
                    if (argumentIndex < arguments.Length - 1) value = arguments[argumentIndex + 1].Trim();

                    lastArgumentField = arguments[argumentIndex].ToLower();

                    switch (lastArgumentField)
                    {
                        case "-gvcf":
                            OutputGvcfFile = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-crushvcf":
                            ForceCrush = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-reportnocalls":
                            ReportNoCalls = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-reportrccounts":
                            ReportRcCounts = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;

                        default:

                            break;
                    }
                    argumentIndex += 2;
                }
                return usedArguments;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("Unable to parse argument {0}: {1}", lastArgumentField, ex.Message));
            }
        }


    }

}
