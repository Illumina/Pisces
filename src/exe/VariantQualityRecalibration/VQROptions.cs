using System.IO;
using Pisces.Domain.Options;

namespace VariantQualityRecalibration
{
    public class VQROptions : VcfConsumerAppOptions
    {

        #region Members
        //public string VcfPath;   <- now in the base options class
        public int LociCount = -1;
        public bool DoBasicChecks = true; //look for over represented mutations across all positions, 
        public bool DoAmpliconPositionChecks = false; //look for over represented mutations with {N} bases of edges
        public int ExtentofEdgeRegion = 4;  // how many bases around a detected edge constitute an edge region. The {N} above.

        //+
        //calibration parameters
        //public int BaseQNoise = 20;  <- now in the base options class
        //public int FilterQScore = 30;  <- now in the base options class
        public int MaxQScore = 100;
        public float ZFactor = 2F;
        public float AlignmentWarningThreshold = 10;
        //-


        #endregion

        public override string GetMainInputDirectory()
        {      
            return Path.GetDirectoryName(VcfPath);
        }
    }
}
