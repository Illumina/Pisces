using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pisces.Domain.Utility
{
    public static class ValidationHelper
    {
        public static void VerifyRange(int field, int minValue, int? maxValue, string fieldName)
        {
            if (field < minValue || (maxValue.HasValue && field > maxValue))
                throw new ArgumentException(string.Format("{0} must be between {1} and {2}.", fieldName,
                            minValue,
                            maxValue));

            if (field < minValue && !maxValue.HasValue)
                throw new ArgumentException(string.Format("{0} must be greater than {1}.", fieldName, minValue));
        }

        public static void VerifyRange(float field, float minValue, float? maxValue, string fieldName)
        {
            if (field < minValue || (maxValue.HasValue && field > maxValue))
                throw new ArgumentException(string.Format("{0} must be between {1} and {2}.", fieldName,
                            minValue,
                            maxValue));

            if (field < minValue && !maxValue.HasValue)
                throw new ArgumentException(string.Format("{0} must be greater than {1}.", fieldName, minValue));
        }
    }
}
