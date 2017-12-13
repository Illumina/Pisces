using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Pisces.Domain.Models;
using Alignment.Domain.Sequencing;

namespace TestUtilities
{
    public static class StitchingScenarioParser
    {
        /// <summary>
        /// Directory contains one or more csv files with the scenarios in them
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="resultFile"></param>
        /// <returns></returns>
        public static List<object[]> ParseScenariosFromDirectory(string directory, string resultFile)
        {
            if (File.Exists(resultFile))
            {
                File.Delete(resultFile);
            }

            var data = new List<object[]>();

            var inputFileDelimiter = ',';

            var inputFiles = Directory.GetFiles(directory).ToList();

            foreach (var inputFile in inputFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(inputFile);

                using (var sr = new StreamReader(File.OpenRead(inputFile)))
                {
                    sr.ReadLine();
                    while (sr.Peek() >= 0)
                    {
                        var line = sr.ReadLine();
                        var splitLine = line.Split(inputFileDelimiter);

                        var scenario = new StitchingScenario(splitLine, fileName);

                        var uniqueId = scenario.Category + "-" + scenario.Id;
                        string desiredId = null;

                        if (desiredId != null && uniqueId != desiredId)
                            continue;

                        if (scenario.Category == null)
                            continue;

                        data.Add(new object[] { scenario, resultFile });
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    /// A little class for the very abstract way we represent reads in the spreadsheet scenarios.
    /// </summary>
    public class AbstractAlignment
    {
        public readonly int Position;
        public readonly string Cigar;
        public readonly string Directions;
        private string _sequence;

        public string Sequence
        {
            get
            {
                return _sequence;
            }

            set
            {
                _sequence = value;
            }
        }

        /// <summary>
        /// Construct an abstract alignment based on the simple attributes we define in the scenarios.
        /// Optionally, pass a sequence in addition to the position, cigar, and directions.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="cigar"></param>
        /// <param name="direction"></param>
        /// <param name="sequence"></param>
        public AbstractAlignment(string position, string cigar, string direction, string sequence = null)
        {
            int.TryParse(position, out Position);
            Cigar = cigar;
            Directions = direction;

            // If the input direction was just "F", "R", we need to assign that direction to the whole read
            if (direction.Length == 1)
            {
                var cigarAlignment = new CigarAlignment(cigar);
                var cigarLength = 0;

                foreach (CigarOp op  in cigarAlignment)
                {
                    cigarLength += (int)op.Length;
                }

                Directions = cigarLength + direction;
            }
        }

        /// <summary>
        /// Returns a very basic read based on the abstract alignment. We don't yet 
        /// </summary>
        /// <returns></returns>
        public Read ToRead()
        {
            var cigar = new CigarAlignment(Cigar);
            const byte qualityForAll = 30;

            var readLength = (int)cigar.GetReadSpan();

            var alignment = new BamAlignment
            {
                CigarData = cigar,
                Position = Position  - 1,
                RefID = 1,
                Bases = Directions.EndsWith("F") ? new string('A', readLength) : new string('T', readLength),
                Qualities = Enumerable.Repeat(qualityForAll, readLength).ToArray()
            };

            var read = new Read("chr1", alignment);
            var di = new DirectionInfo(Directions);
            read.SequencedBaseDirectionMap = di.ToDirectionMap();

            return read;
        }
    }

    public class StitchingScenario
    {
        public readonly AbstractAlignment InputRead1;
        public readonly AbstractAlignment InputRead2;
        public readonly AbstractAlignment OutputRead1;
        public readonly AbstractAlignment OutputRead2;
        public bool ShouldStitch;
        public bool ShouldRefStitch;
        public readonly string Category;
        public readonly string Id;
        public readonly string Notes;

        // For variant caller testing
        public readonly AbstractAlignment InputRefRead1;
        public readonly AbstractAlignment InputRefRead2;
        public readonly AbstractAlignment OutputRefRead1;
        public readonly AbstractAlignment OutputRefRead2;
        public readonly string VarLoading;
        public readonly string RefLoading;
        public readonly string CandidateDirection;
        public readonly string Frequency;
        public readonly string ShouldBias;
        // TODO -- If we want ShouldBias to be a bool, do this -- Tamsen, it looks like some of the files are malformatted. Leaving it as a string for now.
        //public readonly bool ShouldBias;

        public StitchingScenario(string[] splitLine, string file = null)
        {
            var inputRead1StartColumn = 2;
            var inputRead2StartColumn = 5;
            var shouldStitchColumn = 14;
            var outputRead1StartColumn = 15;
            var outputRead2StartColumn = 18;
            var shouldStitchRefColumn = 21;
            var outputRef1StartColumn = 22;
            var outputRef2StartColumn = 25;

            var varLoadingColumn = 28;
            var refLoadingColumn = 29;
            var candidateDirectionColumn = 30;
            var frequencyColumn = 31;
            var biasColumn = 32;
            var notesColumn = 33;

            if (splitLine.Length < 33)
            {
                Console.WriteLine("File is not the correct format: "+file);
                return;
            }
            try
            {
                
                // For tracking purposes, in case the same Category + ID combination exists in multiple files, include the file name in the category
                Category = (file == null ? "" : file + "_") + splitLine[0];
                Id = splitLine[1];

                var parsed = bool.TryParse(splitLine[shouldStitchColumn], out ShouldStitch);
                if (!parsed)
                {
                    throw new Exception("Couldn't parse field '" + splitLine[shouldStitchColumn] +
                                        "' to boolean");
                }

                parsed = bool.TryParse(splitLine[shouldStitchRefColumn], out ShouldRefStitch);
                if (!parsed)
                {
                    throw new Exception("Couldn't parse field '" + splitLine[shouldStitchRefColumn] +
                                        "' to boolean");
                }


            
                Notes = splitLine[notesColumn];


                InputRead1 = GetAlignmentFromCellRange(inputRead1StartColumn, splitLine);
                InputRead2 = GetAlignmentFromCellRange(inputRead2StartColumn, splitLine);
                OutputRead1 = GetAlignmentFromCellRange(outputRead1StartColumn, splitLine);
                OutputRead2 = ShouldStitch
                    ? null
                    : GetAlignmentFromCellRange(outputRead2StartColumn, splitLine);

                OutputRefRead1 = GetAlignmentFromCellRange(outputRef1StartColumn, splitLine);
                OutputRefRead2 = ShouldRefStitch
                    ? null
                    : GetAlignmentFromCellRange(outputRef2StartColumn, splitLine);



                // For variant caller testing
                VarLoading = GetTrimmedCellValue(splitLine, varLoadingColumn);
                RefLoading = GetTrimmedCellValue(splitLine, refLoadingColumn);

                Frequency = GetTrimmedCellValue(splitLine, frequencyColumn);
                CandidateDirection = GetTrimmedCellValue(splitLine, candidateDirectionColumn);
                ShouldBias = GetTrimmedCellValue(splitLine, biasColumn);

            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse line " + string.Join(",", splitLine) + " in file "+ file + ": " + e.Message);
            }
        }

        private string GetTrimmedCellValue(string[] splitLine, int column)
        {
            var cellValue = splitLine[column].Trim();
            return cellValue;
        }

        private AbstractAlignment GetAlignmentFromCellRange(int startPosition, string[] splitLine)
        {
            return new AbstractAlignment(GetTrimmedCellValue(splitLine,startPosition), GetTrimmedCellValue(splitLine,startPosition + 1), GetTrimmedCellValue(splitLine,startPosition + 2));
        }
    }



}