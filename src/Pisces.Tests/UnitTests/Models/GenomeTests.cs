using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Pisces.IO;
using Xunit;

namespace Pisces.Tests.UnitTests.Models
{
    public class GenomeTests
    {
        [Fact]
        public void HappyPath()
        {
            var genomeDirectory = Path.Combine(UnitTestPaths.TestGenomesDirectory, "chr17chr19");
            var genome = new Genome(genomeDirectory, new List<string> { "chr19", "chr17"});

            Assert.Equal(genomeDirectory, genome.Directory);

            // make sure they are listed in the order within genome
            var chrs = genome.ChromosomesToProcess.ToList();
            Assert.Equal(2, chrs.Count());
            Assert.Equal("chr17", chrs[0]);
            Assert.Equal("chr19", chrs[1]);

            var chrLengths = genome.ChromosomeLengths.ToList();
            Assert.Equal(2, chrLengths.Count());
            Assert.Equal("chr17", chrLengths[0].Item1);
            Assert.Equal("chr19", chrLengths[1].Item1);
            Assert.Equal(7573100, chrLengths[0].Item2);
            Assert.Equal(3119100, chrLengths[1].Item2);

            var chrReference = genome.GetChrReference("chrX");
            Assert.Equal(null, chrReference);

            chrReference = genome.GetChrReference("chr19");
            Assert.Equal("chr19", chrReference.Name);
            Assert.Equal(Path.Combine(genomeDirectory, "chr17chr19.fa"), chrReference.FastaPath);
            Assert.Equal(3119100, chrReference.Sequence.Length);

            // test different chrs to process
            genome = new Genome(genomeDirectory, new List<string> { "chr19" });
            chrs = genome.ChromosomesToProcess.ToList();
            Assert.Equal(1, chrs.Count());
            Assert.Equal("chr19", chrs[0]);

            // test gentle exclusion of invalid chr to process (this will later log a warning downstream)
            genome = new Genome(genomeDirectory, new List<string> { "chrY", "chr17" });
            chrs = genome.ChromosomesToProcess.ToList();
            Assert.Equal(1, chrs.Count());
            Assert.Equal("chr17", chrs[0]);

        }

        [Fact]
        [Trait("ReqID", "SDS-7")]
        [Trait("ReqID", "SDS-8")]
        public void Error()
        {
            var genomeDirectoryBadGenomeSizeFormat = Path.Combine(UnitTestPaths.TestGenomesDirectory, "invalidGenomeSize");
            var genomeDirectoryNoGenomeSize = Path.Combine(UnitTestPaths.TestGenomesDirectory, "noGenomeSize");
            var genomeDirectoryFaNotExists = Path.Combine(UnitTestPaths.TestGenomesDirectory, "invalidGenome");
            var genomeDirectoryFaiNotExists = Path.Combine(UnitTestPaths.TestGenomesDirectory, "invalidGenome_noFai");
            var nonexistentDirectory = "Nonexistent";

            // Does the directory exist?
            var exception = Assert.Throws<ArgumentException>(() => new Genome(nonexistentDirectory, new List<string> { "chr19", "chr17" }));
            Assert.Contains(nonexistentDirectory, exception.Message);

            // Does the GenomeSize.xml exist?
            exception = Assert.Throws<ArgumentException>(() => new Genome(genomeDirectoryNoGenomeSize, new List<string> { "chr19", "chr17" }));
            Assert.Contains(genomeDirectoryNoGenomeSize, exception.Message);
            Assert.Contains("GenomeSize.xml is missing", exception.Message);

            // Is the GenomeSize.xml a bad format?
            exception = Assert.Throws<ArgumentException>(() => new Genome(genomeDirectoryBadGenomeSizeFormat, new List<string> { "chr19", "chr17" }));
            Assert.Contains(genomeDirectoryBadGenomeSizeFormat, exception.Message);
            Assert.Contains("Unable to read GenomeSize.xml", exception.Message);

            // Do the fasta files exist?
            exception = Assert.Throws<ArgumentException>(() => new Genome(genomeDirectoryFaNotExists, new List<string> { "chr19", "chr17" }));
            Assert.Contains(genomeDirectoryFaNotExists, exception.Message);

            // Do the fai files exist?
            exception = Assert.Throws<ArgumentException>(() => new Genome(genomeDirectoryFaiNotExists, new List<string> { "chr19", "chr17" }));
            Assert.Contains(genomeDirectoryFaiNotExists, exception.Message);
        }
    }
}
