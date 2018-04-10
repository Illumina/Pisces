using System;

namespace Pisces.Domain.Options
{
  
    public class OptionHelpers
    {
        public const char Delimiter = ',';

        public static string[] ListOfParamsToStringArray(string param)
        {
            return param.Split(new[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static float[] ParseStringToFloat(string[] stringArray)
        {
            var parameters = new float[stringArray.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                try
                {
                    parameters[i] = float.Parse(stringArray[i]);
                }
                catch
                {
                    throw new ArgumentException(string.Format("Unable to parse float type from " + stringArray[i]
                        + ".  Please check parameters."));
                }
            }

            return parameters;
        }

    }
}
