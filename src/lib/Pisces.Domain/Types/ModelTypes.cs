using System;
using System.Collections.Generic;
namespace Pisces.Domain.Types
{
    public enum PloidyModel
    {
        Somatic,
        Diploid 
    };

    public enum NoiseModel  //tjd - might use this with new Q model.
    {
        Flat,
		Window
	};

    public enum QualityModel
    {
        Poisson,  //original, theoretical
        //Sigmoid   //removing support, 2/15, never had good performance
    };

    public enum StrandBiasModel
    {
        Poisson,  //mirrors the variant Q scoring model <- we dont use this anymore, so lets not let people use it
        Extended,  //extends the Poisson model with the Binomial Theorem where Posson is undefined.
        Diploid
    };

    public enum CoverageMethod
    {
        Approximate,
        Exact
    }
}
