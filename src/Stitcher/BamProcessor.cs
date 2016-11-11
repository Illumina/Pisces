using System;
using System.IO;
using Pisces.Processing.Utility;

namespace Stitcher
{
    public class BamProcessor : IStitcherProcessor
    {
        public void Process(string inputBam, string outFolder, StitcherOptions stitcherOptions)
        {
            var stitcher = new BamStitcher(inputBam, Path.Combine(outFolder, Path.GetFileNameWithoutExtension(inputBam) + ".stitched.bam"), stitcherOptions);
            stitcher.Execute();
        }

    }
}