using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallSomaticVariants.Types
{
    public enum BamQCOptions
    {
        BamQCandVarCall,
        VarCallOnly,
        BamQCOnly
    };

    public enum GenotypeModel
    {
        None,
        Symmetrical,
        Thresholding
    };

    public enum NoiseModel  //tjd - might use this with new Q model.
    {
        Window,
        Flat
    };

    public enum QualityModel
    {
        Poisson,  //original, theoretical
        //Sigmoid   //removing support, 2/15, never had good performance
    };

    public enum StrandBiasModel
    {
        Poisson,  //mirrors the variant Q scoring model
        Extended  //extends the Poisson model with the Binomial Theorem where Posson is undefined.
    };
}
