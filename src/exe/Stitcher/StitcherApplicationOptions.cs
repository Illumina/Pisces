using System.IO;
using BamStitchingLogic;
using CommandLine.NDesk.Options;
using Pisces.Domain.Options;

namespace Stitcher
{
    public class StitcherApplicationOptions: BaseApplicationOptions
    {
        public StitcherOptions StitcherOptions;
        public string InputBam = null;
        public bool ShowVersion = false;
        public OptionSet OptionSet;
        public StitcherApplicationOptions()
        {
            StitcherOptions = new StitcherOptions();
        }

        public override string GetMainInputDirectory()
        {
            return Path.GetDirectoryName(InputBam);
        }
    
    }
}