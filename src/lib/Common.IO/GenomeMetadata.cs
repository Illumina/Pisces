using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Common.IO.Sequencing
{
    public class GenomeMetadata
    {
        #region Members
        public const int BufferSize = 10485760; // 1024*1024*10
        protected static Regex IndexRegex;
        public long Length { get; set; }
        public string Name { get; set; }
        public List<SequenceMetadata> Sequences { get; set; }
        public long KnownBases { get; set; }
        public string Species
        {
            get
            {
                if (Sequences.Count == 0)
                    return null;
                return Sequences[0].Species;
            }
        }
        public string Build
        {
            get
            {
                if (Sequences.Count == 0)
                    return null;
                return Sequences[0].Build;
            }
        }
        #endregion

        public List<SequenceMetadata> GetChromosomesIncludingNull()
        {
            List<SequenceMetadata> list = new List<SequenceMetadata>();
            list.AddRange(Sequences);
            list.Add(null);
            return list;
        }

        // constructor
        public enum GenomeFolderState
        {
            Ready = 0, // No action needed
            RequireWritableFolder, // Ensure we have a writable genome folder, but do nothing else (yet)
            RequireImport, // Must import (in a writable folder)
            RequireFASTACombine, // Must combine FASTA files (in a writable folder) and import
        }

        public GenomeMetadata()
        {
            Sequences = new List<SequenceMetadata>();
            IndexRegex = new Regex(@"^(\d+)\t>(.+)$", RegexOptions.Compiled);
        }

        static public SequenceType ParseSequenceType(string type)
        {
            if (type == null) return SequenceType.Unknown;
            switch (type.ToLowerInvariant())
            {
                case "althaplotype":
                    return SequenceType.AltHaplotype;
                case "autosome":
                    return SequenceType.Autosome;
                case "contig":
                    return SequenceType.Contig;
                case "decoy":
                    return SequenceType.Decoy;
                case "mitochondria":
                    return SequenceType.Mitochondria;
                case "sex":
                case "allosome":
                    return SequenceType.Allosome;
                case "other":
                    return SequenceType.Other;
                default:
                    return SequenceType.Unknown;
            }
        }


        /// <summary>
        /// Populates the genome metadata from an XML file
        /// </summary>
        public void Deserialize(string inputFilename)
        {
            // open the XML file
            inputFilename = Path.GetFullPath(inputFilename);
            string directory = Path.GetDirectoryName(inputFilename);
            Length = 0;
            KnownBases = 0; // initial
            int refIndex = 0;
            IGenomesReferencePath iGenomesReference = IGenomesReferencePath.GetReferenceFromFastaPath(directory);

            // use StreamReader to avoid URI parsing of filename that will cause problems with 
            // certain characters in the path (#). 
            using (var xmlReader = XmlReader.Create(new StreamReader(new FileStream(inputFilename, FileMode.Open, FileAccess.Read))))
            {
                while (xmlReader.Read())
                {
                    XmlNodeType nType = xmlReader.NodeType;

                    // handle 
                    if (nType == XmlNodeType.Element)
                    {
                        // retrieve the genome variables
                        if (xmlReader.Name == "sequenceSizes")
                        {
                            Name = xmlReader.GetAttribute("genomeName");
                            if (iGenomesReference != null && string.IsNullOrEmpty(Name))
                                Name = iGenomesReference.ToString();
                        }

                        // retrieve the chromosome variables
                        if (xmlReader.Name == "chromosome")
                        {
                            SequenceMetadata refSeq = new SequenceMetadata
                            {
                                FastaPath = Path.Combine(directory, xmlReader.GetAttribute("fileName")),
                                Name = xmlReader.GetAttribute("contigName"),
                                Index = refIndex++,
                                Length = long.Parse(xmlReader.GetAttribute("totalBases")),
                                Type = ParseSequenceType(xmlReader.GetAttribute("type"))
                            };
                            Length += refSeq.Length;

                            refSeq.Build = xmlReader.GetAttribute("build");
                            refSeq.Species = xmlReader.GetAttribute("species");

                            // update species and build from fasta path if in iGenomes format
                            if (iGenomesReference != null)
                            {
                                if (string.IsNullOrEmpty(refSeq.Build))
                                    refSeq.Build = iGenomesReference.Build;
                                if (string.IsNullOrEmpty(refSeq.Species))
                                    refSeq.Species = iGenomesReference.Species;
                            }

                            string isCircular = xmlReader.GetAttribute("isCircular");
                            if (!string.IsNullOrEmpty(isCircular))
                                refSeq.IsCircular = (isCircular == "true");

                            string ploidy = xmlReader.GetAttribute("ploidy");
                            if (!string.IsNullOrEmpty(ploidy)) refSeq.Ploidy = int.Parse(ploidy);

                            string md5 = xmlReader.GetAttribute("md5");
                            if (!string.IsNullOrEmpty(md5)) refSeq.Checksum = md5;

                            string knownBases = xmlReader.GetAttribute("knownBases");
                            if (!string.IsNullOrEmpty(knownBases))
                            {
                                refSeq.KnownBases = long.Parse(knownBases);
                                KnownBases += refSeq.KnownBases;
                            }

                            Sequences.Add(refSeq);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Scans the reference sequence list and returns the specified sequence metadata
        /// TODO: nulls are evil. throw an exception rather than return null. have them use TryGetSequence instead
        /// TODO: create lookup table to make this faster? 
        /// </summary>
        public SequenceMetadata GetSequence(string sequenceName)
        {
            foreach (SequenceMetadata sequence in Sequences)
            {
                if (string.Equals(sequence.Name, sequenceName, StringComparison.OrdinalIgnoreCase))
                    return sequence;
            }
            return null;
        }

        /// <summary>
        /// Scans the reference sequence list and returns the specified sequence metadata if found
        /// TODO: create lookup table to make this faster? 
        /// </summary>
        public bool TryGetSequence(string sequenceName, out SequenceMetadata foundSequence)
        {
            foreach (SequenceMetadata sequence in Sequences)
            {
                if (string.Equals(sequence.Name, sequenceName, StringComparison.OrdinalIgnoreCase))
                {
                    foundSequence = sequence;
                    return true;
                }
            }

            foundSequence = null;
            return false;
        }

     

        /// <summary>
        ///     Retrieves the FASTA filenames from the specified directory
        /// </summary>
        /// <returns>A list of FASTA filenames</returns>
        private static List<string> GetFastaFilenames(string directory)
        {
            List<string> fastaFilenames = new List<string>();

            if (Directory.Exists(directory))
            {
                DirectoryInfo info = new DirectoryInfo(directory);
                foreach (FileInfo fi in info.GetFiles("*.fa")) fastaFilenames.Add(fi.FullName);
                foreach (FileInfo fi in info.GetFiles("*.fasta")) fastaFilenames.Add(fi.FullName);
            }

            return fastaFilenames;
        }

        /// <summary>
        /// Checks if a file exists and was written to after a reference timepoint (FASTA write time)
        /// </summary>
        /// <returns>true if the file exists and the file is more recent than the FASTA file</returns>
        private static bool CheckFile(string filePath, DateTime compareTime)
        {
            bool isFileGood = File.Exists(filePath);
            if (isFileGood)
            {
                DateTime fileWriteTime = File.GetLastWriteTime(filePath);
                if (fileWriteTime < compareTime)
                {
                    // Update 6/3/13: Don't be strict about the modification times.  In practice, iGenomes can't guarantee (especially
                    // after references are copied here and there) that the modification times will be preserved.  We're more likely
                    // to create problems by rejecting index files with older modification times than we are to prevent issues due to
                    // editing the FASTA file.
                    Console.WriteLine("Warning: Modification time of FASTA file is more recent than {0}.  If FASTA file contents have been modified, please re-generate indexes to ensure they are valid.", filePath);
                }
            }
            return isFileGood;
        }

        /// <summary>
        ///     Determines the state of a reference genome folder - is it ready to go, do we need to double-check that the
        ///     folder is writable, do we need to import, do we need to combine FASTA files.
        /// </summary>
        public static GenomeFolderState CheckReferenceGenomeFolderState(string directory, bool requireBWT, bool requireBowtie)
        {
            List<string> fastaFilenames = GetFastaFilenames(directory);
            DateTime mostRecentFastaFile = DateTime.MinValue;

            // If ther's more than one FASTA, then we need to import.
            if (fastaFilenames.Count > 1)
            {
                Console.WriteLine(">>> Multiple FASTA files -> require import!");
                return GenomeFolderState.RequireImport;
            }
            if (fastaFilenames.Count == 0)
            {
                throw new IOException(string.Format("Error: No reference genome FASTA file (genome.fa) found in folder {0}", directory));
            }

            bool requireWritableFolder = false;

            // check the derivative files
            foreach (string fastaPath in fastaFilenames)
            {
                // retrieve the FASTA time
                DateTime fastaWriteTime = File.GetLastWriteTime(fastaPath);
                if (fastaWriteTime > mostRecentFastaFile) mostRecentFastaFile = fastaWriteTime;

                if (requireBowtie)
                {
                    foreach (string suffix in new[] { ".1.ebwt", ".2.ebwt", ".3.ebwt", ".4.ebwt", ".rev.1.ebwt", ".rev.2.ebwt" })
                    {
                        if (!CheckFile(Path.Combine(Path.GetDirectoryName(fastaPath),
                            Path.GetFileNameWithoutExtension(fastaPath)) + suffix, fastaWriteTime))
                        {
                            Console.WriteLine(">>> Require bowtie -> require writable");
                            requireWritableFolder = true;
                            break;
                        }
                    }
                }
                if (requireBWT)
                {
                    // Check the canonical iGenomes path:
                    string tempFolder = Path.GetDirectoryName(directory);
                    tempFolder = Path.Combine(tempFolder, "BWAIndex");
                    string fastaFileName = Path.GetFileName(fastaPath);
                    bool HasBWT = CheckFile(Path.Combine(tempFolder, fastaFileName + ".bwt"), fastaWriteTime);
                    if (!HasBWT)
                    {
                        // Check in the FASTA folder too:
                        HasBWT = CheckFile(fastaPath + ".bwt", fastaWriteTime);
                    }
                    if (!HasBWT)
                    {
                        Console.WriteLine(">>> Require bwt -> require writable");
                        requireWritableFolder = true;
                    }
                }
                // Check for FOO.fa.fai and FOO.dict:
                if (!CheckFile(fastaPath + ".fai", fastaWriteTime))
                {
                    Console.WriteLine(">>> FAI file missing -> import needed");
                    return GenomeFolderState.RequireImport;
                }
                string dictFilename = Path.Combine(Path.GetDirectoryName(fastaPath), Path.GetFileNameWithoutExtension(fastaPath)) + ".dict";
                if (!CheckFile(dictFilename, fastaWriteTime))
                {
                    Console.WriteLine(">>> Dict file missing -> import needed");
                    return GenomeFolderState.RequireImport;
                }
            }

            // check for genomesize.xml file
            if (!CheckFile(Path.Combine(directory, "GenomeSize.xml"), mostRecentFastaFile))
            {
                Console.WriteLine(">>> GenomeSize.xml file missing -> import needed");
                return GenomeFolderState.RequireImport;
            }

            if (requireWritableFolder) return GenomeFolderState.RequireWritableFolder;
            return GenomeFolderState.Ready;
        }

        public enum SequenceType : int
        {
            Unknown = 0, // not classified. Default for older GenomeSize.xml files where sequence type is not specified
            Autosome, // main chromsomes (chr1, chr2...)
            Mitochondria, // chrM
            Allosome, // sex chromosome (chrX, chrY)
            Contig, // Unplaced or unlocalized contigs (e.g. chr1_KI270711v1_random, chrUn_KI270530v1)
            Decoy,
            AltHaplotype,
            Other, // currently only chrEBV has this classification
        }

       
        public class SequenceMetadata : IComparable<SequenceMetadata>
        {
            #region member variables
            public bool IsCircular = false;
            public int Index { get; set; }
            public int Ploidy { get; set; }
            public long Length { get; set; }
            public string Build { get; set; }
            public string Checksum { get; set; }
            public string FastaPath { get; set; }
            public string Name { get; set; }
            public string Species { get; set; }
            public long KnownBases { get; set; } // bases that are not 'N'
            public SequenceType Type { get; set; }

            #endregion

            #region constructors

            public SequenceMetadata()
            {
                Ploidy = 2;
                Index = -1;
            }

            public SequenceMetadata(string name, long referenceLength, int index)
                : this()
            {
                Name = name;
                Length = referenceLength;
                Index = index;
            }

            #endregion

            /// <summary>
            /// 
            /// TODO: completely eliminate the use of this method. All human GenomeSize.xml will have the type specified
            /// 
            /// Checks if the chromosome is an autosome.
            /// Assumes species is Homo_sapiens 
            /// This metadata should be part of the GenomeSize.xml and come from the Type member, but we fall back
            /// to checking the string if the attribute is missing.
            /// </summary>
            /// <param name="chr">Name of chromosome to check.</param>
            /// <returns></returns>
            public static bool IsAutosome(string chr)
            {
                string id = chr.Replace("chr", "");
                double number;
                bool isNumber = double.TryParse(id, out number);
                return isNumber;
            }

            public bool IsAutosome()
            {
                switch (this.Type)
                {
                    case SequenceType.Autosome:
                        return true;
                    case SequenceType.Contig:
                    case SequenceType.Decoy:
                    case SequenceType.Mitochondria:
                    case SequenceType.Allosome:
                    case SequenceType.Other:
                        return false;
                    case SequenceType.Unknown:
                    default:
                        return IsAutosome(Name);
                }
            }

            /// <summary>
            /// TODO: DO NOT use this. There is too much ambiguity here when chrom is not found. The caller should use GenomeMetadata.TryGetSequence instead and then call IsDecoyOrOther on the returned SequenceMetadata. Don't have the GenomeMetadata? Tell the caller to give it to you.
            /// </summary>
            /// <param name="chrom"></param>
            /// <param name="chromosomes"></param>
            /// <returns></returns>
            public static bool IsDecoyOrOther(string chrom, List<GenomeMetadata.SequenceMetadata> chromosomes)
            {
                foreach (GenomeMetadata.SequenceMetadata referenceChromosome in chromosomes)
                {
                    if (chrom == referenceChromosome.Name && referenceChromosome.IsDecoyOrOther())
                    {
                        return true;
                    }
                }
                return false;
            }

            public bool IsDecoyOrOther()
            {
                switch (this.Type)
                {
                    case SequenceType.Decoy:
                    case SequenceType.Other:
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Checks if the chromosome is mitochondrial
            /// Assumes species is Homo_sapiens 
            /// At some point this metadata should be part of the GenomeSize.xml
            /// </summary>
            /// <param name="chr">Name of chromosome to check.</param>
            /// <returns></returns>
            public static bool IsMito(string chr)
            {
                string tempChr = chr.ToLowerInvariant();
                if (tempChr == "chrm" || tempChr == "mt") return true;
                return false;
            }

            public bool IsMito()
            {
                switch (this.Type)
                {
                    case SequenceType.Mitochondria:
                        return true;
                    case SequenceType.Contig:
                    case SequenceType.Decoy:
                    case SequenceType.Autosome:
                    case SequenceType.Allosome:
                    case SequenceType.Other:
                        return false;
                    case SequenceType.Unknown:
                    default:
                        return IsMito(Name);
                }
            }

            /// <summary>
            ///     Used when sorting the sequences according to length (descending order)
            /// </summary>
            public int CompareTo(SequenceMetadata chromosome)
            {
                return chromosome.Length.CompareTo(Length);
            }

            /// <summary>
            ///     Checks the index file for the sequence offset
            /// </summary>
            public long GetFileOffset()
            {
                string faiPath = string.Format("{0}.fai", FastaPath);

                if (!File.Exists(faiPath))
                {
                    throw new IOException(string.Format("Cannot open the FASTA index file ({0}) for reading.", faiPath));
                }

                long referenceOffset = 0;

                using (StreamReader faiReader = new StreamReader(new FileStream(faiPath, FileMode.Open, FileAccess.Read)))
                {
                    bool foundReference = false;

                    while (true)
                    {
                        // get the next line
                        string line = faiReader.ReadLine();
                        if (line == null) break;

                        // split the columns
                        string[] faiColumns = line.Split('\t');

                        if (faiColumns.Length != 5)
                        {
                            throw new InvalidDataException(string.Format("Expected 5 columns in the FASTA index file ({0}), but found {1}.",
                                faiPath, faiColumns.Length));
                        }

                        // check the reference name
                        if (faiColumns[0] == Name)
                        {
                            referenceOffset = long.Parse(faiColumns[2]);
                            foundReference = true;
                            break;
                        }
                    }

                    // sanity check
                    if (!foundReference)
                    {
                        throw new InvalidDataException(
                            string.Format("Unable to find the current sequence ({0}) in the index file ({1})", Name,
                                          faiPath));
                    }
                }

                return referenceOffset;
            }

            /// <summary>
            ///     Retrieves the bases associated with this sequence
            /// </summary>
            public string GetBases(bool toUpper = false)
            {
                long referenceOffset = GetFileOffset();
                long numRemainingBases = Length;

                StringBuilder builder = new StringBuilder();
                using (FileStream stream = new FileStream(FastaPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                using (StreamReader reader = new StreamReader(stream))
                {
                    stream.Position = referenceOffset;
                    string line = string.Empty;

                    while (numRemainingBases > 0)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            throw new IOException(string.Format(
                                    "Encountered the end of file before being able to retrieve the entire FASTA sequence. Remaining bases: {0}",
                                    numRemainingBases));
                        }
                        if (toUpper)
                        {
                            builder.Append(line.ToUpperInvariant());
                        }
                        else
                        {
                            builder.Append(line);
                        }
                        numRemainingBases -= line.Length;
                    }
                }

                return builder.ToString();
            }

            /// <summary>
            ///     Writes the current sequence to a FASTA file
            /// </summary>
            public void WriteFastaFile(string outputFastaPath)
            {
                using (FileStream readerFS = new FileStream(FastaPath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, BufferSize))
                using (StreamReader reader = new StreamReader(readerFS))
                using (StreamWriter writer = new StreamWriter(new FileStream(outputFastaPath,
                    FileMode.Create, FileAccess.Write, FileShare.Write, BufferSize)))
                {
                    // initialize
                    string line = string.Empty;
                    writer.NewLine = "\n";
                    long numRemainingBases = Length;

                    // jump to the reference offset
                    readerFS.Position = GetFileOffset();

                    // write the header
                    writer.WriteLine(">{0}", Name);

                    // write the FASTA bases
                    while (numRemainingBases > 0)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            throw new IOException(string.Format(
                                "Encountered the end of file before being able to retrieve the entire FASTA sequence. Remaining bases: {0}",
                                numRemainingBases));
                        }
                        writer.WriteLine(line);
                        numRemainingBases -= line.Length;
                    }
                }
            }
        }
    }

}