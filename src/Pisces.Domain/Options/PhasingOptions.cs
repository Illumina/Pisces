using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pisces.Domain.Options
{
    public class PhasableVariantCriteria
    {
        public bool PassingVariantsOnly = true;
        public bool HetVariantsOnly = false;
        public int PhasingDistance = 50;
        public string[] ChrToProcessArray = { };
        public string FilteredNbhdToProcess = null;  //debugging option, to given nbhds
        public int MaxNumNbhdsToProcess = -1;  //debugging option, to restrict num of nbhds we will go through
    }

    public class ClusteringParameters
    {
        public bool AllowClusterMerging = true;
        public bool AllowWorstFitRemoval = true;
        public int MinNumberAgreements = 1;  //to join a cluster. must have his num agreements.
        public int MaxNumberDisagreements = 0; //cannot have more than this num disagreements
        public int MaxNumNewClustersPerSite = 100;
        public int ClusterConstraint = -1;
    }
}
