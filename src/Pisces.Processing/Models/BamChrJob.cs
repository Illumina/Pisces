using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Pisces.Processing.Utility;

namespace Pisces.Processing.Models
{
    public class BamChrJob : GenericJob
    {
        public string BamFilePath { get; private set; }
        public string ChrName { get; private set; }

        public BamChrJob(Action action, string bamFilePath, string chrName) : base(action, Path.GetFileNameWithoutExtension(bamFilePath) + "_" + chrName)
        {
            BamFilePath = bamFilePath;
            ChrName = chrName;
        }
    }
}
