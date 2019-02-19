using System.IO;
using Pisces.Domain.Options;

namespace AdaptiveGenotyper
{
    public class GenotyperOptions : BaseApplicationOptions
    {
        #region Members
        public string InputVcf;
        public string LogFileName = "GQRLog.txt";
        public string ModelFile;
        #endregion

        public override string GetMainInputDirectory()
        {
            return Path.GetDirectoryName(InputVcf);
        }
    }
}
