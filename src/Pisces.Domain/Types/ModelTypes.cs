using System;
using System.Collections.Generic;
namespace Pisces.Domain.Types
{
    public enum PloidyModel
    {
        Somatic,
        Diploid 
    };

    public enum GenotypeModel
    {
        None,
        //Symmetrical, //tjd, currently not supported
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

    public enum CoverageMethod
    {
        Approximate,
        Exact
    }
}
