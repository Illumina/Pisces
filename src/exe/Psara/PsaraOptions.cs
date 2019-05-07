using System.IO;
using Pisces.Domain.Options;

namespace Psara
{
    public class PsaraOptions : VcfConsumerAppOptions
    {
        public GeometricFilterParameters GeometricFilterParameters = new GeometricFilterParameters();


        public string InputDirectory
        {
            get
            {
                return Path.GetDirectoryName(VcfPath);
            }

            set
            {
                _inputDirectory = value;
            }
        }
    }
}

