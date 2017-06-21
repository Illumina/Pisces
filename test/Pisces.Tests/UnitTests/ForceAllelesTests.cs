using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;
using Pisces.Domain.Models.Alleles;
using Pisces.Domain.Types;
using Pisces.Interfaces;
using Pisces.IO;
using Pisces.IO.Interfaces;
using Pisces.Logic;
using Pisces.Processing.Interfaces;
using Pisces.Processing.Models;
using TestUtilities;
using Xunit;

namespace Pisces.Tests.UnitTests
{
	public class ForceAllelesTests
	{

		[Fact]
		public void AddForcedAlleleInRefSite()
		{
			var calledAllele = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				Genotype = Genotype.HomozygousRef,
				ReferenceSupport = 50
			};
			var calledAlleles = new List<CalledAllele> {calledAllele};
			var forcedAllelesInPos = new List<Tuple<string,string>> {new Tuple<string, string>("G", "GCT") } ;

			ForcedAllelesUtils.AddForcedAllelesToCalledAlleles(10, calledAlleles, forcedAllelesInPos, "chr1");

			Assert.Equal(2,calledAlleles.Count);
			var addedAllele = calledAlleles.Last();
			Assert.Equal(Genotype.HomozygousRef,addedAllele.Genotype);
			Assert.True(addedAllele.Filters.Contains(FilterType.ForcedReport));



		}


	    [Fact]
	    public void ForcedAlleleWithReadsInRefSite()
	    {
	        var calledAllele = new CalledAllele(AlleleCategory.Snv)
	        {
	            ReferencePosition = 10,
	            Chromosome = "chr1",
	            ReferenceAllele = "G",
	            Genotype = Genotype.HomozygousRef,
	            ReferenceSupport = 50,
                AlternateAllele = "C",
                AlleleSupport = 1,
                IsForcedToReport = true
	        };
	        var calledAlleles = new List<CalledAllele> { calledAllele };
	        var forcedAllelesInPos = new List<Tuple<string, string>> { new Tuple<string, string>("G", "GCT") };

	        ForcedAllelesUtils.AddForcedAllelesToCalledAlleles(10, calledAlleles, forcedAllelesInPos, "chr1");

	        Assert.Equal(2, calledAlleles.Count);
	        var addedAllele = calledAlleles.Last();
	        Assert.Equal(Genotype.HomozygousRef, addedAllele.Genotype);
	        Assert.True(addedAllele.Filters.Contains(FilterType.ForcedReport));



	    }

        [Fact]
		public void AddForcedAlleleInNoCallSite()
		{
			var calledAllele = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T",
				Genotype = Genotype.AltAndNoCall,
				TotalCoverage = 50,
				AlleleSupport = 20
			};
			var calledAlleles = new List<CalledAllele> { calledAllele };
			var forcedAllelesInPos = new List<Tuple<string, string>> { new Tuple<string, string>("G", "GCT") };

			ForcedAllelesUtils.AddForcedAllelesToCalledAlleles(10, calledAlleles, forcedAllelesInPos, "chr1");

			Assert.Equal(2, calledAlleles.Count);
			var addedAllele = calledAlleles.Last();
			Assert.Equal(Genotype.AltLikeNoCall, addedAllele.Genotype);
			Assert.True(addedAllele.Filters.Contains(FilterType.ForcedReport));
		}

		[Fact]
		public void AddForcedAlleleInHetSite()
		{
			var calledAllele = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T",
				Genotype = Genotype.HeterozygousAltRef,
				TotalCoverage = 50,
				AlleleSupport = 20
			};
			var calledAlleles = new List<CalledAllele> { calledAllele };
			var forcedAllelesInPos = new List<Tuple<string, string>> { new Tuple<string, string>("G", "GCT") };

			ForcedAllelesUtils.AddForcedAllelesToCalledAlleles(10, calledAlleles, forcedAllelesInPos, "chr1");

			Assert.Equal(2, calledAlleles.Count);
			var addedAllele = calledAlleles.Last();
			Assert.Equal(Genotype.Others, addedAllele.Genotype);
			Assert.True(addedAllele.Filters.Contains(FilterType.ForcedReport));
			Assert.Equal(50, addedAllele.TotalCoverage);
			Assert.Equal(30, addedAllele.ReferenceSupport);
		}

		[Fact]
		public void AddForcedAlleleInDoubleAltSite()
		{
			var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T",
				Genotype = Genotype.HeterozygousAlt1Alt2,
				TotalCoverage = 50,
				AlleleSupport = 20
			};
			var calledAllele2 = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "C",
				Genotype = Genotype.HeterozygousAlt1Alt2,
				TotalCoverage = 50,
				AlleleSupport = 25
			};
			var calledAlleles = new List<CalledAllele> { calledAllele1,calledAllele2};
			var forcedAllelesInPos = new List<Tuple<string, string>> { new Tuple<string, string>("G", "GCT") };

			ForcedAllelesUtils.AddForcedAllelesToCalledAlleles(10, calledAlleles, forcedAllelesInPos, "chr1");

			Assert.Equal(3, calledAlleles.Count);
			var addedAllele = calledAlleles.Last();
			Assert.Equal(Genotype.Others, addedAllele.Genotype);
			Assert.True(addedAllele.Filters.Contains(FilterType.ForcedReport));
			Assert.Equal(50, addedAllele.TotalCoverage);
			Assert.Equal(5,addedAllele.ReferenceSupport);
		}





		private static Mock<SomaticVariantCaller> CreateMockedVariantCaller(SortedList<int, List<CalledAllele>> calledAlleleByPos, HashSet<Tuple<string, int, string, string>> forcedGtAlleles)
		{

			var mockAlignmentSource = new Mock<IAlignmentSource>();
			mockAlignmentSource.SetupSequence(s => s.GetNextRead()).Returns(ReadTestHelper.CreateRead("chr1", "AAA", 1)).Returns(null);
			mockAlignmentSource.Setup(s => s.LastClearedPosition).Returns(100);

			var candidateList = new List<CandidateAllele>() { new CandidateAllele("chr1", 10, "G", "T", AlleleCategory.Snv) };
			var candidateBatch = new CandidateBatch(candidateList);

			var mockVariantFinder = new Mock<ICandidateVariantFinder>();
			mockVariantFinder.Setup(v => v.FindCandidates(It.IsAny<Read>(), It.IsAny<string>(), It.IsAny<string>())).Returns(candidateList);

			var mockStateManager = new Mock<IStateManager>();
			mockStateManager.Setup(x => x.GetCandidatesToProcess(It.IsAny<int?>(), It.IsAny<ChrReference>(), It.IsAny<HashSet<Tuple<string, int, string, string>>>())).Returns(candidateBatch);
			mockStateManager.Setup(x => x.DoneProcessing(It.IsAny<ICandidateBatch>()));

			var mockvcfWriter = new Mock<IVcfWriter<CalledAllele>>();
			mockvcfWriter.Setup(x => x.Write(It.IsAny<IEnumerable<CalledAllele>>(), It.IsAny<IRegionMapper>()));
			var mockbiasWriter = new Mock<IStrandBiasFileWriter>();
			mockbiasWriter.Setup(x => x.Write(It.IsAny<List<CalledAllele>>()));

			var mockAlleleCaller = new Mock<IAlleleCaller>();


			mockAlleleCaller.Setup(x => x.Call(candidateBatch, mockStateManager.Object))
				.Returns(calledAlleleByPos);

			var myChrRef = new ChrReference()
			{
				Name = "chr1",
				Sequence = "ATGGCCTACGATTAGTAGGT"

			};

			var mockVariantCaller = new Mock<SomaticVariantCaller>(mockAlignmentSource.Object, mockVariantFinder.Object, mockAlleleCaller.Object, mockvcfWriter.Object, mockStateManager.Object, myChrRef, null, mockbiasWriter.Object, null, forcedGtAlleles);
			mockVariantCaller.Setup(
				x =>
					x.CheckAndAddForcedAllele(It.IsAny<int>(), It.IsAny<List<CalledAllele>>())).Verifiable();
			return mockVariantCaller;
		}


		//verify AddForcedAllelesToCalledAlleles is called by somaticVariantCaller

		[Fact]
		public void ForcedAlleleIsNull()
		{
			var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T"
			};
			var calledAllele2 = new CalledAllele(AlleleCategory.Deletion)
			{
				ReferencePosition = 12,
				Chromosome = "chr1",
				ReferenceAllele = "TC",
				AlternateAllele = "T"
			};

			var calledAlleleByPos = new SortedList<int, List<CalledAllele>>
			{
				{10, new List<CalledAllele> {calledAllele1}},
				{12,new List<CalledAllele> {calledAllele2} }
			};
			HashSet<Tuple<string, int, string, string>> forcedGtAlleles = null;

			Mock<SomaticVariantCaller> mockVariantCaller = CreateMockedVariantCaller(calledAlleleByPos, forcedGtAlleles);
			mockVariantCaller.Object.Execute();

			mockVariantCaller.Verify(x => x.CheckAndAddForcedAllele(It.IsAny<int>(), It.IsAny<List<CalledAllele>>()), Times.Never);



		}
		[Fact]
		public void ForcedAllelePosNotInCalledRegion()
		{

			var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T"
			};
			var calledAllele2 = new CalledAllele(AlleleCategory.Deletion)
			{
				ReferencePosition = 12,
				Chromosome = "chr1",
				ReferenceAllele = "TC",
				AlternateAllele = "T"
			};
			var calledAlleleByPos = new SortedList<int, List<CalledAllele>>()
			{
				{10, new List<CalledAllele>{calledAllele1}},
				{12,new List<CalledAllele> {calledAllele2}}
			};

			HashSet<Tuple<string, int, string, string>> forcedGtAlleles = new HashSet<Tuple<string, int, string, string>>
			{
				new Tuple<string, int, string, string>("chr1",20,"T","C")
			};

			Mock<SomaticVariantCaller> mockVariantCaller = CreateMockedVariantCaller(calledAlleleByPos, forcedGtAlleles);
			mockVariantCaller.Object.Execute();

			mockVariantCaller.Verify(x => x.CheckAndAddForcedAllele(It.IsAny<int>(), It.IsAny<List<CalledAllele>>()), Times.Never);

		}

		[Fact]
		public void ForcedAllelePosInCalledRegion()
		{

			var calledAllele1 = new CalledAllele(AlleleCategory.Snv)
			{
				ReferencePosition = 10,
				Chromosome = "chr1",
				ReferenceAllele = "G",
				AlternateAllele = "T"
			};
			var calledAllele2 = new CalledAllele(AlleleCategory.Deletion)
			{
				ReferencePosition = 12,
				Chromosome = "chr1",
				ReferenceAllele = "TC",
				AlternateAllele = "T"
			};
			var calledAlleleByPos = new SortedList<int, List<CalledAllele>>()
			{
				{10, new List<CalledAllele>{calledAllele1}},
				{12,new List<CalledAllele> {calledAllele2}}
			};

			HashSet<Tuple<string, int, string, string>> forcedGtAlleles = new HashSet<Tuple<string, int, string, string>>
			{
				new Tuple<string, int, string, string>("chr1",12,"T","C")
			};

			Mock<SomaticVariantCaller> mockVariantCaller = CreateMockedVariantCaller(calledAlleleByPos, forcedGtAlleles);

			mockVariantCaller.Object.Execute();

			mockVariantCaller.Verify(x => x.CheckAndAddForcedAllele(It.IsAny<int>(), It.IsAny<List<CalledAllele>>()), Times.AtLeastOnce);

		}

	}
}