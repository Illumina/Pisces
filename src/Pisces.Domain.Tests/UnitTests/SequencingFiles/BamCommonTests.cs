using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alignment.Domain.Sequencing;
using Alignment.Domain.Sequencing;
using Xunit;

namespace Pisces.Domain.Tests.UnitTests.SequencingFiles
{
    public class BamCommonTests
    {
        [Fact]
        public void BamAlignment_Constructor()
        {
            //Happy Path
            var alignment = new BamAlignment
            {
                AlignmentFlag = 1,
                Bases = "AGGGGGGGGTTTTTTCACACACA",
                Bin = 3,
                CigarData = new CigarAlignment("11M5I9M7D25M"),
                FragmentLength = 9,
                MapQuality = 50,
                MatePosition = 5,
                MateRefID = 5,
                RefID = 1,
                Name = "test",
                Position = 11,
                TagData = DomainTestHelper.GetXCTagData("11M5I9M7D25M"),
                Qualities = new byte[] { 10, 30, 50 },
            };

            var newAlignment = new BamAlignment(alignment);
            Assert.Equal(alignment.AlignmentFlag, newAlignment.AlignmentFlag);
            Assert.Equal(alignment.Bases, newAlignment.Bases);
            Assert.Equal(alignment.Bin, newAlignment.Bin);
            Assert.Equal(alignment.CigarData, newAlignment.CigarData);
            Assert.Equal(alignment.FragmentLength, newAlignment.FragmentLength);
            Assert.Equal(alignment.MapQuality, newAlignment.MapQuality);
            Assert.Equal(alignment.MateRefID, newAlignment.MateRefID);
            Assert.Equal(alignment.MatePosition, newAlignment.MatePosition);
            Assert.Equal(alignment.RefID, newAlignment.RefID);
            Assert.Equal(alignment.Name, newAlignment.Name);
            Assert.Equal(alignment.Position, newAlignment.Position);
            Assert.Equal(alignment.Qualities, newAlignment.Qualities);
            Assert.Equal(alignment.TagData, newAlignment.TagData);
            Assert.Equal(alignment.Qualities, newAlignment.Qualities);
        }

        [Fact]
        public void GetEndPosition_Tests()
        {
            var alignment1 = new BamAlignment()
            {
                Position = 500,
                CigarData = new CigarAlignment("5M7I19M3D")
            };
            Assert.Equal(527, alignment1.GetEndPosition());

            var alignment2 = new BamAlignment()
            {
                Position = 500,
                CigarData = new CigarAlignment("3I")
            };
            Assert.Equal(500, alignment2.GetEndPosition());
        }

        [Fact]
        public void GetTag_Tests()
        {
            // create a tag
            TagUtils tagUtils = new TagUtils();
            tagUtils.AddIntTag("NM", 5);
            tagUtils.AddStringTag("XU", "ABCD");
            tagUtils.AddCharTag("XP", '?');
            byte[] tagData = tagUtils.ToBytes();
            var alignment = new BamAlignment() { TagData = tagData };

            // string tag scenarios
            Assert.Equal("ABCD", alignment.GetStringTag("XU"));
            Assert.Equal("?", alignment.GetStringTag("XP"));
            Assert.Throws<ApplicationException>(() => alignment.GetStringTag("NM"));
            Assert.Equal(null, alignment.GetStringTag("AB"));

        }

        [Fact]
        public void BamAlignmentFlag_Accessor_Tests()
        {
            //Is Duplicate
            Assert.True(new BamAlignment() { AlignmentFlag = 1024 }.IsDuplicate());
            Assert.False(new BamAlignment() { AlignmentFlag = 2 }.IsDuplicate());

            //Is Primary Alignment
            Assert.False(new BamAlignment() { AlignmentFlag = 256 }.IsPrimaryAlignment());
            Assert.True(new BamAlignment() { AlignmentFlag = 1024 }.IsPrimaryAlignment());

            //Is Proper Pair
            Assert.True(new BamAlignment() { AlignmentFlag = 2 }.IsProperPair());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsProperPair());

            //Is Reverse Starnd
            Assert.True(new BamAlignment() { AlignmentFlag = 16 }.IsReverseStrand());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsReverseStrand());

            //Is Failed QC
            Assert.True(new BamAlignment() { AlignmentFlag = 512 }.IsFailedQC());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsFailedQC());

            //Is First Mate
            Assert.True(new BamAlignment() { AlignmentFlag = 64 }.IsFirstMate());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsFirstMate());

            //Is Mapped
            Assert.False(new BamAlignment() { AlignmentFlag = 4 }.IsMapped());
            Assert.True(new BamAlignment() { AlignmentFlag = 1024 }.IsMapped());

            //Is Mate Mapped
            Assert.False(new BamAlignment() { AlignmentFlag = 8 }.IsMateMapped());
            Assert.True(new BamAlignment() { AlignmentFlag = 1024 }.IsMateMapped());

            //Is Mate Reverse Starnd
            Assert.True(new BamAlignment() { AlignmentFlag = 32 }.IsMateReverseStrand());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsMateReverseStrand());

            //Is Paired
            Assert.True(new BamAlignment() { AlignmentFlag = 1 }.IsPaired());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsPaired());

            //Is Main Alignment 
            Assert.True(new BamAlignment() { AlignmentFlag = 1 }.IsMainAlignment());
            Assert.False(new BamAlignment() { AlignmentFlag = 2304 }.IsMainAlignment());

            //Is Supplementary Alignment
            Assert.True(new BamAlignment() { AlignmentFlag = 2048 }.IsSupplementaryAlignment());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsSupplementaryAlignment());

            //Is Second Mate
            Assert.True(new BamAlignment() { AlignmentFlag = 128 }.IsSecondMate());
            Assert.False(new BamAlignment() { AlignmentFlag = 1024 }.IsSecondMate());

        }

        [Fact]
        public void BamAlignmentFlag_Setter_Tests()
        {
            var alignment = new BamAlignment();

            //Set Failed QC
            alignment.SetIsFailedQC(true);
            Assert.Equal((uint)512, alignment.AlignmentFlag);
            alignment.SetIsFailedQC(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Mate Unmapped
            alignment.SetIsMateUnmapped(true);
            Assert.Equal((uint)8, alignment.AlignmentFlag);
            alignment.SetIsMateUnmapped(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Mate Reverse Strand
            alignment.SetIsMateReverseStrand(true);
            Assert.Equal((uint)32, alignment.AlignmentFlag);
            alignment.SetIsMateReverseStrand(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Is Paired
            alignment.SetIsPaired(true);
            Assert.Equal((uint)1, alignment.AlignmentFlag);
            alignment.SetIsPaired(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Duplicate
            alignment.SetIsDuplicate(true);
            Assert.Equal((uint)1024, alignment.AlignmentFlag);
            alignment.SetIsDuplicate(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set First Mate
            alignment.SetIsFirstMate(true);
            Assert.Equal((uint)64, alignment.AlignmentFlag);
            alignment.SetIsFirstMate(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Proper Pair
            alignment.SetIsProperPair(true);
            Assert.Equal((uint)2, alignment.AlignmentFlag);
            alignment.SetIsProperPair(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Reverse Strand
            alignment.SetIsReverseStrand(true);
            Assert.Equal((uint)16, alignment.AlignmentFlag);
            alignment.SetIsReverseStrand(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Secondary Alignment
            alignment.SetIsSecondaryAlignment(true);
            Assert.Equal((uint)256, alignment.AlignmentFlag);
            alignment.SetIsSecondaryAlignment(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Second Mate
            alignment.SetIsSecondMate(true);
            Assert.Equal((uint)128, alignment.AlignmentFlag);
            alignment.SetIsSecondMate(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

            //Set Unmapped
            alignment.SetIsUnmapped(true);
            Assert.Equal((uint)4, alignment.AlignmentFlag);
            alignment.SetIsUnmapped(false);
            Assert.Equal((uint)0, alignment.AlignmentFlag);

        }

        [Fact]
        public void AppendTagData_Test()
        {
            var alignment = new BamAlignment() { TagData = new byte[] { 10, 10, 10 } };
            alignment.AppendTagData(new byte[] { 10, 10, 10 });
            Assert.Equal(6, alignment.TagData.Count());
            foreach (var item in alignment.TagData)
            {
                Assert.Equal(item, (byte)10);
            }
        }

        [Fact]
        public void BinaryIO_Tests()
        {
            //Add null terminated string
            byte[] bytes1 = new byte[10];
            int offset1 = 3;
            BinaryIO.AddNullTerminatedString(ref bytes1, ref offset1, "ABCDE");
            Assert.Equal(9, offset1);

            //Add int bytes
            byte[] bytes2 = new byte[10];
            int offset2 = 3;
            BinaryIO.AddIntBytes(ref bytes2, ref offset2, -4096);
            Assert.Equal(bytes2[4], 240);
            Assert.Equal(bytes2[5], 255);
            Assert.Equal(bytes2[6], 255);
            Assert.Equal(7, offset2);

            //Add uint bytes
            byte[] bytes3 = new byte[10];
            int offset3 = 3;
            BinaryIO.AddUIntBytes(ref bytes3, ref offset3, 4096);
            Assert.Equal(bytes3[4], 16);
            Assert.Equal(7, offset3);

        }

        [Fact]
        public void CigarAlignment_Constructor()
        {
            //Parameterless Constructor
            var emptyCigarAlignment = new CigarAlignment();
            Assert.Equal(0, emptyCigarAlignment.Count);

            //Happy Path
            var cigarAlignment = new CigarAlignment("5M6I");
            Assert.Equal(2, cigarAlignment.Count);
            Assert.Equal(new CigarOp('M', 5), cigarAlignment[0]);
            Assert.Equal(new CigarOp('I', 6), cigarAlignment[1]);

            //Empty Cigar String
            var emptyCigarString = new CigarAlignment("");
            Assert.Equal(0, emptyCigarString.Count);

            //Empty Cigar String
            var specialCharCigarString = new CigarAlignment("*");
            Assert.Equal(0, specialCharCigarString.Count);

            //Malformatted Cigar String
            Assert.Throws<Exception>(() => new CigarAlignment("6Y"));
            Assert.Throws<Exception>(() => new CigarAlignment("10"));
        }

        [Fact]
        public void CigarAlignment_DeepCopy_Test()
        {
            var cigarAlignment = new CigarAlignment("5M6I12M11D");
            var copy = cigarAlignment.DeepCopy();

            Assert.Equal(cigarAlignment.Count, copy.Count);
            for (int index = 0; index < cigarAlignment.Count; index++)
            {
                Assert.Equal(cigarAlignment[index], copy[index]);
            }
            cigarAlignment = new CigarAlignment("5M6I12M12S");
            Assert.Equal(cigarAlignment[0], copy[0]);
            Assert.Equal(cigarAlignment[1], copy[1]);
            Assert.Equal(cigarAlignment[2], copy[2]);
            Assert.NotEqual(cigarAlignment[3], copy[3]);
        }

        [Fact]
        public void CigarAlignment_CountMatches_Test()
        {
            var cigarAlignment = new CigarAlignment("5M6I12M11D");
            Assert.Equal(17, cigarAlignment.CountMatches());
        }

        [Fact]
        public void TranslateReadToReferenceOffset_Tests()
        {
            //read offset  = 0
            Assert.Equal(0, new CigarAlignment("5M6I12M11D").TranslateReadToReferenceOffset(0));

            //read offset  > 0 
            Assert.Equal(3, new CigarAlignment("5M6I12M11D").TranslateReadToReferenceOffset(3));
            Assert.Equal(25, new CigarAlignment("1M1D2I12M11D").TranslateReadToReferenceOffset(15));
        }

        [Fact]
        public void TranslateReferenceToReadOffset_Tests()
        {
            //no mapping scenario
            Assert.Equal(-1, new CigarAlignment("5M6I12M11D").TranslateReferenceToReadOffset(-1));
            Assert.Equal(-1, new CigarAlignment().TranslateReferenceToReadOffset(1));
            Assert.Equal(-1, new CigarAlignment("5D").TranslateReferenceToReadOffset(1));

            //ref offset > 0
            Assert.Equal(1, new CigarAlignment("5M6I12M11D").TranslateReferenceToReadOffset(1));
            Assert.Equal(13, new CigarAlignment("5M6I12M11D").TranslateReferenceToReadOffset(7));
        }

        [Fact]
        public void Compress_Tests()
        {
            var cigarAlignment1 = new CigarAlignment("5M2M");
            Assert.Equal(true, cigarAlignment1.Compress());
            Assert.Equal("7M", cigarAlignment1.ToString());

            var cigarAlignment2 = new CigarAlignment("5M0M");
            Assert.Equal(true, cigarAlignment2.Compress());
            Assert.Equal("5M", cigarAlignment2.ToString());

            var cigarAlignment3 = new CigarAlignment("5I2D1I3D");
            Assert.Equal(true, cigarAlignment3.Compress());
            Assert.Equal("6I5D", cigarAlignment3.ToString());
        }

        [Fact]
        public void GetReadSpan_Tests()
        {
            Assert.Equal((uint)11, new CigarAlignment("7M3I2D1S").GetReadSpan());
            Assert.Equal((uint)0, new CigarAlignment("7H3P").GetReadSpan());
        }

        [Fact]
        public void GetReferenceSpan_Tests()
        {
            Assert.Equal((uint)9, new CigarAlignment("7M3I2D1S").GetReferenceSpan());
            Assert.Equal((uint)0, new CigarAlignment("7H3P").GetReadSpan());
        }

        [Fact]
        public void GetPrefixClip_Tests()
        {
            Assert.Equal((uint)0, new CigarAlignment("7M3I2D1S11M2S").GetPrefixClip());
            Assert.Equal((uint)9, new CigarAlignment("7S3H2S").GetPrefixClip());
            Assert.Equal((uint)3, new CigarAlignment("3S7M5S").GetPrefixClip());
        }

        [Fact]
        public void GetSuffixClip_Tests()
        {
            Assert.Equal((uint)2, new CigarAlignment("7M3I2D1S11M2S").GetSuffixClip());
            Assert.Equal((uint)9, new CigarAlignment("7S3H2S").GetSuffixClip());
            Assert.Equal((uint)5, new CigarAlignment("3S7M5S").GetSuffixClip());
        }

        [Fact]
        public void CigarString_Manipulation_Tests()
        {
            var cigarstring = new CigarAlignment("7M3I2D1S11M2S");

            Assert.Equal(6, cigarstring.Count);
            Assert.Equal("7M3I2D1S11M2S", cigarstring.ToString());

            cigarstring.Add(new CigarOp('M', 6));
            Assert.Equal("7M3I2D1S11M2S6M", cigarstring.ToString());
            Assert.Equal(7, cigarstring.Count);

            cigarstring.Reverse();
            Assert.Equal("6M2S11M1S2D3I7M", cigarstring.ToString());
            Assert.Equal(7, cigarstring.Count);

            cigarstring.Clear();
            Assert.Equal("", cigarstring.ToString());
            Assert.Equal(0, cigarstring.Count);
        }

        [Fact]
        public void CigarOp_Equality_Tests()
        {
            CigarOp cigarOp = new CigarOp();
            Assert.True(cigarOp.Equals(new CigarOp()));
            cigarOp.Length = 21;
            cigarOp.Type = 'M';
            Assert.True(cigarOp.Equals(new CigarOp('M', 21)));
            Assert.False(cigarOp.Equals(null));//null check
            Assert.False(cigarOp.Equals(new CigarOp('I',21))); //Type mismatch
            Assert.False(cigarOp.Equals(new CigarOp('M', 22))); //Length mismatch 
            Assert.False(cigarOp.Equals(new CigarOp('D', 22))); //Both mismatch 
        }
    }
}
