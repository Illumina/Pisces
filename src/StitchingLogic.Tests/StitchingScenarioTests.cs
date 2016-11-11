using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Alignment.Domain.Sequencing;
using Xunit;
using Xunit.Extensions;
using TestUtilities;
using Xunit.Sdk;

namespace StitchingLogic.Tests
{

    public class StitchingScenarioTests
    {
        /// <summary>
        /// Requisite for data-driven Theory setup. 
        /// </summary>
        public static IEnumerable<object[]> ScenarioData
        {
            get
            {
                var inputDirectory = @"SharedTestData";
                var visualFile = "Stitching_Results.csv" + ".visuals.csv";
                if (File.Exists(visualFile))
                {
                    File.Delete(visualFile);
                }

                return StitchingScenarioParser.ParseScenariosFromDirectory(inputDirectory, "Stitching_Results.csv");
            }
        }

        private List<string> ExpandCigar(CigarAlignment cigar, CigarDirection directions)
        {
            var expandedDirections = new List<DirectionType>();
            foreach (var direction in directions.Directions)
            {
                for (var i = 0; i < direction.Length; i++)
                {
                    expandedDirections.Add(direction.Direction);
                }
            }
            var expandedCigar = new List<string>();

            var index = 0;
            foreach (CigarOp op in cigar)
            {
                for (var i = 0; i < op.Length; i++)
                {
                    expandedCigar.Add(op.Type.ToString()[0].ToString());
                }
            }

            var expandedCigarDirection = new List<string>();
            for (int i = 0; i < expandedCigar.Count; i++)
            {
                var cigarChunk = expandedCigar[i].ToString() + expandedDirections[i].ToString()[0];
                expandedCigarDirection.Add(cigarChunk);
            }

            return expandedCigarDirection;
        } 

        /// <summary>
        /// Log result info to a result file. Doesn't directly impact test, just useful for looking at the results all together.
        /// (Also useful for a deliverable output summary).
        /// </summary>
        /// <param name="resultFile"></param>
        /// <param name="scenario"></param>
        /// <param name="didStitch"></param>
        /// <param name="resultSet"></param>
        private void LogResult(string resultFile, StitchingScenario scenario, bool didStitch, AlignmentSet resultSet = null, string message = null)
        {
            var diagramLength = 12;
            // This is useful for looking at the results across the full test set.

            const string delimiter = ",";

            var visualResultsFile = resultFile + ".visuals.csv";

            if (!File.Exists(visualResultsFile))
            {
                using (var sw = File.CreateText(visualResultsFile))
                {
                    var leftOfDiagram = new List<string>() {"ID", "Pos", "Cigar", "Dirs", "Diagram Var"};
                    var varDiagram = Enumerable.Repeat("", diagramLength);
                    var leftOfRef = new List<string>() {"Pos", "Cigar", "Dirs", "Diagram Ref" };
                    var refDiagram = Enumerable.Repeat("", diagramLength);
                    var leftOfStitched = new List<string>() { "Pos", "Cigar", "Dirs", "Diagram Stitched" };
                    var stitchedDiagram = Enumerable.Repeat("", diagramLength);
                    sw.WriteLine(string.Join(delimiter, 
                        leftOfDiagram.
                        Concat(varDiagram).
                        Concat(leftOfRef).
                        Concat(refDiagram).
                        Concat(leftOfStitched).
                        Concat(stitchedDiagram)));
                }
            }


            using (var sw = File.AppendText(visualResultsFile))
            {
                Read stitchedRead = null;
                if (resultSet != null && resultSet.ReadsForProcessing.Any())
                {
                    stitchedRead = resultSet.ReadsForProcessing.First();
                }

                // First row
                var leftOfDiagram = new List<string>() {scenario.Category + "-" + scenario.Id,
                    scenario.InputRead1.Position.ToString(), scenario.InputRead1.Cigar, scenario.InputRead1.Directions};
                var r1Cigar = new CigarAlignment(scenario.InputRead1.Cigar);
                var r2Cigar = new CigarAlignment(scenario.InputRead2.Cigar);
                var r1BasesStart = scenario.InputRead1.Position - 1 - (int)r1Cigar.GetPrefixClip();
                var r2BasesStart = scenario.InputRead2.Position - 1 - (int)r2Cigar.GetPrefixClip();

                if (r1BasesStart < 0 || r2BasesStart < 0)
                {
                    throw new ArgumentException("Test scenario has invalid position/cigar combination: " + scenario.InputRead1.Position + ":" + scenario.InputRead1.Cigar + " or " + scenario.InputRead2.Position + ":" + scenario.InputRead2.Cigar);
                }
                if (r1BasesStart < 0) r1BasesStart = 0;
                if (r2BasesStart < 0) r2BasesStart = 0;


                var r2CigarLength = 0;
                foreach (CigarOp op in r2Cigar)
                {
                    r2CigarLength += (int)op.Length;
                }

                var r2BasesEnd = r2BasesStart + r2CigarLength;

                var preOverlapCigar = new CigarAlignment(scenario.InputRead1.Cigar).GetClippedCigar(0, (int)(r2BasesStart - r1BasesStart) + 1, includeWholeEndIns: true);
                var insertionsPreOverlap = preOverlapCigar.CountOperations('I');

                var expectedReadLength = r2BasesEnd - r1BasesStart + insertionsPreOverlap;
                r2BasesStart = r2BasesStart + insertionsPreOverlap;
                
                var varDiagram = Enumerable.Repeat("",r1BasesStart).Concat(ExpandCigar(r1Cigar,
                    new CigarDirection(scenario.InputRead1.Directions))).ToList();
                varDiagram = varDiagram.Concat(Enumerable.Repeat("", diagramLength - varDiagram.Count()).ToList()).ToList();
                var leftOfRef = new List<string>() { scenario.InputRead2.Position.ToString(), scenario.InputRead2.Cigar, scenario.InputRead2.Directions};
                var refDiagram = Enumerable.Repeat("", diagramLength);
                var leftOfStitched = Enumerable.Repeat("", 3).ToList();
                var stitchedDiagram = Enumerable.Repeat("NA", diagramLength);
                if (stitchedRead != null && stitchedRead.CigarDirections != null)
                {
                    var stitchedBasesStart = stitchedRead.Position - 1 - (int)stitchedRead.CigarData.GetPrefixClip();
                    leftOfStitched = new List<string>() { stitchedRead.Position.ToString(), stitchedRead.CigarData.ToString(), GetDirectionsString(stitchedRead) };
                    stitchedDiagram = Enumerable.Repeat("", stitchedBasesStart).Concat(ExpandCigar(stitchedRead.CigarData,
                        stitchedRead.CigarDirections));
                }

                sw.WriteLine(string.Join(delimiter,
                    leftOfDiagram.
                    Concat(varDiagram).
                    Concat(leftOfRef).
                    Concat(refDiagram).
                    Concat(leftOfStitched).
                    Concat(stitchedDiagram)));

                // Second row
                var varDiagramR2 =
                    Enumerable.Repeat("", r2BasesStart)
                        .Concat(ExpandCigar(r2Cigar,
                            new CigarDirection(scenario.InputRead2.Directions)));

                varDiagramR2 = varDiagramR2.Concat(Enumerable.Repeat("", diagramLength - varDiagramR2.Count())).ToList();
                var leftOfDiagramPad = new List<string>()
                {
                    "",
                    scenario.InputRead2.Position.ToString(),
                    scenario.InputRead2.Cigar,
                    scenario.InputRead2.Directions
                };

                var leftOfRefPad = Enumerable.Repeat("", leftOfRef.Count);
                var leftOfStitchedPad = Enumerable.Repeat("", leftOfStitched.Count);
                var refDiagramR2 = Enumerable.Repeat("", diagramLength);

                var totalBasesCovered =
                    Enumerable.Repeat("", r1BasesStart).Concat(Enumerable.Repeat("+", expectedReadLength)).ToList();
                sw.WriteLine(string.Join(delimiter,
                    leftOfDiagramPad.
                    Concat(varDiagramR2).
                    Concat(leftOfRefPad).
                    Concat(refDiagramR2).
                    Concat(leftOfStitchedPad).
                    Concat(totalBasesCovered)
                    ));

                sw.WriteLine();

            }

            if (!File.Exists(resultFile))
            {
                // Create a file to write to, and write the header.
                using (var sw = File.CreateText(resultFile))
                {
                    sw.WriteLine(string.Join(delimiter,new[] {"ID",
                        "R1_Pos","R1_Cigar","R1_Dirs",
                        "R2_Pos","R2_Cigar","R2_Dirs",
                        "ShouldStitch","DidStitch",
                        "Exp_SR_Pos","Exp_SR_Cigar","Exp_SR_Dirs",
                        "Actual_SR_Pos","Actual_SR_Cigar","Actual_SR_Dirs",
                        "Notes","Pass", "Message"}));
                }
            }


            using (var sw = File.AppendText(resultFile))
            {
                // Add everything we know from the input scenario, and whether it did stitch.
                var fields = new List<string>() {scenario.Category + "-" + scenario.Id,
                    scenario.InputRead1.Position.ToString(), scenario.InputRead1.Cigar, scenario.InputRead1.Directions,
                    scenario.InputRead2.Position.ToString(), scenario.InputRead2.Cigar, scenario.InputRead2.Directions,
                    scenario.ShouldStitch.ToString(), didStitch.ToString(),
                    scenario.OutputRead1.Position.ToString(), scenario.OutputRead1.Cigar, scenario.OutputRead1.Directions,
                };

                var stitchResultsMatch = false;
                var cigarResultsMatch = false;
                var directionResultsMatch = false;

                stitchResultsMatch = scenario.ShouldStitch == didStitch;

                // Add the info from the output reads
                if (resultSet != null && resultSet.ReadsForProcessing.Any() && resultSet.ReadsForProcessing.First().CigarDirections != null)
                {
                    var stitchedRead = resultSet.ReadsForProcessing.First();
                    var directions = GetDirectionsString(stitchedRead);
                     
                    fields.AddRange(new List<string>()
                    {
                        stitchedRead.Position.ToString(),
                        stitchedRead.CigarData.ToString(),
                        directions
                    });

                    cigarResultsMatch = !scenario.ShouldStitch || OutputCigarsMatch(scenario, resultSet);
                    directionResultsMatch = !scenario.ShouldStitch || OutputDirectionsMatch(scenario, resultSet);
                }
                else
                {
                    fields.AddRange(new List<string>() {"","",""});
                }

                // Determine if this scenario "Passed" (i.e. matched expectations). (TODO if the resultSet is null, it failed -- is that valid?)
                var testResult = (!scenario.ShouldStitch && stitchResultsMatch) || (stitchResultsMatch && cigarResultsMatch && directionResultsMatch);
                fields.Add(Sanitize(scenario.Notes, Convert.ToChar(delimiter)));
                fields.Add(testResult.ToString());

                fields.Add(message);
                // Write scenario results to file
                sw.WriteLine(string.Join(delimiter, fields));
            }


        }

        private string Sanitize(string inputString, char delimiter)
        {
            // Assumes we're never doing semi-colon separated
            return inputString.Replace(delimiter, ';');
        }


        [Theory, PropertyData("ScenarioData")]
        public void RunStitchingScenarios(StitchingScenario scenario, string resultFile)
        {
            Console.WriteLine(scenario.Category + " " + scenario.Id);

            bool resultLogged = false;

            try
            {
                var stitcher = new BasicStitcher(10);

                var inputRead1 = scenario.InputRead1.ToRead();
                var inputRead2 = scenario.InputRead2.ToRead();

                var alignmentSet = new AlignmentSet(inputRead1, inputRead2);
                var didStitch = stitcher.TryStitch(alignmentSet);

                LogResult(resultFile, scenario, didStitch, alignmentSet);
                resultLogged = true;


                if (scenario.ShouldStitch)
                {
                    Assert.Equal(scenario.ShouldStitch, didStitch);
                    Assert.True(OutputDirectionsMatch(scenario, alignmentSet));
                    Assert.True(OutputCigarsMatch(scenario, alignmentSet));
                }

                if (didStitch)
                {
                    // TODO fix scenario source files so we can turn this back on.
                    Assert.True(OutputCigarsMatch(scenario, alignmentSet));
                }
            }
            catch (Exception ex)
            {
                if (!resultLogged)
                    LogResult(resultFile, scenario, false, null, message: "Threw exception: "+ex.Message);
                throw;
            }
        }
        
        private bool OutputDirectionsMatch(StitchingScenario scenario, AlignmentSet alignmentSet)
        {
            var directions = GetDirectionsString(alignmentSet.ReadsForProcessing.First());
            return scenario.OutputRead1.Directions == directions;
        }

        private bool OutputCigarsMatch(StitchingScenario scenario, AlignmentSet alignmentSet)
        {
            return scenario.OutputRead1.Cigar == alignmentSet.ReadsForProcessing.First().CigarData.ToString();
        }

        private string GetDirectionsString(Read read)
        {           
            var directionsRaw = read.CigarDirections.ToString();
            return directionsRaw;
        }

    }
}
