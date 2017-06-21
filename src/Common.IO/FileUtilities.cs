using System.IO;
using System.Reflection;

namespace Common.IO
{
    //helper methods for assembly data that is harder to get to in .net Core
    public class FileUtilities
    {
        public static string LocalAssemblyPath<T>()
        {
            return Path.GetDirectoryName(typeof(T).GetTypeInfo().Assembly.Location);
        }

        public static string LocalAssemblyDll<T>()
        {
            return (typeof(T).GetTypeInfo().Assembly.CodeBase);
        }

        public static string LocalAssemblyName<T>()
        {
            return (typeof(T).GetTypeInfo().Assembly.GetName().Name);
        }

        public static string LocalAssemblyVersion<T>()
        {
            return (typeof(T).GetTypeInfo().Assembly.GetName().Version.ToString());
        }
    }
}
