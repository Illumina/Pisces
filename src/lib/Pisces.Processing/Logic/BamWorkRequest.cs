using System.IO;

namespace Pisces.Processing.Logic
{
    public class BamWorkRequest
    {
        public string BamFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public string GenomeDirectory { get; set; }

        public string BamFileName
        {
            get { return string.IsNullOrEmpty(BamFilePath) ? string.Empty : Path.GetFileName(BamFilePath); }
        }
    }
}
