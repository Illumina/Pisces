#!/bin/bash

# to mount data:
# docker build -t pisces_happy . > ../docker_build.log
# docker run -it --mount type=bind,source=/c/Users/tamse/Documents/Data/,target=/data pisces_happy /bin/bash

VERSION="5.2.5.20"
cd /app/Pisces
tar -xzf /app/Pisces/binaries/$VERSION/Pisces_$VERSION.tar.gz


#***********************titration analysis*****************
#
DATA_FOLDER=/data
GENOMES=$DATA_FOLDER/genomes
BS_DATA=$DATA_FOLDER/AppResults
OUT=$DATA_FOLDER/out/Pisces_$VERSION

update_ras_header () {
	cat >$1 <<EOF
##fileformat=VCFv4.1
##fileDate=20180608
##reference=/data/Genomes/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta
##ALT=<ID=<M>,Description="There is an overlapping other allele that has been called in a separate VCF record">
##INFO=<ID=DP,Number=1,Type=Integer,Description="Total Depth">
##FILTER=<ID=q30,Description="Quality score less than 30">
##FILTER=<ID=SB,Description="Variant strand bias too high">
##FILTER=<ID=R5x9,Description="Repeats of part or all of the variant allele (max repeat length 5) in the reference greater than or equal to 9">
##FORMAT=<ID=GT,Number=1,Type=String,Description="Genotype">
##FORMAT=<ID=GQ,Number=1,Type=Integer,Description="Genotype Quality">
##FORMAT=<ID=AD,Number=.,Type=Integer,Description="Allele Depth">
##FORMAT=<ID=DP,Number=1,Type=Integer,Description="Total Depth Used For Variant Calling">
##FORMAT=<ID=VF,Number=.,Type=Float,Description="Variant Frequency">
##FORMAT=<ID=NL,Number=1,Type=Integer,Description="Applied BaseCall Noise Level">
##FORMAT=<ID=SB,Number=1,Type=Float,Description="StrandBias Score">
##contig=<ID=chr1,length=249250621>
##contig=<ID=chr2,length=243199373>
##contig=<ID=chr3,length=198022430>
##contig=<ID=chr4,length=191154276>
##contig=<ID=chr5,length=180915260>
##contig=<ID=chr6,length=171115067>
##contig=<ID=chr7,length=159138663>
##contig=<ID=chr8,length=146364022>
##contig=<ID=chr9,length=141213431>
##contig=<ID=chr10,length=135534747>
##contig=<ID=chr11,length=135006516>
##contig=<ID=chr12,length=133851895>
##contig=<ID=chr13,length=115169878>
##contig=<ID=chr14,length=107349540>
##contig=<ID=chr15,length=102531392>
##contig=<ID=chr16,length=90354753>
##contig=<ID=chr17,length=81195210>
##contig=<ID=chr18,length=78077248>
##contig=<ID=chr19,length=59128983>
##contig=<ID=chr20,length=63025520>
##contig=<ID=chr21,length=48129895>
##contig=<ID=chr22,length=51304566>
##contig=<ID=chrX,length=155270560>
##contig=<ID=chrY,length=59373566>
#CHROM	POS	ID	REF	ALT	QUAL	FILTER	INFO	FORMAT	truth.bam
EOF
	cat $2 >> $1 
}

run_somatic_analysis () {
	#run Pisces
	echo "Running Pisces for titration"
	bamFolder=$1
	outFolder=$2
	#bamFolder=$BS_DATA/SampleSet1_Titration/Files
	#outFolder=$OUT/titration
	genome=$GENOMES/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta
	intervals=$BS_DATA/BedFiles/Files/$3
	dotnet=dotnet 
	pisces=/app/Pisces/Pisces_$VERSION/Pisces.dll
	if [ ! -e $outFolder ]
	then 
		mkdir -p $outFolder
	fi
	
	$dotnet $pisces -BAMFolder $bamFolder -CallMNVs false -g $genome -gVCF false -i $intervals -OutFolder $outFolder -RMxNFilter 5,9,0.35 

	#firstbam=`ls $bamFolder/*.bam | head -1`
	#$dotnet $pisces -bam $firstbam -CallMNVs false -g $genome -gVCF false -i $intervals -OutFolder $outFolder -RMxNFilter 5,9,0.35 -threadbychr true
	
	
	#run som.py
	echo "Running som.py"
	truth=$BS_DATA/Truth_Titration/Files/NA1287_78Titr.vcf 
	goldstandardBed=/data/platinum/ConfidentRegions.bed.gz;
	bedFile=$BS_DATA/BedFiles/Files/$4
	sompy=/opt/hap.py/bin/som.py
	vcfFolder=$outFolder
	sompyout=$outFolder/SompyOut_v0.3.10
	#v=8pct-NA12877-r1_S12.vcf 
	export HG19=$GENOMES/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta/genome.fa
	 
	mkdir $sompyout
	cd $vcfFolder
	vcfs="*.vcf"
	for v in $vcfs
	do
	 mkdir $sompyout/$v
	 echo $sompyout/$v
	 if [[ $bamFolder = *"RAS"* ]]
	 then
           sample_name=`echo $v | sed -s 's/_.*vcf//g'`
           truth_base=$BS_DATA/Truth_RAS/Files/${sample_name}_truth.txt.vcf
           truth=${truth_base}.with_header.vcf
           update_ras_header $truth $truth_base
         fi
	 $sompy $truth $vcfFolder/$v -f $goldstandardBed -o $sompyout/$v/sompy --logfile $sompyout/log.txt -T $bedFile
	done   
	
	#collect accuracy results into a single file
	
	echo "Collecting accuracy results"
	cd $sompyout
	#vcfs="*.vcf"
	i=0
	for v in $vcfs
	do
	    i=$[$i+1]
	    echo $i
	    echo -e "$v,$(cat $sompyout/$v/sompy.stats.csv|grep SNVs )" > snvs_$i"_"$v.snvs
	    echo -e "$v,$(cat $sompyout/$v/sompy.stats.csv|grep indel )" > indels_$i"_"$v.indels
	done 
	HEADERS="sample,,type,total.truth,total.query,tp,fp,fn,unk,ambi,recall,recall_lower,recall_upper,recall2,precision,precision_lower,precision_upper,na,ambiguous,fp.region.size,fp.rate,sompyversion,sompycmd"
	
	echo "$HEADERS" > snvs.out.csv
	echo "$HEADERS" > indels.out.csv
	
	cat *.snvs >> snvs.out.csv
	cat *.indels >> indels.out.csv
	
	echo "completed somatic for $1"
}	

run_germline_analysis () {

	#run Pisces
	echo "Running TSAV Pisces on $bamFolder"
	bamFolder=$1
	outFolder=$2
	#bamFolder=$BS_DATA/SampleSet3_VariantPanel2/Files/
	#outFolder=$OUT/TSAVP/
	genome=$GENOMES/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta
	intervals=$BS_DATA/BedFiles/Files/$3
	dotnet=dotnet 
	pisces=/app/Pisces/Pisces_$VERSION/Pisces.dll
	if [ ! -e $outFolder ]; then  mkdir -p $outFolder; fi
	
	#firstbam=`ls $bamFolder/*.bam | head -1`
        #$dotnet $pisces -bam $firstbam -CallMNVs false -g $genome -gVCF false -i $intervals -OutFolder $outFolder -RMxNFilter 5,9,0.35 -ploidy diploid -threadbychr true
	
	$dotnet $pisces -BAMFolder $bamFolder -CallMNVs false -g $genome -gVCF false -i $intervals -OutFolder $outFolder -RMxNFilter 5,9,0.35 -ploidy diploid
	
	
	#run hap.py
	echo "Running hap.py"
	goldstandard77=/data/platinum/NA12877.vcf
	goldstandard78=/data/platinum/NA12878.vcf
	goldstandardBed=/data/platinum/ConfidentRegions.bed.gz;
	bedFile=$BS_DATA/BedFiles/Files/$3
	happy=/opt/hap.py/bin/hap.py
	#vcfFolder=/illumina/scratch/pisces/PublicationMaterials/Bioinformatics/Data/Germline/TSAVP/Vcfs/Pisces_525
	vcfFolder=$outFolder
	export HG19=$GENOMES/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta/genome.fa
	
	happyout="$vcfFolder/HappyOut_v0.3.10"
	if [ ! -e $happyout ]; then mkdir -p $happyout; fi
	
	cd $vcfFolder
	
	#vcfs78="*878*.vcf"
	vcfs78=`ls *878*.vcf`
	
	echo "Looking through $vcfs78 to $happyout"
	i=0
	for v in $vcfs78
	do
	    if [ ! -e $happyout/$v ]; then mkdir -p $happyout/$v; fi
	    i=$[$i+1]
	    echo "Run hap.py $i for $v"
	    $happy $goldstandard78 $vcfFolder/$v -f $goldstandardBed -o $happyout/$v/happy --threads 16 --logfile $happyout/log.txt -V -X -T $bedFile
	done 
	
	#vcfs77="*877*.vcf"
	vcfs77=`ls *877*.vcf`
	
	i=0
	for v in $vcfs77
	do
	    if [ ! -e $happyout/$v ]; then mkdir -p $happyout/$v; fi
	    i=$[$i+1]
	    echo "Run hap.py $i for $v"
	    $happy $goldstandard77 $vcfFolder/$v -f $goldstandardBed -o $happyout/$v/happy --threads 16 --logfile $happyout/log.txt -V -X -T $bedFile
	done 
	
	
	#consolidate the results
	
	echo "Consolidating hap.py results from $happyout"
	cd $happyout
	#vcfs="*.vcf.gz"
	#vcfs="*.vcf"
	vcfs=`ls -d *.vcf`
	
	i=0
	echo "..checking $vcfs"
	for v in $vcfs
	do
	    i=$[$i+1]
	    echo "Consolidationg result from $i: $happyout / $v"
	    echo -e "$v,$(cat $happyout/$v/happy.summary.csv|grep INDEL,PASS )" > indel$i.indel
	    echo -e "$v,$(cat $happyout/$v/happy.summary.csv|grep SNP,PASS )" > snp$i.snp
	done 
	
	# Output Summary CSVs
	HEADERS="Sample,Type,Filter,TRUTH.TOTAL,TRUTH.TP,TRUTH.FN,QUERY.TOTAL,QUERY.FP,QUERY.UNK,FP.gt,FP.al,METRIC.Recall,METRIC.Precision,METRIC.Frac_NA,METRIC.F1_Score,TRUTH.TOTAL.TiTv_ratio,QUERY.TOTAL.TiTv_ratio,TRUTH.TOTAL.het_hom_ratio,QUERY.TOTAL.het_hom_ratio"
	echo "$HEADERS" > indel.out.csv
	echo "$HEADERS" > snp.out.csv
	
	cat *.indel >>indel.out.csv
	cat *.snp >>snp.out.csv
	
	echo "completed germline for $1"
}

run_somatic_analysis "$BS_DATA/SampleSet1_Titration/Files" "$OUT/titration" "Intervals_TSAVP_Titr.txt" "Intervals_TSAVP_Titr.bed"  
run_somatic_analysis "$BS_DATA/SampleSet2_RASPanel/Files/" "$OUT/raspanel" "KRASandNRASinterval2.picard" "KRASandNRASinterval2.bed"
run_germline_analysis "$BS_DATA/SampleSet3_VariantPanel2/Files/" "$OUT/varientpanel" "TSAVP_v3_intervals_sorted.txt" "TSAVP_v3_intervals_sorted.bed"
run_germline_analysis "$BS_DATA/SampleSet4_Myeloid/Files/" "$OUT/myeloidpanel" "TruSightMyeloid_intervals.txt" "TruSightMyeloid_intervals.bed"


