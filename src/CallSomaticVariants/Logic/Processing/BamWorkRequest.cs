using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallSomaticVariants.Logic.Processing
{
    public class BamWorkRequest
    {
        public string BamFilePath { get; set; }
        public string VcfFilePath { get; set; }
        public string GenomeDirectory { get; set; }

        public string BamFileName
        {
            get { return string.IsNullOrEmpty(BamFilePath) ? string.Empty : Path.GetFileName(BamFilePath); }
        }
    }
}
