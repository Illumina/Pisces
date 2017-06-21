using System.IO;
using Common.IO.Sequencing;
using Xunit;

namespace Pisces.IO.Tests.UnitTests
{
    public class GenomeMetaDataTests
    {
        //private static string _genomeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Genomes", "chr19");
        private static string _genomeFolder = Path.Combine(TestPaths.SharedGenomesDirectory, "chr19");
        private static string _genomeXML = Path.Combine(_genomeFolder, "GenomeSize.xml");

        /// <summary>
        /// This tests some of the standard functions of GenomeMetaData. The tests for CheckReferenceGenomeFolderState include dummy .dict and .bwt files to satisfy those paths.
        /// </summary>
        [Fact]
        public void HappyPath()
        {
            Assert.Equal(GenomeMetadata.GenomeFolderState.Ready, GenomeMetadata.CheckReferenceGenomeFolderState(_genomeFolder, false, false));
            Assert.Equal(GenomeMetadata.GenomeFolderState.Ready, GenomeMetadata.CheckReferenceGenomeFolderState(_genomeFolder, true, false));
            Assert.Equal(GenomeMetadata.GenomeFolderState.RequireWritableFolder, GenomeMetadata.CheckReferenceGenomeFolderState(_genomeFolder, false, true));
            Assert.Equal(GenomeMetadata.GenomeFolderState.RequireWritableFolder, GenomeMetadata.CheckReferenceGenomeFolderState(_genomeFolder, true, true));

            var firstGmdt = new GenomeMetadata();

            firstGmdt.Deserialize(_genomeXML);
            TestGenomeMetadata(firstGmdt);
        }

        /// <summary>
        /// Test the standard SequenceMetaData functions.
        /// </summary>
        [Fact]
        public void SequenceMetaDataTest()
        {
           
            var gmdt = new GenomeMetadata();

            gmdt.Deserialize(_genomeXML);
            TestGenomeMetadata(gmdt);

            //gmdt.Save(testFile);

            var seq1 = gmdt.Sequences[0];

            Assert.True(seq1.CompareTo(seq1) == 0);

            Assert.False(seq1.IsMito());

            Assert.False(seq1.IsDecoyOrOther());

            Assert.True(seq1.IsAutosome());

        }

        /// <summary>
        /// Test writing a fasta file
        /// </summary>
        [Fact]
        public void WriteFastaFileTest()
        {
            var testFile = Path.Combine(TestPaths.LocalScratchDirectory, "WriteFastaFileTest.fa");

            if (File.Exists(testFile)) File.Delete(testFile);

            var gmdt = new GenomeMetadata();

            gmdt.Deserialize(_genomeXML);
            TestGenomeMetadata(gmdt);

            gmdt.Sequences[0].WriteFastaFile(testFile);

            Assert.True(File.Exists(testFile));
        }

        /// <summary>
        /// Test the values read from the xml file.
        /// </summary>
        /// <param name="gmdt"></param>
        private void TestGenomeMetadata(GenomeMetadata gmdt)
        {
            Assert.Null(gmdt.Species);
            Assert.Null(gmdt.Build);
            Assert.Equal(gmdt.KnownBases, 3119000);
            Assert.Equal(gmdt.Length, 3119000);
            Assert.Equal(gmdt.Name, "chr19FASTA");

            var result = gmdt.GetChromosomesIncludingNull();

            Assert.Equal(result.Count, 2);
            GenomeMetadata.SequenceMetadata foundSequence = new GenomeMetadata.SequenceMetadata();
            gmdt.TryGetSequence("chr19", out foundSequence);
            Assert.True(foundSequence.Name == "chr19");
            Assert.Equal(foundSequence.Length, 3119000);
        }
    }
}
