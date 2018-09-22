#!/bin/bash
cd Pisces

echo "Building Pisces........"
dotnet build 

binarypath=/app/Pisces/src/exe/Pisces/bin/Debug/netcoreapp2.0

echo "Running Pisces........"
#your command here, ie
#dotnet Pisces.dll -bam {bamfile} -g {genome}

dotnet $binarypath/Pisces.dll -help

genome=/data/Genomes/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta
bam=/data/somatic/titration/bams/8pct-NA12877-r1_S12.bam
interval=/data/BedFiles/Intervals_TSAVP_Titr.txt
outFolder=/data/out

dotnet $binarypath/Pisces.dll -bam $bam -g $genome -outFolder $outFolder -i $interval -gvcf false -rmxnfilter 5,9,0.35 -callMNVs false



