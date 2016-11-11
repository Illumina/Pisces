using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alignment.IO.Sequencing;
using Xunit;
using Pisces.IO;
using Common.IO.Sequencing;

namespace Pisces.IO.Tests.UnitTests
{
    public class GenomeMetaDataTests
    {
        private static string _genomeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "Genomes", "chr19");
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

            firstGmdt.Serialize("temp.xml");

            var secondGmdt = new GenomeMetadata();
            secondGmdt.Deserialize("temp.xml");

            TestGenomeMetadata(secondGmdt);
        }

        /// <summary>
        /// Test the standard SequenceMetaData functions.
        /// </summary>
        [Fact]
        public void SequenceMetaDataTest()
        {
            var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SequenceMetaDataTest.fa");

            var gmdt = new GenomeMetadata();

            gmdt.Deserialize(_genomeXML);
            TestGenomeMetadata(gmdt);

            gmdt.Serialize(testFile);

            var seq1 = gmdt.Sequences[0];

            Assert.True(seq1.CompareTo(seq1) == 0);

            Assert.False(seq1.IsMito());

            Assert.False(seq1.IsDecoyOrOther());

            Assert.True(seq1.IsAutosome());

            File.Delete(testFile);
        }

        /// <summary>
        /// Test writing a fasta file
        /// </summary>
        [Fact]
        public void WriteFastaFileTest()
        {
            var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WriteFastaFileTest.fa");
            if ( File.Exists(testFile)) File.Delete(testFile);

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
