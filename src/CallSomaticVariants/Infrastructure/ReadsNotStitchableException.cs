using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallSomaticVariants.Infrastructure
{
    public class ReadsNotStitchableException : Exception
    {
        public ReadsNotStitchableException(string message) : base(message)
        {
        }

        public ReadsNotStitchableException(string message, Exception inner)
            : base(message,inner)
        {
        }

    }
}
