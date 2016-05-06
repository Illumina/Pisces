using System;
using System.Linq;
using SequencingFiles;
using TestUtilities.MockBehaviors;
using Pisces.Domain.Models;
using Pisces.Domain.Tests;

namespace TestUtilities
{
    public class AmpliconTestFactory
    {
        public int ChrOffset { get; set; }
        private string _referenceSequence;
        public MockAlignmentExtractor _MAE { get; set; }
        public ChrReference ChrInfo { get; set; }

        public AmpliconTestFactory(string referenceSequence)
        {
            _referenceSequence = referenceSequence;

            ChrInfo = new ChrReference()
            {
                Name = "chr7",
                Sequence = _referenceSequence
            };

            _MAE = new MockAlignmentExtractor(ChrInfo);
        }

        public void StageInsertion(int readLength, string insertion, int variantPosition, int variantDepth, int referenceDepth)
        {
            var variantReads = GetInsertionReads(insertion, variantPosition, readLength);
            var referenceReads = GetReferenceReads(readLength);

            _MAE.StageAlignment(variantReads.Item1, variantReads.Item2, variantDepth);
            _MAE.StageAlignment(referenceReads.Item1, referenceReads.Item2, referenceDepth);
        }

        public void StageDeletion(int readLength, int numberDeletedBases, int variantPosition, int variantDepth, int referenceDepth)
        {
            var variantReads = GetDeletionReads(numberDeletedBases, variantPosition, readLength);
            var referenceReads = GetReferenceReads(readLength);

            _MAE.StageAlignment(variantReads.Item1, variantReads.Item2, variantDepth, "del");
            _MAE.StageAlignment(referenceReads.Item1, referenceReads.Item2, referenceDepth, "ref");
        }

        public void StageMnv(int readLength, string changedSequence, int variantPosition, int variantDepth, int referenceDepth)
        {
            var variantReads = GetMnvReads(changedSequence, variantPosition, readLength);
            var referenceReads = GetReferenceReads(readLength);

            _MAE.StageAlignment(variantReads.Item1, variantReads.Item2, variantDepth);
            _MAE.StageAlignment(referenceReads.Item1, referenceReads.Item2, referenceDepth);
        }

        public Tuple<BamAlignment, BamAlignment> GetReferenceReads(int readLength)
        {
            var refReadSequence = _referenceSequence.Substring(ChrOffset);
            var stitchedCigar = string.Format("{0}M", _referenceSequence.Length - ChrOffset);

            return CreateNewReads(readLength, stitchedCigar, refReadSequence);
        }

        public Tuple<BamAlignment, BamAlignment> GetInsertionReads(string insertion, int position, int readLength)
        {
            var variantSequence = _referenceSequence.Substring(ChrOffset, position - ChrOffset) + insertion +
                                  _referenceSequence.Substring(position);

            var stitchedCigar = string.Format("{0}M{1}I{2}M", position - ChrOffset, insertion.Length,
                variantSequence.Length - insertion.Length - (position - ChrOffset));

            return CreateNewReads(readLength, stitchedCigar, variantSequence);
        }

        public Tuple<BamAlignment, BamAlignment> GetDeletionReads(int numberDeletedBases, int position, int readLength)
        {
            var variantSequence = _referenceSequence.Substring(ChrOffset, position - ChrOffset) +
                                  _referenceSequence.Substring(position + numberDeletedBases);

            var stitchedCigar = string.Format("{0}M{1}D{2}M", position - ChrOffset, numberDeletedBases,
                variantSequence.Length - (position - ChrOffset));

            return CreateNewReads(readLength, stitchedCigar, variantSequence);
        }

        public Tuple<BamAlignment, BamAlignment> GetMnvReads(string changedSequence, int position, int readLength)
        {
            var variantSequence = _referenceSequence.Substring(ChrOffset, position - ChrOffset - 1) + changedSequence +
                      _referenceSequence.Substring(position - 1 + changedSequence.Length);

            var stitchedCigar = string.Format("{0}M", variantSequence.Length);

            return CreateNewReads(readLength, stitchedCigar, variantSequence);
        }

        public Tuple<BamAlignment, BamAlignment> CreateNewReads(int readLength, string stitchedCigar, string sequence)
        {
            var reverseCigar = TestHelper.GetReadCigarFromStitched(stitchedCigar, readLength, true);
            var reversePos = ChrOffset + (int)new CigarAlignment(stitchedCigar).GetReferenceSpan() - (int)reverseCigar.GetReferenceSpan();

            var read1 = new BamAlignment()
            {
                RefID = 1,
                Position = ChrOffset,
                CigarData = TestHelper.GetReadCigarFromStitched(stitchedCigar, readLength, false),
                Bases = sequence.Substring(0, readLength),
                TagData = DomainTestHelper.GetXCTagData(stitchedCigar),
                Qualities = new byte[readLength],
                MapQuality = 50
            };

            var read2 = new BamAlignment()
            {
                RefID = 1,
                Position = reversePos,
                CigarData = reverseCigar,
                Bases = sequence.Substring(sequence.Length - readLength),
                TagData = DomainTestHelper.GetXCTagData(stitchedCigar),
                Qualities = new byte[readLength],
                MapQuality = 50
            };

            for (int i = 0; i < read1.Qualities.Length; i++)
            {
                read1.Qualities[i] = 30;
                read2.Qualities[i] = 30;
            }

            SetPairedEndAttributes(read1, read2);

            return new Tuple<BamAlignment, BamAlignment>(read1, read2);
        }

        private void SetPairedEndAttributes(BamAlignment read1, BamAlignment read2)
        {
            read1.SetIsFirstMate(true);
            read1.SetIsSecondMate(false);
            read2.SetIsFirstMate(false);
            read2.SetIsSecondMate(true);
            read1.SetIsProperPair(true);
            read2.SetIsProperPair(true);
            read2.SetIsReverseStrand(true);
            read2.MatePosition = read1.Position;
            read1.MatePosition = read2.Position;
        }
    }

    public class AmpliconInsertionTest : AmpliconTest
    {
        public string InsertionSequence { get; set; }
    }

    public class AmpliconDeletionTest : AmpliconTest
    {
        public int NumberDeletedBases { get; set; }
    }

    public class AmpliconMnvTest : AmpliconTest
    {
        public string ChangedSequence { get; set; }
    }

    public class AmpliconTest
    {
        private string _referenceSequenceRelative;
        private int _variantPositionRelative;
        private int _chrOffset = 0;

        public bool StitchPairedReads { get; set; }
        public bool UseXcStitcher { get; set; }

        public int ReadLength { get; set; }

        public int ChrOffset { get { return _chrOffset; } set { _chrOffset = value; } }
        public string ReferenceSequenceRelative { set { _referenceSequenceRelative = value; } }
        public string ReferenceSequenceAbsolute { get { return AddOffsetToReferenceSeq(); } }
        public int VariantPositionRelative { set { _variantPositionRelative = value; } }
        public int VariantPositionAbsolute { get { return _variantPositionRelative + _chrOffset; } }

        public int VariantDepth { get; set; }
        public int ReferenceDepth { get; set; }

        private string AddOffsetToReferenceSeq()
        {
            const string repeatUnit = "N";
            return String.Concat(Enumerable.Repeat(repeatUnit, _chrOffset / repeatUnit.Length)) + _referenceSequenceRelative;
        }
    }

    public struct AmpliconTestResult
    {
        public string Filters { get; set; }
        public int Position { get; set; }
        public string ReferenceAllele { get; set; }
        public string VariantAllele { get; set; }
        public float VariantFrequency { get; set; }
        public float VariantDepth { get; set; }
        public float ReferenceDepth { get; set; }
        public float TotalDepth { get; set; }
    }
}