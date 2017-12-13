using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pisces.Processing.Utility
{
    public class Utilities
    {
        public static bool IsThisMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        /* Should never need this ever again....
        public static string GetMonoPath()
        {
            var runtime = string.Empty;
            try
            {
                // runtime location should be like: {monoDir}/lib/mono/4.5/mscorlib.dll
                runtime = Type.GetType("Mono.Runtime").Assembly.Location;

                // we need {monoDir}/bin/mono
                var monoRootDir =
                    Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(runtime))));

                return Path.Combine(monoRootDir, "bin", "mono");
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to get mono path from run time location: " + runtime, ex);
            }
        }*/
    }
}
