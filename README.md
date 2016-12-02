# Pisces

This application calls low frequency variants, on Linux or Windows. It will run on tumor-only samples, and search for SNVs, MNVs, and small indels. It takes in .bams and generates .vcf or .gvcf files. It is included with the Illumina MiSeqReporter pipeline and various BaseSpace workflows. The caller can also be run as a standalone program.  

POC: 
[Tamsen Dunn](https://www.linkedin.com/in/tamsen-dunn-7340145) and
[Gwenn Berry](https://www.linkedin.com/in/gwenn-berry-43071939)

# License
Pisces source code is provided under the GPLv3 license. Pisces includes third party packages provided under other open source licenses, please see COPYRIGHT.txt for additional details.

# Similar Software at Illumina

Strelka is available for matched tumor/normal variant calling, and is part of the TUNE workflow secondary analysis workflow.
http://www.ncbi.nlm.nih.gov/pubmed/22581179

# System requirements

64 bit OS. 

If running on windows, you need .net framework 4.5.1.  This is freely available from http://www.microsoft.com/download/en/details.aspx?id=21

If running on Linux, you need mono installed and the “libFileCompression.so” file included with the Pisces solution.
Mono is freely available from http://www.mono-project.com/docs/about-mono/releases/ .  We typically use mono-3.10.0

# Running from a binary distribution

Latest distributions are available at: https://github.com/Illumina/Pisces/tree/release/Pisces_5_1_6/redist

So long as mono (for Linux) or .NET (for Windows) has been installed, the uncompressed binary is ready to go. Example command lines are:

Windows:

Pisces.exe -B C:\my\path\to\TestData\example_S1.bam -g C:\my\path\to\WholeGenomeFasta -MinVF 0.01

Linux:

mono Pisces.exe -B /my/path/to/TestData/example_S1.bam -g /my/path/to/WholeGenomeFasta -MinVF 0.01

example qsub cmd to a grid cluster:

echo "Pisces.exe -B /path/to/mybam.bam -g /Genomes/Homo_sapiens/UCSC/hg19/Sequence/WholeGenomeFasta -MinVF 0.01" | qsub -N PISCESJob -pe threaded 16-20 -M you@yoursmtp.com -m eas

It is necessary to supply a reference genome following the -g argument. Reference genomes may be downloaded from illumina's website at: http://support.illumina.com/sequencing/sequencing_software/igenome.ilmn

Note, the main executable Pisces.exe was originally named CallSomaticVariants.exe. So the above commands need to be altered for the 5.0.x releases.

# Build instructions

To configure and install, build the solution and copy the build to the desired location. The build is a windows process, and the solution in this repository is for VisualStudio 2015. Load the solution into VS, and build in release, x64 mode. This will create a bin\x64\Release folder along side the solution file. The Release folder has everything needed to run on windows and linux (under mono). To run unit tests, be sure to configure your Default Processor Architecture in VS to x64 (else you will hit BadImageFormatExceptions). 

The component algorithms are intended for developers to re-use and improve them. This version is not commercially supported and provided as is under the GNU GENERAL PUBLIC LICENSE. For first time use, we recommend testing with the example in the "testdata" folder.

# User Guide
https://github.com/Illumina/Pisces/wiki

# FAQ
https://github.com/Illumina/Pisces/wiki/Frequently-Asked-Questions

# Support
pisces@illumina.com
