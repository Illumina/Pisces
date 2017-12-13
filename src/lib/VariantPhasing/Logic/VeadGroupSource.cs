using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Options;
using VariantPhasing.Interfaces;
using VariantPhasing.Models;
using Common.IO.Utility;

namespace VariantPhasing.Logic
{
    public class VeadGroupSource : IVeadGroupSource
    {
        private readonly IAlignmentExtractor _alignmentExtractor;
        private readonly BamFilterParameters _options;
        private readonly bool _debugMode;
        private readonly string _debugLogRoot;

        public VeadGroupSource(IAlignmentExtractor alignmentExtractor, BamFilterParameters options, bool debugMode, string logFolder)
        {
            _alignmentExtractor = alignmentExtractor;
            _options = options;
            _debugMode = debugMode;
            _debugLogRoot = logFolder;
        }

        public IEnumerable<VeadGroup> GetVeadGroups(VcfNeighborhood neighborhood)
        {
            var veadGroups = new Dictionary<string, VeadGroup>();

            var neighbors = neighborhood.VcfVariantSites;
            var refName = neighbors.First().ReferenceName;
            _alignmentExtractor.Jump(refName);

            // keep reading the alignments while we're on the same reference sequence

            var veadMaker = new VeadFinder(_options);
            var debugLog = Path.Combine(_debugLogRoot, refName + "_" + neighborhood.Id + "_ReadsInNbhd.txt");

            WriteToReadLog(debugLog, string.Join("\t", "ReadName", "used?", "IsFirstMate", "CigarData", "Read.Position"));

            Read read = new Read();

            while (true)
            {
                if (!_alignmentExtractor.GetNextAlignment(read))
                {
                    break; // no more reads
                }


                if (ShouldSkipRead(read, neighborhood))
                {

                    WriteToReadLog(debugLog,(string.Join("\t", read.Name, "skipped", read.IsFirstMate, read.CigarData.ToString(), read.Position)));
                    continue;
                }
                if (PastNeighborhood(read, neighborhood))
                {
                    WriteToReadLog(debugLog,(string.Join("\t", read.Name, "past nbhd", read.IsFirstMate, read.CigarData.ToString(), read.Position)));
                    break;
                }


                //Make a vead and add it to our list
                var readName = read.Name + "_";
                if (read.IsFirstMate)
                    readName += "fwd_" + read.Position;
                else
                    readName += "rev_" + read.Position;

                WriteToReadLog(debugLog, (string.Join("\t", read.Name, "will use", read.IsFirstMate, read.CigarData.ToString(), read.Position)));

                //map from bases to ref position
                var vead = new Vead(readName, veadMaker.FindVariantResults(neighbors, read));

                if (vead.SiteResults == null || !vead.SiteResults.Any()) continue;

                // Add vead to a veadgroup.
                var hash = vead.ToVariantSequence();
                if (!veadGroups.ContainsKey(hash))
                {
                    veadGroups.Add(hash, new VeadGroup(vead));
                }
                else
                {
                    veadGroups[hash].AddSupport(vead);
                }
            }

            LogVeadGroupInfo(veadGroups.Values);

            return veadGroups.Values;
        }

        private void LogVeadGroupInfo(IEnumerable<VeadGroup> collapsedReads)
        {
            if (_debugMode)
            {
                Logger.WriteToLog("variant-compressed read groups as follows:  ");
                Logger.WriteToLog("count" + "\t", collapsedReads.First().ToPositions());
                foreach (var vG in collapsedReads)
                {
                    Logger.WriteToLog("\t" + vG.NumVeads + "\t" + vG);
                }
            }

            Logger.WriteToLog("Found " + collapsedReads.Count() + " variant-collapsed read groups.");

            if (_debugMode)
            {
                StringBuilder sb = new StringBuilder();
                int[] depths;
                int[] nocalls;
                VeadGroup.DepthAtSites(collapsedReads, out depths, out nocalls);
                Logger.WriteToLog("depth at sites:  ");
                Logger.WriteToLog(collapsedReads.First().ToPositions());

                for (int i = 0; i < depths.Length; i++)
                    Logger.WriteToLog(string.Join("\t", depths[i]));
            }
        }


        private void WriteToReadLog(string debugLog, string msg)
        {
            if (_debugMode)
            {
                using (StreamWriter sw = new StreamWriter(new FileStream(debugLog, FileMode.OpenOrCreate)))
                {
                    sw.WriteLine(msg);
                }
            }
        }

        private bool PastNeighborhood(Read read, VcfNeighborhood neighborhood)
        {
            return read.Position > neighborhood.LastPositionOfInterestWithLookAhead;
        }

        private bool ShouldSkipRead(Read read, VcfNeighborhood neighborhood)
        {
            if (_options.RemoveDuplicates)
            {
                if (read.IsPcrDuplicate) return true;
            }

            if (_options.OnlyUseProperPairs)
            {
                if (!read.IsProperPair) return true;
            }

            if (read.MapQuality < _options.MinimumMapQuality) return true;
            if (read.EndPosition < neighborhood.VcfVariantSites.First().VcfReferencePosition)
                return true;

            return false;
        }
    }
}
