using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Alignment.Domain.Sequencing;
using Pisces.Domain.Models;
using Pisces.Domain.Types;
using Pisces.Domain.Utility;
using Common.IO.Utility;
using StitchingLogic.Models;

namespace StitchingLogic
{
    public class StitchedCigarLengthException : Exception
    {
        public StitchedCigarLengthException(string message) : base(message)
        {
        }

        public StitchedCigarLengthException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }

        public StitchedCigarLengthException(Exception innerException, string message, params object[] args)
            : base(string.Format(message, args), innerException)
        {
        }
    }

    public class CigarReconciler
    {
        private const int MaxBaseQuality = 93;
        private int _minBasecallQuality;

        private readonly ReadStatusCounter _statusCounter;
        private bool _debug;
        private readonly bool _ignoreProbeSoftclips;
        private readonly bool _allowTerminalClipsToSupportOverlappingDels;
        private readonly bool _ignoreReadsAboveMaxStitchedLength;
        private bool _useSoftclippedBases;

        // For performance reasons, avoid allocating memory repeatedly
        private readonly StitchedPosition[] _stitchPositions;
        private readonly StitchedPosition[] _stitchPositionsList;
        private readonly List<CigarOp> _expandedCigar1;
        private readonly List<CigarOp> _expandedCigar2;

        private int _positionsUsed;
        private List<char?> _r2Bases;
        private List<char?> _r1Bases;
        private List<byte?> _r1Quals;
        private List<byte?> _r2Quals;
        private bool _nifyDisagreements;
        private int maxStitchedPosition;
        private int _stitchedPositionsListLength;
        private int _totalProcessed;

        public CigarReconciler(ReadStatusCounter statusCounter, bool useSoftclippedBases, bool debug, bool ignoreProbeSoftclips, int maxReadLength, int minBaseCallQuality, bool allowTerminalClipsToSupportOverlappingDels = true, bool ignoreReadsAboveMaxStitchedLength = false, bool nifyDisagreements = false)
        {
            _statusCounter = statusCounter;
            _useSoftclippedBases = useSoftclippedBases;
            _debug = debug;
            _ignoreProbeSoftclips = ignoreProbeSoftclips;
            _allowTerminalClipsToSupportOverlappingDels = allowTerminalClipsToSupportOverlappingDels;
            _ignoreReadsAboveMaxStitchedLength = ignoreReadsAboveMaxStitchedLength;

            var maxStitchedReadLength = maxReadLength * 2 - 1;
            _stitchPositions = new StitchedPosition[maxStitchedReadLength];
            _stitchPositionsList = new StitchedPosition[maxStitchedReadLength];
            _expandedCigar1 = new List<CigarOp>(maxReadLength);
            _expandedCigar2 = new List<CigarOp>(maxReadLength);
            _nifyDisagreements = nifyDisagreements;
            _minBasecallQuality = minBaseCallQuality;

            // For performance reasons, we keep one collection of StitchedPositions and recycle them for each pair we try to stitch
            for (var i = 0; i < _stitchPositions.Length; i++)
            {
                _stitchPositions[i] = new StitchedPosition();
            }
        }

        private StitchingInfo GetSuperDuperSimpleStitchedCigarFriendlier(CigarAlignment cigar1, int pos1, CigarAlignment cigar2,
          int pos2, bool reverseFirst, string r1OrigBases, string r2OrigBases, byte[] r1OrigQuals, byte[] r2OrigQuals)
        {
            var superClean = (cigar1.Count == 1 && cigar2.Count == 1 & cigar1[0].Type == 'M' && cigar2[0].Type == 'M');

            var softClipPrefix1 = cigar1.GetPrefixClip();
            var softClipPrefix2 = cigar2.GetPrefixClip();
            var softClipSuffix1 = cigar1.GetSuffixClip();
            var softClipSuffix2 = cigar2.GetSuffixClip();

            if (softClipSuffix1 > 0 || softClipPrefix2 > 0)
            {
                return null;
            }


            var r1End = pos1 + cigar1.GetReadSpan();
            var r2End = pos2 + cigar2.GetReadSpan();

            if (pos2 < r1End)
            {
                var overlapLength = (int)(r1End - pos2);
                var r1Length = r1OrigBases.Length;
                var r2Length = r2OrigBases.Length;
                var r1FirstBaseOverlap = r1OrigBases.Length - overlapLength;

                if (softClipPrefix1 >= r1FirstBaseOverlap || (softClipSuffix2 > 0 && r2Length - softClipSuffix2  < overlapLength))
                {
                    // No softclip in overlap for the simple case
                    return null;
                }
                var r1OverlapBases = r1OrigBases.Substring(r1FirstBaseOverlap, overlapLength);
                var r2OverlapBases = r2OrigBases.Substring(0, overlapLength);

                if (r1OverlapBases != r2OverlapBases)
                {
                    return null;
                }

                if (!superClean)
                {
                    var r1Ops = cigar1.Expand();
                    var r2Ops = cigar2.Expand();

                    var r1OverlapOps = string.Join("", r1Ops.Select(x => x.Type)).Substring(r1FirstBaseOverlap, overlapLength);
                    var r2OverlapOps = string.Join("", r2Ops.Select(x => x.Type)).Substring(0, overlapLength);

                    if (r1OverlapOps != r2OverlapOps)
                    {
                        return null;
                    }

                    if ((r1OverlapOps + r2OverlapOps).Contains("S"))
                    {
                        return null;
                    }

                }

                var r1BeforeOverlapLength = r1Length - overlapLength;
                var r2AdditionalBases = r2OrigBases.Substring(overlapLength);
                var r2AfterOverlapLength = r2AdditionalBases.Length;
                var bases = r1OrigBases + r2AdditionalBases;

                var quals = new byte[bases.Length];
                for (int i = 0; i < r1BeforeOverlapLength; i++)
                {
                    quals[i] = r1OrigQuals[i];
                }

                for (int i = 0; i < overlapLength; i++)
                {
                    var adjustedI = i + r1BeforeOverlapLength;

                    var r1Qual = r1OrigQuals[adjustedI];
                    var r2Qual = r2OrigQuals[i];

                    var sumQuality = Convert.ToInt32((byte)r1Qual) +
                                     Convert.ToInt32((byte)r2Qual);
                    var stitchedQuality = sumQuality > MaxBaseQuality ? MaxBaseQuality : sumQuality;
                    var qualToAdd = (byte)stitchedQuality;

                    quals[adjustedI] = qualToAdd;
                }

                for (int i = 0; i < r2AfterOverlapLength; i++)
                {
                    var adjustedI = i + r1Length;
                    quals[adjustedI] = r2OrigQuals[i + overlapLength];
                }

                var beforeDirString = r1BeforeOverlapLength > 0 ? $"{r1BeforeOverlapLength}{(reverseFirst ? "R" : "F")}" : "";
                var afterDirString = r2AfterOverlapLength > 0
                    ? $"{r2AfterOverlapLength}{(reverseFirst ? "F" : "R")}"
                    : "";

                var matchLength = bases.Length - softClipPrefix1 - softClipSuffix2;
                var prefixCigar = softClipPrefix1 > 0 ? $"{softClipPrefix1}S" : "";
                var suffixCigar = softClipSuffix2 > 0 ? $"{softClipSuffix2}S" : "";

                var stitchingInfo = new StitchingInfo()
                {
                    StitchedBases = bases.ToCharArray().Select(x => x as char?).ToList(),
                    StitchedQualities = quals.ToList(),
                    StitchedCigar = new CigarAlignment($"{prefixCigar}{matchLength}M{suffixCigar}"),
                    StitchedDirections = new CigarDirection($"{beforeDirString}{overlapLength}S{afterDirString}")
                };

                return stitchingInfo;
            }
            else
            {
                return null;
            }
        }


        private StitchingInfo GetSuperDuperSimpleStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2,
            int pos2, bool reverseFirst, string r1OrigBases, string r2OrigBases, byte[] r1OrigQuals, byte[] r2OrigQuals, bool r1IsFirstMate)
        {
            var superClean = (cigar1.Count == 1 && cigar2.Count == 1 & cigar1[0].Type == 'M' && cigar2[0].Type == 'M');
            //if (!superClean)
            //{
            //    return null;
            //}

            uint softClipPrefix1 = 0;
            uint match1 = 0;
            uint softClipSuffix1 = 0;

            uint softClipPrefix2 = 0;
            uint match2 = 0;
            uint softClipSuffix2 = 0;

            if (!GetSimpleCigarComponents(cigar1, ref softClipPrefix1, ref match1, ref softClipSuffix1))
            {
                _statusCounter.AddStatusCount("Not simple");
                // The cigar is not simple.
                return null;
            }

            if (!GetSimpleCigarComponents(cigar2, ref softClipPrefix2, ref match2, ref softClipSuffix2))
            {
                _statusCounter.AddStatusCount("Not simple");
                // The cigar is not simple.
                return null;
            }

            if (softClipSuffix1 + softClipPrefix2 > 0)
            {
                _statusCounter.AddStatusCount("R2 hs prefix clip");
                return null;
            }

            var posGap = pos2 - pos1;
            if ((posGap > 0 && posGap > match1) || (posGap < 0 && posGap * -1 > match2))
            {
                _statusCounter.AddStatusCount("Cigars don't overlap");

                // The cigars don't overlap. We can't stitch them.
                return null;
            }


            var r1End = pos1 + cigar1.GetReferenceSpan();
            var r2End = pos2 + cigar2.GetReferenceSpan();

            if (pos2 < r1End && r1End <=  r2End) // Not dealing with r1 going past r2 here for now
            {
                var overlapLength = (int)(r1End - pos2);
                var r1Length = r1OrigBases.Length;
                var r1FirstBaseOverlap = r1OrigBases.Length - overlapLength;

                if (softClipPrefix1 >= r1FirstBaseOverlap  || (softClipSuffix2 > 0 && match2 < overlapLength))
                {
                    _statusCounter.AddStatusCount("Softclip in overlap");

                    // No softclip in overlap for the simple case
                    return null;
                }

                if (overlapLength <= 0)
                {
                    _statusCounter.AddStatusCount("No overlap");

                    return null;
                }

                var r1OverlapBases = r1OrigBases.Substring(r1FirstBaseOverlap, overlapLength);
                var r2OverlapBases = r2OrigBases.Substring(0, overlapLength);

                var numDisagreements = 0;

                if (!superClean)
                {
                    var r1Ops = cigar1.Expand();
                    var r2Ops = cigar2.Expand();

                    for (int i = 0; i < overlapLength; i++)
                    {
                        var indexInR1 = r1FirstBaseOverlap + i;
                        var indexInR2 = i;

                        if (r1Ops[indexInR1].Type != r2Ops[indexInR2].Type)
                        {
                            _statusCounter.AddStatusCount("Overlap ops mismatch");
                            return null;
                        }
                    }

                    //var r1OverlapOps = string.Join("", r1Ops.Select(x => x.Type)).Substring(r1FirstBaseOverlap, overlapLength);
                    //var r2OverlapOps = string.Join("", r2Ops.Select(x => x.Type)).Substring(0, overlapLength);

                    //if (r1OverlapOps != r2OverlapOps)
                    //{
                    //    _statusCounter.AddStatussCount("Overlap ops mismatch");

                    //    return null;
                    //}

                }

                var r1BeforeOverlapLength = r1Length - overlapLength;
                var r1BeforeOverlapBases = r1OrigBases.Substring(0, r1BeforeOverlapLength);
                var r2AdditionalBases = r2OrigBases.Substring(overlapLength);
                var r2AfterOverlapLength = r2AdditionalBases.Length;
                var stitchedReadLength = r1OrigBases.Length + r2AfterOverlapLength;

                var stitchedBases = r1OverlapBases.ToCharArray();

                var quals = new byte[stitchedReadLength];
                for (int i = 0; i < r1BeforeOverlapLength; i++)
                {
                    quals[i] = r1OrigQuals[i];
                }

                for (int i = 0; i < overlapLength; i++)
                {
                    var adjustedI = i + r1BeforeOverlapLength;

                    var r1Qual = r1OrigQuals[adjustedI];
                    var r2Qual = r2OrigQuals[i];

                    var sumQuality = Convert.ToInt32((byte)r1Qual) +
                                     Convert.ToInt32((byte)r2Qual);

                    var stitchedQuality = sumQuality > MaxBaseQuality ? MaxBaseQuality : sumQuality;
                    var qualToAdd = (byte)stitchedQuality;

                    var mismatchingBases = r1OverlapBases[i] != r2OverlapBases[i];
                    if (mismatchingBases)
                    {
                        numDisagreements++;

                        char baseToAdd;
                        if (r1IsFirstMate)
                        {
                            if (r1Qual >= r2Qual)
                            {
                                baseToAdd = r1OverlapBases[i];
                                qualToAdd = r1Qual;
                            }
                            else
                            {
                                baseToAdd = r2OverlapBases[i];
                                qualToAdd = r2Qual;
                            }
                        }
                        else
                        {
                            if (r2Qual >= r1Qual)
                            {
                                baseToAdd = r2OverlapBases[i];
                                qualToAdd = r2Qual;
                            }
                            else
                            {
                                baseToAdd = r1OverlapBases[i];
                                qualToAdd = r1Qual;
                            }

                        }
                        if (r1Qual > _minBasecallQuality && r2Qual > _minBasecallQuality)
                        {
                            qualToAdd = 0;
                            if (_nifyDisagreements)
                            {
                                baseToAdd = 'N';
                            }
                        }

                        stitchedBases[i] = baseToAdd;
                    }

                    quals[adjustedI] = qualToAdd;
                }

                var bases = r1BeforeOverlapBases + new string(stitchedBases) + r2AdditionalBases;

                for (int i = 0; i < r2AfterOverlapLength; i++)
                {
                    var adjustedI = i + r1Length; 
                    quals[adjustedI] = r2OrigQuals[i + overlapLength];
                }

                var beforeDirString = r1BeforeOverlapLength > 0 ? $"{r1BeforeOverlapLength}{(reverseFirst ? "R" : "F")}" : "";
                var afterDirString = r2AfterOverlapLength > 0
                    ? $"{r2AfterOverlapLength}{(reverseFirst ? "F" : "R")}"
                    : "";

                var matchLength = bases.Length - softClipPrefix1 - softClipSuffix2;
                var prefixCigar = softClipPrefix1 > 0 ? $"{softClipPrefix1}S" : "";
                var suffixCigar = softClipSuffix2 > 0 ? $"{softClipSuffix2}S" : "";

                if (bases.Length > _stitchPositions.Length)
                {
                    // Added this here to be consistent with non-super-simple
                    throw new StitchedCigarLengthException("Combined length of reads is greater than the expected maximum stitched read length of " + _stitchPositions.Length);

                }
                var stitchingInfo = new StitchingInfo()
                {
                    StitchedBases = bases.ToCharArray().Select(x=> x as char?).ToList(),
                    StitchedQualities = quals.ToList(),
                    StitchedCigar = new CigarAlignment($"{prefixCigar}{matchLength}M{suffixCigar}"),
                    StitchedDirections = new CigarDirection($"{beforeDirString}{overlapLength}S{afterDirString}"),
                    NumDisagreeingBases = numDisagreements
                };

                
                // TODO this is just in here to test
                //var map = CreateSequencedBaseDirectionMap(stitchingInfo.StitchedDirections.Expand().ToArray(),
                //    stitchingInfo.StitchedCigar);

                return stitchingInfo;
            }
            else
            {
                return null;
            }
        }

        //public static DirectionType[] CreateSequencedBaseDirectionMap(DirectionType[] cigarBaseDirectionMap, CigarAlignment cigarData)
        //{
        //    var cigarBaseAlleleMap = cigarData.Expand();
        //    var sequencedBaseDirectionMap = new DirectionType[cigarData.GetReadSpan()];

        //    int sequencedBaseIndex = 0;
        //    for (int cigarBaseIndex = 0; cigarBaseIndex < cigarBaseDirectionMap.Length; cigarBaseIndex++)
        //    {
        //        var cigarOp = cigarBaseAlleleMap[cigarBaseIndex];

        //        if (cigarOp.IsReadSpan()) //choices: (MIDNSHP)
        //        {
        //            sequencedBaseDirectionMap[sequencedBaseIndex] = cigarBaseDirectionMap[cigarBaseIndex];
        //            sequencedBaseIndex++;
        //        }

        //    }
        //    return sequencedBaseDirectionMap;
        //}

        private StitchingInfo GetSuperSimpleStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2,
            int pos2, bool reverseFirst, string r1OrigBases, string r2OrigBases, byte[] r1OrigQuals, byte[] r2OrigQuals)
        {
            string bases = "";
            uint softClipPrefix1 = 0;
            uint match1 = 0;
            uint softClipSuffix1 = 0;

            uint softClipPrefix2 = 0;
            uint match2 = 0;
            uint softClipSuffix2 = 0;

            if (!GetSimpleCigarComponents(cigar1, ref softClipPrefix1, ref match1, ref softClipSuffix1))
            {
                // The cigar is not simple.
                return null;
            }

            if (!GetSimpleCigarComponents(cigar2, ref softClipPrefix2, ref match2, ref softClipSuffix2))
            {
                // The cigar is not simple.
                return null;
            }

            if (softClipPrefix1 + softClipPrefix2 + softClipSuffix1 + softClipSuffix2 > 0)
            {
                return null;
            }

            var posGap = pos2 - pos1;
            if ((posGap > 0 && posGap > match1) || (posGap < 0 && posGap * -1 > match2))
            {
                // The cigars don't overlap. We can't stitch them.
                return null;
            }

            var r1End = pos1 + cigar1.GetReferenceSpan();
            var r2End = pos2 + cigar2.GetReferenceSpan();

            if (pos2 < r1End)
            {
                var overlapLength = (int)(r1End - pos2);
                var r1Length = r1OrigBases.Length;
                var r1OverlapBases = r1OrigBases.Substring(r1OrigBases.Length - overlapLength, overlapLength);
                var r2OverlapBases = r2OrigBases.Substring(0, overlapLength);

                if (r1OverlapBases != r2OverlapBases)
                {
                    return null;
                }

                var r1BeforeOverlapLength = r1Length - overlapLength;
                var r2AdditionalBases = r2OrigBases.Substring(overlapLength);
                var r2AfterOverlapLength = r2AdditionalBases.Length;
                bases = r1OrigBases + r2AdditionalBases;

                var quals = new byte[bases.Length];
                for (int i = 0; i < r1BeforeOverlapLength; i++)
                {
                    quals[i] = r1OrigQuals[i];
                }

                for (int i = 0; i < overlapLength; i++)
                {
                    var adjustedI = i + r1BeforeOverlapLength;

                    var r1Qual = r1OrigQuals[adjustedI];
                    var r2Qual = r2OrigQuals[i];

                    var sumQuality = Convert.ToInt32((byte)r1Qual) +
                                     Convert.ToInt32((byte)r2Qual);
                    var stitchedQuality = sumQuality > MaxBaseQuality ? MaxBaseQuality : sumQuality;
                    var qualToAdd = (byte)stitchedQuality;

                    quals[adjustedI] = qualToAdd;
                }

                for (int i = 0; i < r2AfterOverlapLength; i++)
                {
                    var adjustedI = i + r1Length;
                    quals[adjustedI] = r2OrigQuals[i + overlapLength];
                }

                var beforeDirString = r1BeforeOverlapLength > 0 ? $"{r1BeforeOverlapLength}{(reverseFirst ? "R" : "F")}" : "";
                var afterDirString = r2AfterOverlapLength > 0
                    ? $"{r2AfterOverlapLength}{(reverseFirst ? "F" : "R")}"
                    : "";
                var stitchingInfo = new StitchingInfo()
                {
                    StitchedBases = bases.ToCharArray().Select(x => x as char?).ToList(),
                    StitchedQualities = quals.ToList(),
                    StitchedCigar = new CigarAlignment(bases.Length + "M"),
                    StitchedDirections = new CigarDirection($"{beforeDirString}{overlapLength}S{afterDirString}")
                };

                return stitchingInfo;
            }
            else
            {
                return null;
            }

            //var cigar1DirectionType = reverseFirst ? DirectionType.Reverse : DirectionType.Forward;
            //var cigar2DirectionType = reverseFirst ? DirectionType.Forward : DirectionType.Reverse;

            //StitchingInfo stitchingInfo = new StitchingInfo();

            //int totalMatchBases = Math.Max((int)match1, (int)match2 + posGap) - Math.Min(0, posGap);

            //stitchingInfo.StitchedCigar = new CigarAlignment(totalMatchBases + "M");
            //stitchingInfo.StitchedDirections = new CigarDirection();

            //var numStitchedBases = (int) (match1 + match2 - totalMatchBases);
            //var numTrailingBases = (int) (match2 - numStitchedBases);

            //if (posGap > 0)
            //{
            //    stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar1DirectionType, posGap));
            //    stitchingInfo.StitchedBases.AddRange(r1OrigBases.Substring(0, posGap).ToCharArray()
            //        .Select(x => x as char?));
            //    stitchingInfo.StitchedQualities.AddRange(r1OrigQuals.Skip(0).Take(posGap));
            //}

            //if (numStitchedBases > 0)
            //{
            //    stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(DirectionType.Stitched,
            //        numStitchedBases));

            //    stitchingInfo.StitchedBases.AddRange(r1OrigBases.Substring(posGap, numStitchedBases).ToCharArray()
            //        .Select(x => x as char?));
            //    stitchingInfo.StitchedQualities.AddRange(r1OrigQuals.Skip(posGap - 1).Take(numStitchedBases));
            //}

            //if (numTrailingBases > 0)
            //{
            //    stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar2DirectionType, numTrailingBases));
            //    stitchingInfo.StitchedBases.AddRange(r2OrigBases.Substring(numStitchedBases, numTrailingBases)
            //        .ToCharArray().Select(x => x as char?));
            //    stitchingInfo.StitchedQualities.AddRange(r2OrigQuals.Skip(numStitchedBases - 1).Take(numTrailingBases));
            //}


            //return stitchingInfo;
        }

        private StitchingInfo GetSimpleStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2, int pos2, bool reverseFirst, string r1OrigBases, string r2OrigBases, byte[] r1OrigQuals, byte[] r2OrigQuals)
        {
            //if (!_useSoftclippedBases)
            //{
            //    throw new NotImplementedException("Cannot call GetSimpleStitchedCigar with UseSoftclippedBases false");
            //}

            string bases = "";
            uint softClipPrefix1 = 0;
            uint match1 = 0;
            uint softClipSuffix1 = 0;

            uint softClipPrefix2 = 0;
            uint match2 = 0;
            uint softClipSuffix2 = 0;

            if (!GetSimpleCigarComponents(cigar1, ref softClipPrefix1, ref match1, ref softClipSuffix1))
            {
                // The cigar is not simple.
                return null;
            }

            if (!GetSimpleCigarComponents(cigar2, ref softClipPrefix2, ref match2, ref softClipSuffix2))
            {
                // The cigar is not simple.
                return null;
            }

            // We have 2 simple cigars.

            var posGap = pos2 - pos1;
            if ((posGap > 0 && posGap > match1) || (posGap < 0 && posGap * -1 > match2))
            {
                // The cigars don't overlap. We can't stitch them.
                return null;
            }

            // If we got here, the cigars look simple and they overlap in a sensible way.

            var cigar1DirectionType = reverseFirst ? DirectionType.Reverse : DirectionType.Forward;
            var cigar2DirectionType = reverseFirst ? DirectionType.Forward : DirectionType.Reverse;

            StitchingInfo stitchingInfo = new StitchingInfo();

            int totalSoftClipPrefixBases = 0;

            var indexInR1 = 0;
            var indexInR2 = 0;

            // Prefer soft clip prefix bases from read 2
            int stitchedSoftClipPrefixDirection2 = (int)softClipPrefix2 - posGap;

            if (_ignoreProbeSoftclips)
            {
                // Only use read1 prefix soft clip bases if there are more of them than in read 2
                stitchingInfo.IgnoredProbePrefixBases = Math.Max(0, stitchedSoftClipPrefixDirection2);
            }

            int stitchedSoftClipPrefixDirection1 = (int)softClipPrefix1 - stitchingInfo.IgnoredProbePrefixBases;

            if (stitchedSoftClipPrefixDirection1 > 0)
            {
                totalSoftClipPrefixBases += stitchedSoftClipPrefixDirection1;

                if (stitchedSoftClipPrefixDirection2 <= 0)
                {
                    stitchedSoftClipPrefixDirection1 -= stitchedSoftClipPrefixDirection2;
                }

                stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar1DirectionType, stitchedSoftClipPrefixDirection1));
                bases += r1OrigBases.Substring(indexInR1, stitchedSoftClipPrefixDirection1);
                indexInR1 += stitchedSoftClipPrefixDirection1;
            }
            if (stitchedSoftClipPrefixDirection2 > 0)
            {
                totalSoftClipPrefixBases += Math.Min((int)softClipPrefix2, stitchedSoftClipPrefixDirection2);
                stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar2DirectionType, stitchedSoftClipPrefixDirection2));
                bases += r2OrigBases.Substring(indexInR2, stitchedSoftClipPrefixDirection2);
                indexInR2 += stitchedSoftClipPrefixDirection2;
            }

            int totalMatchBases = Math.Max((int)match1, (int)match2 + posGap) - Math.Min(0, posGap);

            int numExtraStitchBasesFromRead1 = Math.Max(Math.Min(posGap, posGap), 0);
            //int numExtraStitchBasesFromRead1 = Math.Max(Math.Min(posGap, (int)softClipPrefix2), 0);

            int stitchedBases = numExtraStitchBasesFromRead1 + Math.Min((int)match1, (int)match2 + posGap) - Math.Max(0, posGap);

            int numTrailingMatchedBases2 = (int)match2 - ((int)match1 - posGap);
            int numExtraStitchBasesFromRead2 = 0;
            if (numTrailingMatchedBases2 > 0)
            {
                //numExtraStitchBasesFromRead2 = Math.Min((int)softClipSuffix1, numTrailingMatchedBases2);

                // Soft clipped bases in read1 count as stitched if they overlap match bases in read 2.
                stitchedBases += numExtraStitchBasesFromRead2;
            }

            stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(DirectionType.Stitched, stitchedBases));
            bases += r1OrigBases.Substring(indexInR1, numExtraStitchBasesFromRead1);
            indexInR1 += numExtraStitchBasesFromRead1;
            bases += r1OrigBases.Substring(indexInR1, stitchedBases);
            indexInR1 += stitchedBases;
            indexInR2 += stitchedBases;
            bases += r2OrigBases.Substring(indexInR2, numExtraStitchBasesFromRead2);
            indexInR2 += numExtraStitchBasesFromRead2;

            if (totalSoftClipPrefixBases > 0)
            {
                stitchingInfo.StitchedCigar.Add(new CigarOp('S', (uint)totalSoftClipPrefixBases));
                //bases += firstBases.Substring(0, totalSoftClipPrefixBases);
            }

            stitchingInfo.StitchedCigar.Add(new CigarOp('M', (uint)totalMatchBases));
            //bases += firstBases.Substring(totalSoftClipPrefixBases, totalMatchBases);


            // Prefer soft clip suffix bases from read 1
            int stitchedSoftClipSuffixDirection1 = (int)softClipSuffix1 + Math.Min(0, ((int)match1 - ((int)match2 + posGap)));

            int numTrailingMatchedButNotStitchedBases1 = (int)match1 - ((int)match2 + posGap);

            int totalSoftClipSuffixBases = Math.Max(0, stitchedSoftClipSuffixDirection1);
            int stitchedSuffixDirection1 = stitchedSoftClipSuffixDirection1 + Math.Max(0, numTrailingMatchedButNotStitchedBases1);

            if (stitchedSuffixDirection1 > 0)
            {
                stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar1DirectionType, stitchedSuffixDirection1));
                bases += r1OrigBases.Substring(indexInR1, stitchedSuffixDirection1);
                indexInR1 += stitchedSuffixDirection1;
            }

            if (_ignoreProbeSoftclips)
            {
                // Only use read2 suffix soft clip bases if there are more of them than in read 1
                stitchingInfo.IgnoredProbeSuffixBases = Math.Max(0, numTrailingMatchedButNotStitchedBases1 + (int)softClipSuffix1);
            }

            int stitchedSoftClipSuffixDirection2 = (int)softClipSuffix2 - stitchingInfo.IgnoredProbeSuffixBases;

            if (stitchedSoftClipSuffixDirection2 > 0)
            {
                totalSoftClipSuffixBases += stitchedSoftClipSuffixDirection2;
            }

            int numTrailingMatchedButNotStitchedBases2 = Math.Max(0, (int)match2 - ((int)match1 - posGap));
            int stitchedSuffixDirection2 = stitchedSoftClipSuffixDirection2 + numTrailingMatchedButNotStitchedBases2 - numExtraStitchBasesFromRead2;
            if (stitchedSuffixDirection2 > 0)
            {
                stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp(cigar2DirectionType, stitchedSuffixDirection2));
                bases += r2OrigBases.Substring(indexInR2, stitchedSuffixDirection2);
                indexInR2 += stitchedSuffixDirection2;
            }

            if (totalSoftClipSuffixBases > 0)
            {
                stitchingInfo.StitchedCigar.Add(new CigarOp('S', (uint)totalSoftClipSuffixBases));
            }

            Console.WriteLine(bases);
            stitchingInfo.StitchedBases = bases.ToCharArray().Select(x=> (char?)x).ToList();
            //List<char?> basesArray = (firstBases + secondBases.Substring(stitchedBases)).ToCharArray().Select(x=>(char?)x).ToList();
            //stitchingInfo.StitchedBases = basesArray;

            return stitchingInfo;
        }

        bool GetSimpleCigarComponents(CigarAlignment cigar, ref uint softClipPrefix, ref uint match, ref uint softClipSuffix)
        {
            if (cigar.Count == 3 && cigar[0].Type == 'S' && cigar[1].Type == 'M' && cigar[2].Type == 'S')
            {
                softClipPrefix = cigar[0].Length;
                match = cigar[1].Length;
                softClipSuffix = cigar[2].Length;
                return true;
            }

            if (cigar.Count == 2)
            {
                if (cigar[0].Type == 'S' && cigar[1].Type == 'M')
                {
                    softClipPrefix = cigar[0].Length;
                    match = cigar[1].Length;
                    return true;
                }
                else if (cigar[0].Type == 'M' && cigar[1].Type == 'S')
                {
                    match = cigar[0].Length;
                    softClipSuffix = cigar[1].Length;
                    return true;
                }
            }

            if (cigar.Count == 1)
            {
                if (cigar[0].Type == 'M')
                {
                    match = cigar[0].Length;
                    return true;
                }
            }

            // The cigar is not simple.
            return false;
        }

        private static string GetCigarPattern(CigarAlignment cigar)
        {
            var cigarPattern = "";

            foreach (CigarOp item in cigar)
            {
                cigarPattern += item.Type;
            }
            return cigarPattern;
        }
        public StitchingInfo GetStitchedCigar(CigarAlignment cigar1, int pos1, CigarAlignment cigar2, int pos2, bool reverseFirst, bool pairIsOutie, string sequence1, string sequence2, byte[] quals1, byte[] quals2, bool r1IsFirstMate)
        {
            try
            {
                StitchingInfo stitchingInfo = null;

                _totalProcessed++;

                //if (_totalProcessed % 10000 == 0)
                //{
                //    foreach (var readStatus in _statusCounter.GetReadStatuses().OrderBy(x => x.Key))
                //    {
                //        Logger.WriteToLog($" INTERMEDIATE ({_totalProcessed})| STATUSCOUNT " + readStatus.Key + " | " + readStatus.Value);
                //    }
                //}
                if (true)
                {
                    stitchingInfo = GetSuperDuperSimpleStitchedCigar(cigar1, pos1, cigar2, pos2, reverseFirst, sequence1,
                        sequence2, quals1, quals2, r1IsFirstMate);

                    if (stitchingInfo != null)
                    {
                        //Console.WriteLine("hooray! new method!!");
                        _statusCounter.AddStatusCount("Super simple stitching method");
                        return stitchingInfo;
                    }
                }

                // TODO maybe come back to this. But I think SuperDuperSimple is enough.
                //if (_useSoftclippedBases && false)
                //{
                //    if (cigar1.Count == 1 && cigar2.Count == 1)
                //    {
                //        stitchingInfo = GetSuperSimpleStitchedCigar(cigar1, pos1, cigar2, pos2, reverseFirst, sequence1,
                //        sequence2, quals1, quals2);

                //    }

                //    if (stitchingInfo != null)
                //    {
                //        //stitchingInfo.IsSimple = true;
                //        return stitchingInfo;
                //    }
                //}


                // This list is cleared rather than reallocated to avoid excess garbage collection.
                //_stitchPositionsList.Clear();
                for (int i = 0; i < _stitchPositionsList.Length; i++)
                {
                    var pos = _stitchPositionsList[i];
                    if (pos != null)
                    {
                        pos.Reset();
                    }
                    //_stitchPositionsList[i] = null;
                }
                
                //_statusCounter.AddStatusCount(GetCigarPattern(cigar1) + "/" + GetCigarPattern(cigar2));

                _stitchedPositionsListLength = 0;

                GetStitchedSites(cigar1, cigar2, pos2, pos1, sequence1, sequence2, quals1, quals2);

                var success = true;

                var r1PrefixClip = cigar1.GetPrefixClip();
                var r2PrefixClip = cigar2.GetPrefixClip();
                var r1SuffixClip = cigar1.GetSuffixClip();
                var r2SuffixClip = cigar2.GetSuffixClip();


                stitchingInfo = ReconcileSites(reverseFirst, out success,
                    pairIsOutie ? (int)r2PrefixClip : (int)r1PrefixClip,
                    pairIsOutie
                        ? (int) (cigar1.GetReadSpan() - (int)r1SuffixClip)
                        : (int) (cigar2.GetReadSpan() - (int)r2SuffixClip), pairIsOutie, r1IsFirstMate, r1PrefixClip > 0, r1SuffixClip > 0, 
                    r2PrefixClip > 0, r2SuffixClip > 0);


                return success ? stitchingInfo : null;
            }
            catch (StitchedCigarLengthException e)
            {
                if (_ignoreReadsAboveMaxStitchedLength)
                {
                    _statusCounter.AddDebugStatusCount("Stitched cigar length error:\t" + e);
                    return null;
                }
                throw;
            }
        }

        private StitchingInfo ReconcileSites(bool r1IsReverse, out bool success, int prefixProbeClipEnd, int suffixProbeClipStart, bool pairIsOutie, 
            bool r1IsFirstMate, bool r1HasPrefixClip, bool r1HasSuffixClip, bool r2HasPrefixClip, bool r2HasSuffixClip)
        {
            var stitchingInfo = new StitchingInfo();
            success = true;

            // Assumption is that exactly one read is forward and one read is reverse, and each component read is only one direction
            var r1DirectionType = r1IsReverse ? DirectionType.Reverse : DirectionType.Forward;
            var r2DirectionType = r1IsReverse ? DirectionType.Forward : DirectionType.Reverse;

            if (r1HasPrefixClip || r1HasSuffixClip)
            {
                RedistributeSoftclips(true, r1HasPrefixClip, r1HasSuffixClip);
            }

            if (r2HasPrefixClip || r2HasSuffixClip)
            {
                RedistributeSoftclips(false, r2HasPrefixClip, r2HasSuffixClip);
            }

            var emptySites = 0;
            var indexInR1 = -1;
            var indexInR2 = -1;
            for (var i = 0; i < _stitchedPositionsListLength; i++)
            {
                StitchedPosition positionBefore = null;
                if (i > 0)
                {
                    positionBefore = _stitchPositionsList[i - 1];
                }
                var stitchPosition = _stitchPositionsList[i];

                if (stitchPosition.UnmappedPrefix.R1HasInsertion() && (stitchPosition.UnmappedPrefix.GetNumOpsForRead(ReadNumber.Read2) == 0))
                {
                    if (stitchPosition.MappedSite != null && stitchPosition.MappedSite.GetOpsForRead(ReadNumber.Read2).Any(x => x.CigarOp.IsReferenceSpan()) &&
                        positionBefore != null && positionBefore.MappedSite != null && positionBefore.MappedSite.GetOpsForRead(ReadNumber.Read2).Any(x => x.CigarOp.IsReferenceSpan()))
                    {
                        success = false;
                        return null;
                    }
                }
                if (stitchPosition.UnmappedPrefix.R2HasInsertion() && (stitchPosition.UnmappedPrefix.GetNumOpsForRead(ReadNumber.Read1) == 0))
                {
                    if (stitchPosition.MappedSite != null && stitchPosition.MappedSite.GetOpsForRead(ReadNumber.Read1).Any(x => x.CigarOp.IsReferenceSpan()) &&
                        positionBefore != null && positionBefore.MappedSite != null && positionBefore.MappedSite.GetOpsForRead(ReadNumber.Read1).Any(x => x.CigarOp.IsReferenceSpan()))
                    {
                        success = false;
                        return null;
                    }
                }

                if (emptySites >= 1 && stitchPosition.MappedSite != null && stitchPosition.MappedSite.HasValue())
                {
                    // We shouldn't have empty gaps in between mapped sites -- if we do, it's not really stitched!
                    success = false;
                    return null;
                }

                if (!stitchPosition.UnmappedPrefix.HasValue() && stitchPosition.MappedSite != null && !stitchPosition.MappedSite.HasValue())
                {
                    // If there's nothing here, there's no point reconciling. But we shouldn't bail out just yet, because there may be a redistributed softclip still to come (in future, change redistribution logic so that we never have gaps)
                    emptySites++;
                    continue;
                }

                success = ReconcileSite(stitchPosition.UnmappedPrefix, stitchingInfo, prefixProbeClipEnd, suffixProbeClipStart, pairIsOutie, r1DirectionType, r2DirectionType, ref indexInR1, ref indexInR2, r1IsFirstMate);
                if (!success)
                {
                    return null;
                }

                success = ReconcileSite(stitchPosition.MappedSite, stitchingInfo, prefixProbeClipEnd, suffixProbeClipStart, pairIsOutie, r1DirectionType, r2DirectionType, ref indexInR1, ref indexInR2, r1IsFirstMate);
                if (!success)
                {
                    return null;
                }
            }


            stitchingInfo.StitchedCigar.Compress();

            stitchingInfo.StitchedDirections.Compress();

            // Don't allow stitching that creates internal softclip
            if (stitchingInfo.StitchedCigar.HasInternalSoftclip())
            {
                success = false;
                return null;
            }

            return stitchingInfo;
        }

        private bool ReconcileSite(StitchedSite stitchSite, StitchingInfo stitchingInfo, int prefixProbeClipEnd, int suffixProbeClipStart, bool pairIsOutie, DirectionType r1DirectionType, DirectionType r2DirectionType, ref int indexInR1, ref int indexInR2, bool r1IsFirstMate)
        {
            bool success = true;
            var numDisagreements = 0;

            var unmappedSite = stitchSite as UnmappedStretch;
            var rightAlign = unmappedSite != null && unmappedSite.IsPrefix; //&& !unmappedSite.IsSuffix;

            // TODO only get these numops once each
            var read1NumOps = stitchSite.GetNumOpsForRead(ReadNumber.Read1);
            var read2NumOps = stitchSite.GetNumOpsForRead(ReadNumber.Read2);
            var offset = Math.Abs(read1NumOps - read2NumOps);
            var r1StretchLonger = read1NumOps >
                                  read2NumOps;

            for (var j = 0; j < Math.Max(read1NumOps, read2NumOps); j++)
            {
                int r1StretchIndex;
                int r2StretchIndex;
                if (rightAlign)
                {
                    r1StretchIndex = r1StretchLonger ? j : j - offset;
                    r2StretchIndex = r1StretchLonger ? j - offset : j;
                }
                else
                {
                    r1StretchIndex = j;
                    r2StretchIndex = j;
                }

                StitchableItem r1Item = new StitchableItem();
                StitchableItem r2Item = new StitchableItem();

                CigarOp? r1Op = null;
                if (r1StretchIndex >= 0 && read1NumOps > r1StretchIndex)
                {
                    r1Item = stitchSite.GetOpsForRead(ReadNumber.Read1)[r1StretchIndex];
                    r1Op = r1Item.CigarOp;
                }

                CigarOp? r2Op = null;
                
                if (r2StretchIndex >= 0 && read2NumOps > r2StretchIndex)
                {
                    r2Item = stitchSite.GetOpsForRead(ReadNumber.Read2)[r2StretchIndex];
                    r2Op = r2Item.CigarOp;
                }

                var combinedOp = GetCombinedOp(r1Op, r2Op);

                if (combinedOp == null)
                {
                    success = false;
                    if (_debug)
                    {
                        Logger.WriteToLog(string.Format("Could not stitch operations {0} and {1}.", r1Op?.Type,
                            r2Op?.Type));
                    }
                    _statusCounter.AddDebugStatusCount("Could not stitch operations");
                    return success;
                }

                stitchingInfo.StitchedCigar.Add(combinedOp.Value);

                var r1opUsed = r1Op != null;
                var r2opUsed = r2Op != null;
                var r1OpType = r1Op?.Type;
                var r2OpType = r2Op?.Type;
                var combinedOpType = combinedOp.Value.Type;

                if (r1opUsed && r1Op.Value.IsReadSpan())
                {
                    indexInR1++;
                }
                if (r2opUsed && r2Op.Value.IsReadSpan())
                {
                    indexInR2++;
                }

                if (combinedOpType != 'S')
                {
                    if (!_useSoftclippedBases && r2OpType == 'S')
                    {
                        r2opUsed = false;
                    }
                    if (!_useSoftclippedBases && r1OpType == 'S')
                    {
                        r1opUsed = false;
                    }
                }


                if (_ignoreProbeSoftclips)
                {
                    if (r1opUsed && r1OpType == 'S')
                    {
                        var isProbeSoftclip = (pairIsOutie && indexInR1 >= suffixProbeClipStart) ||
                                                (!pairIsOutie && indexInR1 < prefixProbeClipEnd);

                        // If this is a probe softclip, don't stitch it
                        if (isProbeSoftclip && r2opUsed)
                        {
                            r1opUsed = false;
                            if (pairIsOutie)
                            {
                                stitchingInfo.IgnoredProbeSuffixBases++;
                            }
                            else
                            {
                                stitchingInfo.IgnoredProbePrefixBases++;
                            }
                        }
                    }
                    if (r2opUsed && r2OpType == 'S')
                    {
                        var isProbeSoftclip = (pairIsOutie && indexInR2 < prefixProbeClipEnd) ||
                                                (!pairIsOutie && indexInR2 >= suffixProbeClipStart);
                        if (isProbeSoftclip && r1opUsed)
                        {
                            r2opUsed = false;
                            if (pairIsOutie)
                            {
                                stitchingInfo.IgnoredProbePrefixBases++;
                            }
                            else
                            {
                                stitchingInfo.IgnoredProbeSuffixBases++;
                            }
                        }
                    }
                    // TODO support scenarios where R1 and R2 are both in probe softclips, if necessary. Otherwise, if this is really never going to happen, throw an exception if we see it.
                    if (!r1opUsed && !r2opUsed)
                    {
                        throw new InvalidDataException("Stitching exception: Both R1 and R2 are in probe softclip regions at overlapping position.");
                    }
                }

                var stitched = r1opUsed && r2opUsed;
                stitchingInfo.StitchedDirections.Directions.Add(new DirectionOp()
                {
                    Direction =
                        stitched
                            ? DirectionType.Stitched
                            : (r1opUsed ? r1DirectionType : r2DirectionType),
                    Length = 1
                });

                if (combinedOpType == 'D')
                {
                    continue;
                }
                
                if (stitched)
                {
                    char? baseToAdd = null;
                    byte? qualToAdd = 0;

                    if (r1Item.Base == r2Item.Base) { 
                        baseToAdd = r1Item.Base;

                        var sumQuality = Convert.ToInt32((byte)r1Item.Quality) +
                                                       Convert.ToInt32((byte)r2Item.Quality);
                        var stitchedQuality = sumQuality > MaxBaseQuality ? MaxBaseQuality : sumQuality;
                        qualToAdd = (byte)stitchedQuality;
                    }
                    else
                    {
                        numDisagreements++;
                        if (_nifyDisagreements)
                        {
                            baseToAdd = 'N';
                            qualToAdd = 0;

                        }
                        else
                        {
                            var forwardItem = r1DirectionType == DirectionType.Forward ? r1Item : r2Item;
                            var reverseItem = r1DirectionType == DirectionType.Forward ? r2Item : r1Item;

                            if (forwardItem.Quality > reverseItem.Quality)
                            {
                                baseToAdd = forwardItem.Base;
                                
                                if (reverseItem.Quality < _minBasecallQuality)
                                {
                                    qualToAdd = forwardItem.Quality;
                                }
                                else
                                {
                                    // this was a high Q disagreement, and dangerous! we will filter this base.
                                    qualToAdd = 0;
                                }
                            }
                            else if (forwardItem.Quality == reverseItem.Quality)
                            {
                                var firstMateItem = r1IsFirstMate ? r1Item : r2Item;
                                var secondMateItem = r1IsFirstMate ? r2Item : r1Item;

                                baseToAdd = firstMateItem.Base;
                                if (secondMateItem.Quality < _minBasecallQuality)
                                {
                                    qualToAdd = firstMateItem.Quality;
                                }
                                else
                                {
                                    // this was a high Q disagreement, and dangerous! we will filter this base.
                                    qualToAdd = 0;
                                }
                            }
                            else
                            {
                                baseToAdd = reverseItem.Base;
                                if (forwardItem.Quality < _minBasecallQuality)
                                {
                                    qualToAdd = reverseItem.Quality;
                                }
                                else {
                                    // this was a high Q disagreement, and dangerous! we will filter this base.
                                    qualToAdd = 0;
                                }
                            }

                        }
                    }
                    stitchingInfo.StitchedBases.Add(baseToAdd);
                    stitchingInfo.StitchedQualities.Add((byte)qualToAdd);
                }
                else
                {
                    stitchingInfo.StitchedBases.Add(r1opUsed ? r1Item.Base : r2Item.Base);
                    stitchingInfo.StitchedQualities.Add(r1opUsed ? r1Item.Quality.Value : r2Item.Quality.Value);
                }
            }

            stitchingInfo.NumDisagreeingBases = numDisagreements;

            return success;
        }

        private void RedistributeSoftclips(bool operateOnR1, bool readHasPrefixClip, bool readHasSuffixClip)
        {
            var thisReadNum = operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2;
            var otherReadNum = operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1;
            StitchedPosition suffixToAdd = null;

            for (var indexInPositions = 0; indexInPositions < _stitchedPositionsListLength; indexInPositions++)
            {
                // Try to spread bookending softclips across the further-extending positions on the opposite read

                var stitchPosition = _stitchPositionsList[indexInPositions];
                StitchedPosition nextStitchPos = null;
                StitchedPosition previousStitchPos = null;

                if (indexInPositions <= _stitchedPositionsListLength - 2)
                {
                    nextStitchPos = _stitchPositionsList[indexInPositions + 1];
                }
                if (indexInPositions > 0)
                {
                    previousStitchPos = _stitchPositionsList[indexInPositions - 1];
                }

                var isSuffixPosition = indexInPositions == _stitchedPositionsListLength - 1 || (nextStitchPos!= null && ((nextStitchPos.UnmappedPrefix.GetNumOpsForRead(thisReadNum) == 0) &&
                                       (nextStitchPos.MappedSite.GetNumOpsForRead(thisReadNum) == 0)));
                var isPrefixPosition = indexInPositions == 0 || (previousStitchPos!= null && (previousStitchPos.UnmappedPrefix.GetNumOpsForRead(thisReadNum) == 0) &&
                                       (previousStitchPos.MappedSite.GetNumOpsForRead(thisReadNum) == 0));

                if (isPrefixPosition)
                {
                    isSuffixPosition = false;
                }
                if (stitchPosition.UnmappedPrefix.HasValue())
                {
                    stitchPosition.UnmappedPrefix.IsPrefix = isPrefixPosition;
                    stitchPosition.UnmappedPrefix.IsSuffix = isSuffixPosition;
                }

                if (!isPrefixPosition && !readHasSuffixClip)
                {
                    // short out of here
                    break;
                }

                if (!isSuffixPosition && !readHasPrefixClip)
                {
                    continue;
                }

                // If this is a suffix clip, extend to the right.
                if (isSuffixPosition)
                {
                    // TODO only dole out those that go out past R2's unmappeds -- this should be fine now for suffix. need to fix for prefix.
                    if (stitchPosition.UnmappedPrefix.GetNumOpsForRead(thisReadNum)
                        > stitchPosition.UnmappedPrefix.GetNumOpsForRead(otherReadNum))
                    {
                        var stitchPosCount = 0;
                        var numOpsToGiveAway = 0;
                        var opsToGiveAway = new List<StitchableItem>();
                        //var basesToGiveAway = new List<char?>();
                        
                        var originalOps = new List<StitchableItem>(stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum));

                        for (var index = originalOps.Count - 1; index >= 0 ; index--)
                        {
                            var item = originalOps[index];

                            if (item.CigarOp.Type == 'S')
                            {
                                opsToGiveAway.Add(item);
                                numOpsToGiveAway++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        opsToGiveAway.Reverse();

                        stitchPosition.UnmappedPrefix.SetOpsForRead(thisReadNum, new List<StitchableItem> { });
                        for (var i = 0; i < originalOps.Count - numOpsToGiveAway; i++)
                        {
                            stitchPosition.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<StitchableItem> { originalOps[i] });
                        }
                        while (opsToGiveAway.Count != 0)
                        {
                            var indexToRatchetTo = indexInPositions + stitchPosCount;

                            if (indexToRatchetTo > _stitchedPositionsListLength - 1)
                            {
                                suffixToAdd = new StitchedPosition();
                                suffixToAdd.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = _stitchPositionsList[indexToRatchetTo];

                            // First support the unmappeds at this site
                            while (true)
                            {
                                if (currentRatchetedStitchPos.UnmappedPrefix.GetNumOpsForRead(otherReadNum) ==
                                    currentRatchetedStitchPos.UnmappedPrefix.GetNumOpsForRead(thisReadNum)
                                    || opsToGiveAway.Count == 0)
                                {
                                    break;
                                }
                                currentRatchetedStitchPos.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<StitchableItem> { opsToGiveAway.First() });
                                opsToGiveAway.RemoveAt(0);
                            }

                            // Support the mappeds at this site next
                            var otherSideOpsAtSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(otherReadNum);
                            var siteHasOtherSideMapped = ((otherSideOpsAtSite?.Count ?? 0) != 0);

                            if (_allowTerminalClipsToSupportOverlappingDels && siteHasOtherSideMapped && otherSideOpsAtSite.All(s => s.CigarOp.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<StitchableItem> { new StitchableItem(
                                    new CigarOp(otherSideOpsAtSite.First().CigarOp.Type, otherSideOpsAtSite.First().CigarOp.Length) ,
                                    otherSideOpsAtSite.First().Base,
                                    otherSideOpsAtSite.First().Quality)
                                    });
                            }
                            else if (siteHasOtherSideMapped && (opsToGiveAway.Count != 0) && (currentRatchetedStitchPos.MappedSite.GetNumOpsForRead(thisReadNum) == 0))
                            {
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<StitchableItem> { opsToGiveAway.First() });
                                opsToGiveAway.RemoveAt(0);
                            }

                            // Then move on to the next position.

                            // For suffix clips, start with the current site.
                            stitchPosCount++;

                        }
                    }
                }
                if (isPrefixPosition)
                {
                    // TODO only dole out those that go out past R2's unmappeds
                    if (stitchPosition.UnmappedPrefix.GetNumOpsForRead(thisReadNum)
                        > stitchPosition.UnmappedPrefix.GetNumOpsForRead(otherReadNum))
                    {
                        // While I have any pieces to dole out, 
                        // if the previous position has something from R2 in mapped and nothing from R1, add a piece to R1 mapped
                        // if the previous position does not have something from R2 in mapped, put the whole remainder in R1 unmapped of whatever I'm on now
                        // if the previous position has something from R2 in mapped AND in unmapped and nothing from R1, add a piece to both

                        var stitchPosCount = 0;
                        var numOpsToGiveAway = 0;
                        var opsToGiveAway = new List<StitchableItem>();
                        var originalOps = new List<StitchableItem>(stitchPosition.UnmappedPrefix.GetOpsForRead(thisReadNum));
                        foreach (var item in originalOps)
                        {
                            if (item.CigarOp.Type == 'S')
                            {
                                opsToGiveAway.Add(item);
                                numOpsToGiveAway++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        stitchPosition.UnmappedPrefix.SetOpsForRead(thisReadNum, new List<StitchableItem> { });
                        for (var i = numOpsToGiveAway; i < originalOps.Count; i++)
                        {
                            stitchPosition.UnmappedPrefix.AddOpsForRead(thisReadNum, new List<StitchableItem> { originalOps[i] });
                        }
                        while (opsToGiveAway.Count != 0)
                        {
                            stitchPosCount++;
                            var indexToRatchetTo = indexInPositions - stitchPosCount;
                            var penultimateStitchPos = _stitchPositionsList[indexToRatchetTo + 1];

                            if (indexToRatchetTo < 0)
                            {
                                penultimateStitchPos.UnmappedPrefix.SetOpsForRead(thisReadNum, opsToGiveAway);
                                break;
                            }
                            var currentRatchetedStitchPos = _stitchPositionsList[indexToRatchetTo];

                            var otherSideOpsAtPreviousSite = currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read2 : ReadNumber.Read1);
                            var previousHasSomethingInR2Mapped = (otherSideOpsAtPreviousSite.Count != 0);

                            if (_allowTerminalClipsToSupportOverlappingDels && previousHasSomethingInR2Mapped && otherSideOpsAtPreviousSite.All(s => s.CigarOp.Type == 'D'))
                            {
                                // By virtue of us having a terminal S here, we can say we support this deletion, and kick the S over to support the ops at the other side of the deletion
                                // Assumption is that there's only one op at that site.
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(thisReadNum, new List<StitchableItem> { new StitchableItem(
                                   new CigarOp(otherSideOpsAtPreviousSite.First().CigarOp.Type, otherSideOpsAtPreviousSite.First().CigarOp.Length),
                                   otherSideOpsAtPreviousSite.First().Base,
                                   otherSideOpsAtPreviousSite.First().Quality)
                                 });
                                continue;
                            }
                            else if (previousHasSomethingInR2Mapped && (opsToGiveAway.Count != 0) && (currentRatchetedStitchPos.MappedSite.GetOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2).Count == 0))
                            {
                                currentRatchetedStitchPos.MappedSite.AddOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2, new List<StitchableItem> { opsToGiveAway.Last() });
                                opsToGiveAway.RemoveAt(opsToGiveAway.Count - 1);
                            }
                            else
                            {
                                penultimateStitchPos.UnmappedPrefix.SetOpsForRead(operateOnR1 ? ReadNumber.Read1 : ReadNumber.Read2, new List<StitchableItem>(opsToGiveAway));
                                break;
                            }

                        }
                    }
                    
                    //if (!readHasSuffixClip)
                    //{
                    //    break;
                    //}
                }
            }

            if (suffixToAdd != null)
            {
                AddToStitchPositionsList(suffixToAdd);
                //_stitchPositionsList.Add(suffixToAdd);
            }
        }

        private void AddToStitchPositionsList(StitchedPosition positionToAdd)
        {
            _stitchPositionsList[_stitchedPositionsListLength] = positionToAdd;
            _stitchedPositionsListLength++;
        }

        private CigarOp? GetCombinedOp(CigarOp? r1Op, CigarOp? r2Op)
        {
            if (r1Op == null && r2Op == null)
            {
                return null;
            }
            if (r1Op == null)
            {
                return r2Op;
            }
            if (r2Op == null)
            {
                return r1Op;
            }
            if (r1Op.Value.Type == r2Op.Value.Type)
            {
                return r1Op;
            }

            // TODO - more nuanced resolution
            if (r1Op.Value.Type == 'S')
            {
                return r2Op;
            }
            if (r2Op.Value.Type == 'S')
            {
                return r1Op;
            }
            else return null;
        }

        private StitchedPosition GetFreshStitchedPosition()
        {
            if (_positionsUsed >= _stitchPositions.Length)
            {
                throw new StitchedCigarLengthException("Combined length of reads is greater than the expected maximum stitched read length of " + _stitchPositions.Length);
            }

            var stitchPosition = _stitchPositions[_positionsUsed];
            stitchPosition.Reset();
            _positionsUsed++;
            return stitchPosition;
        }

        private void GetStitchedSites(CigarAlignment cigar1, CigarAlignment cigar2, long firstPos2, long firstPos1, string r1OrigBases, string r2OrigBases, byte[] r1OrigQuals, byte[] r2OrigQuals)
        {
            cigar1.Expand(_expandedCigar1);
            cigar2.Expand(_expandedCigar2);

            _r2Bases = new List<char?>();
            _r1Bases = new List<char?>();
            _r1Quals = new List<byte?>();
            _r2Quals = new List<byte?>();
            var indexInR1 = 0;
            foreach (CigarOp item in _expandedCigar1)
            {
                if (item.Type == 'D')
                {
                    _r1Bases.Add(null);
                    _r1Quals.Add(null);
                }
                else
                {
                    _r1Bases.Add(r1OrigBases[indexInR1]);
                    _r1Quals.Add(r1OrigQuals[indexInR1]);
                    indexInR1++;
                }
            }

            var indexInR2 = 0;
            foreach (CigarOp item in _expandedCigar2)
            {
                if (item.Type == 'D')
                {
                    _r2Bases.Add(null);
                    _r2Quals.Add(null);
                }
                else
                {
                    _r2Bases.Add(r2OrigBases[indexInR2]);
                    _r2Quals.Add(r2OrigQuals[indexInR2]);
                    indexInR2++;
                }
            }

            _positionsUsed = 0;

            if (firstPos1 < firstPos2)
            {
                AddR1ToList(0);
                AddR2ToList((int)(firstPos2 - firstPos1));
            }
            else
            {
                AddR2ToList(0);
                AddR1ToList((int)(firstPos1 - firstPos2));
            }
        }

        private void AddR1ToList(int refPos)
        {
            var index = 0;
            foreach (var op in _expandedCigar1)
            {
                while (refPos >= _stitchedPositionsListLength)
                {
                    AddToStitchPositionsList(GetFreshStitchedPosition());

                    //_stitchPositionsList.Add(GetFreshStitchedPosition());
                }
                if (op.IsReferenceSpan())
                {
                    var stitchableItem = new StitchableItem(op, _r1Bases[index], _r1Quals[index]);
                    _stitchPositionsList[refPos].MappedSite.AddOpsForRead(ReadNumber.Read1, stitchableItem);
                    index++;
                    refPos++;
                }
                else
                {
                    var stitchableItem = new StitchableItem(op, _r1Bases[index], _r1Quals[index]);
                    _stitchPositionsList[refPos].UnmappedPrefix.AddOpsForRead(ReadNumber.Read1, stitchableItem);
                    index++;
                }
            }
        }

        private void AddR2ToList(int refPos)
        {
            var index = 0;

            foreach (var op in _expandedCigar2)
            {
                while (refPos >= _stitchedPositionsListLength)
                {
                    AddToStitchPositionsList(GetFreshStitchedPosition());
                    //_stitchPositionsList.Add(GetFreshStitchedPosition());
                }
                if (op.IsReferenceSpan())
                {
                    var stitchableItem = new StitchableItem(op, _r2Bases[index], _r2Quals[index]);

                    _stitchPositionsList[refPos].MappedSite.AddOpsForRead(ReadNumber.Read2, stitchableItem);
                    index++;
                    refPos++;
                }
                else
                {
                    var stitchableItem = new StitchableItem(op, _r2Bases[index], _r2Quals[index]);

                    _stitchPositionsList[refPos].UnmappedPrefix.AddOpsForRead(ReadNumber.Read2, stitchableItem);
                    index++;
                }
            }
        }
    }
}