using System.IO;
using System.Collections.Generic;
using Pisces.Domain.Models.Alleles;
using TestUtilities;
using Xunit;

namespace Psara.Tests
{
    public class GeometricFilterTests
    {


        [Fact]
        public void TestbyStartPosition()
        {
            var parameters = new GeometricFilterParameters();
            parameters.ExclusionStrategy = GeometricFilterParameters.ExclusionModel.Prune;
            parameters.InclusionStrategy = GeometricFilterParameters.InclusionModel.ByStartPosition;
            parameters.RegionOfInterestPath = Path.Combine(TestPaths.LocalTestDataDirectory, "roi.txt");

            //ROI
            //chr11   6415642 6415648
            //chr11   6415768 6415772

            var filter = new GeometricFilter(parameters);

            var allelesWrongChr = CreateAllelesWith3baseDeletion(6415643, "chr4");
            var allelesBeforeROI = CreateAllelesWith3baseDeletion(6415641, "chr11");
            var allelesStartROI = CreateAllelesWith3baseDeletion(6415642, "chr11");
            var allelesEndROI = CreateAllelesWith3baseDeletion(6415648, "chr11");
            var allelesNotInROI = CreateAllelesWith3baseDeletion(6415650, "chr11");
            var allelesBackInInROI = CreateAllelesWith3baseDeletion(6415771, "chr11");
            var allelesAfterROI = CreateAllelesWith3baseDeletion(6415773, "chr11");


            Assert.True(CheckFiltered(filter, allelesWrongChr, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBeforeROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesStartROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesEndROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesNotInROI, GeometricFilterParameters.ExclusionModel.Prune,false));
            Assert.True(CheckFiltered(filter, allelesBackInInROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesAfterROI, GeometricFilterParameters.ExclusionModel.Prune, false));

        }


        [Fact]
        public void TestbyFilteringByExpandingTheInterval()
        {
            var parameters = new GeometricFilterParameters();
            parameters.ExclusionStrategy = GeometricFilterParameters.ExclusionModel.Prune;
            parameters.InclusionStrategy = GeometricFilterParameters.InclusionModel.Expanded;
            parameters.RegionOfInterestPath = Path.Combine(TestPaths.LocalTestDataDirectory, "roi.txt");

            //ROI
            //chr11   6415642 6415648
            //chr11   6415768 6415772

            var filter = new GeometricFilter(parameters);
            

            var allelesWrongChr = CreateSNPOnlyAlleles(6415643, "chr4");
            var allelesBeforeROI = CreateSNPOnlyAlleles(6415641, "chr11");
            var allelesStartROI = CreateSNPOnlyAlleles(6415642, "chr11");
            var allelesEndROI = CreateSNPOnlyAlleles(6415648, "chr11");
            var allelesNotInROI = CreateSNPOnlyAlleles(6415650, "chr11");
            var allelesBackInInROI2 = CreateSNPOnlyAlleles(6415771, "chr11");
            var allelesAfterROI2 = CreateSNPOnlyAlleles(6415773, "chr11");


            Assert.True(CheckFiltered(filter, allelesWrongChr, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBeforeROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesStartROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesEndROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesNotInROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBackInInROI2, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesAfterROI2, GeometricFilterParameters.ExclusionModel.Prune, false));


            //ROI
            //chr11   6415642 6415648
            //chr11   6415768 6415772

            allelesWrongChr = CreateAllelesWith3baseDeletion(6415643, "chr4");  //will still be on wrong chr
            var snpsBeforeROI1 = CreateSNPOnlyAlleles(6415641, "chr11"); // not in the interval yet - the deletion has not caused it to be expanded
            allelesBeforeROI = CreateAllelesWith3baseDeletion(6415639, "chr11"); //3 base deletion starts on 39, and has deleted bases 40,41,42
            var snpsBeforeROI2 = CreateSNPOnlyAlleles(6415641, "chr11"); //the deletion upstream should make it included in the interval when it was not before.
            allelesStartROI = CreateAllelesWith3baseDeletion(6415642, "chr11");
            allelesEndROI = CreateAllelesWith3baseDeletion(6415648, "chr11");
            allelesNotInROI = CreateAllelesWith3baseDeletion(6415650, "chr11");
            allelesBackInInROI2 = CreateAllelesWith3baseDeletion(6415771, "chr11");
            allelesAfterROI2 = CreateAllelesWith3baseDeletion(6415773, "chr11");

            Assert.True(CheckFiltered(filter, allelesWrongChr, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, snpsBeforeROI1, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBeforeROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, snpsBeforeROI2, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesStartROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesEndROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesNotInROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBackInInROI2, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesAfterROI2, GeometricFilterParameters.ExclusionModel.Prune, false));

            //make a new filter to reset the intervals to the original positions.
            filter = new GeometricFilter(parameters);
            var mnvBeforeROI = CreateAllelesWith2baseMNV(6415640, "chr11"); //2 base MNV starts on 40 and extends to 41, so still out of the interval
            var mnvCrossingIntoROI = CreateAllelesWith2baseMNV(6415641, "chr11"); //2 base MNV starts on 41 and extends to 42, so goes into interval + forces it to expand
            var mnvInsideROI = CreateAllelesWith2baseMNV(6415648, "chr11"); //2 base MNV starts on 648
            var mnvAfterROI = CreateAllelesWith2baseMNV(6415766, "chr11"); //2 base MNV starts on 766

            Assert.True(CheckFiltered(filter, allelesWrongChr, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, mnvBeforeROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, snpsBeforeROI1, GeometricFilterParameters.ExclusionModel.Prune, false)); //SNP on pos 641
            Assert.True(CheckFiltered(filter, mnvCrossingIntoROI, GeometricFilterParameters.ExclusionModel.Prune, true)); //now position 641 is IN the interval
            Assert.True(CheckFiltered(filter, snpsBeforeROI2, GeometricFilterParameters.ExclusionModel.Prune, true)); //SNP on pos 641
            Assert.True(CheckFiltered(filter, allelesStartROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, allelesEndROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, mnvInsideROI, GeometricFilterParameters.ExclusionModel.Prune, true));
            Assert.True(CheckFiltered(filter, mnvAfterROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesNotInROI, GeometricFilterParameters.ExclusionModel.Prune, false));
            Assert.True(CheckFiltered(filter, allelesBackInInROI2, GeometricFilterParameters.ExclusionModel.Prune, true));
        }


        public bool CheckFiltered(GeometricFilter filter, List<CalledAllele> inputAlleles, GeometricFilterParameters.ExclusionModel exclusionModel, bool shouldBeKept)
        {

            List<CalledAllele> filteredAlleles = filter.DoFiltering(inputAlleles);

            if (shouldBeKept)
            {
                if (filteredAlleles.Count == 0)
                    return false;

                foreach (var allele in filteredAlleles)
                {
                    if (allele.Filters.Contains(Pisces.Domain.Types.FilterType.OffTarget))
                        return false;
                }

                return true;
            }
            else
            {

                if (exclusionModel == GeometricFilterParameters.ExclusionModel.Prune)
                {
                    if (filteredAlleles.Count == 0)
                        return true; //everything excluded as desired
                    else
                        return false;
                }
                else
                {
                    if (filteredAlleles.Count == 0)
                        return false;

                    foreach (var allele in filteredAlleles)
                    {
                        if (!allele.Filters.Contains(Pisces.Domain.Types.FilterType.OffTarget))
                            return false;
                    }
                    return true;
                }
            }
        }

        public List<CalledAllele> CreateAllelesWith3baseDeletion(int position, string chr)
        {

            var snv = TestHelper.CreateDummyAllele(chr, position, "A", "C", 100, 10);
            var del = TestHelper.CreateDummyAllele(chr, position, "ACGT", "A", 100, 20);
            var ins = TestHelper.CreateDummyAllele(chr, position, "A", "AGGGG", 100, 30);
            var mnv = TestHelper.CreateDummyAllele(chr, position, "ACG", "CCC", 100, 40);

            var alleles = new List<CalledAllele>() { snv, del, ins, mnv };

            return alleles;
        }

        public List<CalledAllele> CreateAllelesWith2baseMNV(int position, string chr)
        {

            var snv = TestHelper.CreateDummyAllele(chr, position, "A", "C", 100, 10);
            var ins = TestHelper.CreateDummyAllele(chr, position, "A", "AGGGG", 100, 30);
            var mnv = TestHelper.CreateDummyAllele(chr, position, "AC", "CC", 100, 40);

            var alleles = new List<CalledAllele>() { snv,  ins, mnv };

            return alleles;
        }


        public List<CalledAllele> CreateSNPOnlyAlleles(int position, string chr)
        {

            var snv1 = TestHelper.CreateDummyAllele(chr, position, "A", "C", 100, 10);
            var snv2 = TestHelper.CreateDummyAllele(chr, position, "A", "G", 100, 10);

            var alleles = new List<CalledAllele>() { snv1, snv2 };

            return alleles;
        }


    }
}