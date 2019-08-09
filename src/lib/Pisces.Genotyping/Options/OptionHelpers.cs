using System.Linq;
using System;

namespace Pisces.Domain.Options
{
  
    public class OptionHelpers
    {
        public const char Delimiter = ',';

        public static string ListOfParamsToDelimiterSeparatedString<T>(T[] array)
        {
            return string.Join(Delimiter, array.Select(p => p.ToString()).ToArray());
        }
        public static string[] ListOfParamsToStringArray(string param)
        {
            return param.Split(new[] { Delimiter, '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
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

        public static double[] ParseStringToDouble(string[] stringArray)
        {
            var parameters = new double[stringArray.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                try
                {
                    parameters[i] = double.Parse(stringArray[i]);
                }
                catch
                {
                    throw new ArgumentException(string.Format("Unable to parse double type from " + stringArray[i]
                        + ".  Please check parameters."));
                }
            }

            return parameters;
        }

    }
}
