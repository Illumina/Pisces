using System.IO;
using Common.IO.Utility;
using Pisces.Domain.Options;

namespace Psara
{
    public class PsaraOptions : BaseApplicationOptions
    {
        public GeometricFilterParameters GeometricFilterParameters = new GeometricFilterParameters();
        public string InputVcf = "";

      
        public string InputDirectory
        {
            get
            {
                return Path.GetDirectoryName(InputVcf);
            }

            set
            {
                _inputDirectory = value;
            }
        }
    }
}
