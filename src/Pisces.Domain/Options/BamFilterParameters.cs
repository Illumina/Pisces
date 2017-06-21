using System;
using System.Collections.Generic;
using Pisces.Domain.Utility;

namespace Pisces.Domain.Options
{
    public class BamFilterParameters
    {
        public int MinimumMapQuality = 1;
        public int MinimumBaseCallQuality = 20;
        public int MinNumberVariantsInRead = 1; //Scylla only
        public bool RemoveDuplicates = true; 
        public bool OnlyUseProperPairs = false;
        public void Validate()
        {
            ValidationHelper.VerifyRange(MinimumBaseCallQuality, 0, int.MaxValue, "MinimumBaseCallQuality");
            ValidationHelper.VerifyRange(MinimumMapQuality, 0, int.MaxValue, "MinimumMapQuality");
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
                        case "-minbq":
                        case "-minbasecallquality":
                            MinimumBaseCallQuality = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-minmq": //used to be "m"
                        case "-minmapquality":
                            MinimumMapQuality = int.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-filterduplicates":
                        case "-duplicatereadfilter":
                            RemoveDuplicates = bool.Parse(value);
                            usedArguments.Add(lastArgumentField);
                            break;
                        case "-pp":
                        case "-onlyuseproperpairs":
                            OnlyUseProperPairs = bool.Parse(value);
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
