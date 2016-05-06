using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariantCalling.Processing.Utility;

namespace VariantCalling.Processing.Models
{
    public class BamChrJob : GenericJob
    {
        public string BamFilePath { get; private set; }
        public string ChrName { get; private set; }

        public BamChrJob(Action action, string bamFilePath, string chrName) : base(action)
        {
            BamFilePath = bamFilePath;
            ChrName = chrName;
        }
    }
}
