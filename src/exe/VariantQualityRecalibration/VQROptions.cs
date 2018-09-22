using System.IO;
using Pisces.Domain.Options;

namespace VariantQualityRecalibration
{
    public class VQROptions : BaseApplicationOptions
    {
    
        #region Members
        public string InputVcf;
        public string LogFileName = "VariantQualityRecalibrationLog.txt";
        public int LociCount = -1;
       
        //+
        //calibration parameters
        public int BaseQNoise = 20;
        public int FilterQScore = 30;
        public int MaxQScore = 100;
        public float ZFactor = 2F;
        //-
        #endregion

        public override string GetMainInputDirectory()
        {      
            return Path.GetDirectoryName(InputVcf);
        }
    }
}
