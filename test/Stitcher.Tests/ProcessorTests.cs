using System.IO;
using Alignment.Domain.Sequencing;
using Alignment.IO.Sequencing;
using Common.IO.Utility;
using Xunit;

namespace Stitcher.Tests
{
    public class StitcherProcessorTests
    {
        [Fact]
        public void TestGenomeProcessor()
        {
            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var expBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.stitched.bam");
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory);
            var outBam = Path.Combine(outFolder, "Bcereus_S4.final.stitched.bam");

            var stitcherOptions = new StitcherOptions();
            if (File.Exists(outBam))
                File.Delete(outBam);

            RunProcessorTest(inBam, outBam, expBam, outFolder, true, stitcherOptions);
        }

        [Fact]
        public void TestBamProcessor()
        {
            var inBam = Path.Combine(TestPaths.SharedBamDirectory, "Bcereus_S4.bam");
            var expBam = Path.Combine(TestPaths.LocalTestDataDirectory, "Bcereus_S4.stitched.bam");
            var outFolder = Path.Combine(TestPaths.LocalScratchDirectory);
            var outBam = Path.Combine(outFolder, "Bcereus_S4.stitched.bam");

            var stitcherOptions = new StitcherOptions();
            if (File.Exists(outBam))
                File.Delete(outBam);

            RunProcessorTest(inBam, outBam, expBam, outFolder, false, stitcherOptions);
        }

        private static void RunProcessorTest(string inBam, string outBam, string expBam, string outFolder, bool threadbyChr, StitcherOptions stitcherOptions)
        {
            if (File.Exists(outBam))
                File.Delete(outBam);


            Logger.OpenLog(TestPaths.LocalScratchDirectory, "StitcherTestLog.txt", true);
            var processor = threadbyChr ? (IStitcherProcessor)new GenomeProcessor(inBam) : new BamProcessor();
            processor.Process(inBam, outFolder, stitcherOptions);
            Logger.CloseLog();


            Assert.True(File.Exists(outBam));

            var observedAlignment = new BamAlignment();
            var expectedAlignment = new BamAlignment();

            using (var outReader = new BamReader(outBam))
            using (var expReader = new BamReader(expBam))
            {
                while (true)
                {
                    var nextObservation = outReader.GetNextAlignment(ref observedAlignment, true);

                    var nextExpected = expReader.GetNextAlignment(ref expectedAlignment, true);

                    if ((nextExpected == false) || (expectedAlignment == null))
                    {
                        break;
                    }


                    Assert.Equal(expectedAlignment.Bases, observedAlignment.Bases);
                    Assert.Equal(expectedAlignment.Position, observedAlignment.Position);
                    Assert.Equal(expectedAlignment.Qualities, observedAlignment.Qualities);


                }

                outReader.Close();
                expReader.Close();
            }
        }
    }
}