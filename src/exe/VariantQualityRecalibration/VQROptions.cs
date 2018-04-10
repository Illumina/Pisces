using System.Collections.Generic;
using Common.IO.Utility;
using CommandLine.NDesk.Options;
using Pisces.Domain.Options;

namespace VariantQualityRecalibration
{
    public class VQROptions : BaseApplicationOptions
    {
    
        #region Members
        public string InputVcf;
        public string OutputDirectory = "";
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
    }
}
