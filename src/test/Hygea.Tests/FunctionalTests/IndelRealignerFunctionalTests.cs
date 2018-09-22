using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealignIndels.Logic;
using RealignIndels.Tests.Utilities;
using Pisces.Domain.Models;
using Pisces.IO;
using Hygea.Tests;
using Xunit;

namespace RealignIndels.Tests.FunctionalTests
{
    public class IndelRealignerFunctionalTests
    {
        private readonly string _bamSmall = Path.Combine(TestPaths.LocalTestDataDirectory, "Ins-L3-var12_S12.bam");
        private static string snippetPath = Path.Combine(TestPaths.SharedGenomesDirectory, "Snippets");

        [Fact]
        public void BasicTest()
        {
            // this number is 186 with 5.2.7
            ExecuteTest(_bamSmall, _bamSmall.Replace(".bam", ".ri.bam"), 170); // TODO This really needs to be independently verified!!
            // this number is 181 with 5.2.7, max shift of 20, // 5 would have shifts of larger than 20, don't realign those
            ExecuteTest(_bamSmall, _bamSmall.Replace(".bam", ".ri.bam"), 169, 15); // 1 would have shifts of larger than 15, don't realign those
        }

        public void ExecuteTest(string inputBamFile, string outputBamFile, int expectedNumRealignedReads, int maxShift = 250)
        {
            if (File.Exists(outputBamFile))
                File.Delete(outputBamFile);

            var options = new HygeaOptions();
            options.SkipAndRemoveDuplicates = false; //(old default/simple behavior for regression testing basic execution)
            options.MaxRealignShift = maxShift;

            var factory = new Factory(options);

            var chrRef = GetGenomeSnippet();

            var writer = new RemapBamWriter(inputBamFile, outputBamFile);
            writer.Initialize();

            var realigner = (ChrRealigner)factory.CreateRealigner(chrRef, inputBamFile, writer);
            realigner.Execute();

            writer.FinishAll();

            Assert.True(File.Exists(outputBamFile));
            Assert.Equal(expectedNumRealignedReads, realigner.TotalRealignedReads);
            
        }

        private static ChrReference GetGenomeSnippet()
        {
            var testInput = File.ReadAllLines(Path.Combine(snippetPath, "Ins-L3-var12_S12_snippet.txt"));

            // The filler is 50 N's to match the length of a standard line in the genome sequence file.
            // It makes it easier to know the number of copies to append before inserting the snippet.
            var filler = "NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN";
            var x = new StringBuilder();

            // Pad the snippet by 572148 lines of filler to place it in the same location that the actual sequence would be found in the chromosome.
            for (int i = 0; i < 572148; i++)
            {
                x.AppendFormat(filler);
            }

            foreach (var line in testInput)
            {
                x.AppendFormat(line);
            }
            
            return new ChrReference() {Name = "chr13", Sequence = x.ToString()};
        }
    }
}
