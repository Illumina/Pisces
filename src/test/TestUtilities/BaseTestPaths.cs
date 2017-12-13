using System.IO;
using System.Reflection;
using Common.IO;

namespace TestUtilities
{
    public class BaseTestPaths<T>
    {
        public static string CurrentDirectory { get { return Directory.GetCurrentDirectory(); } }
        public static string RootDirectory { get { return LocalAssemblyPath; } }

        public static string SharedStitcherData { get { return Path.Combine(RootDirectory, "SharedData", "StitcherTestData"); } }

        public static string SharedIntervalsDirectory { get { return Path.Combine(RootDirectory, "SharedData", "Intervals"); } }

        public static string SharedBamDirectory { get { return Path.Combine(RootDirectory, "SharedData", "Bams"); } }
        public static string SharedGenomesDirectory { get { return Path.Combine(RootDirectory, "SharedData", "Genomes"); } }

     
        public static string LocalAssemblyPath { get { return FileUtilities.LocalAssemblyPath<T>(); } }

 
        public static string LocalTestDataDirectory { get { return Path.Combine(LocalAssemblyPath, "TestData"); } }
        
        public static string LocalScratchDirectory
        { get
            {
                var scratchPath = Path.Combine(LocalAssemblyPath, "Scratch");
                if (!Directory.Exists(scratchPath))
                    Directory.CreateDirectory(scratchPath);

                return scratchPath;

            }

        }
    }
}
