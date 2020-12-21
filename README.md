# Pisces

This application calls low frequency variants, on linux or windows. It will run on tumor-only samples, and search for SNVs, MNVs, and small indels. It takes in .bams and generates .vcf or .gvcf files. It is included with the Illumina MiSeqReporter pipeline and various BaseSpace workflows. The caller can also be run as a standalone program.  

POC: 
[Tamsen Dunn](https://www.linkedin.com/in/tamsen-dunn-7340145) and
[Gwenn Berry](https://www.linkedin.com/in/gwenn-berry-43071939)


# License
Pisces source code is provided under the GPLv3 license. Pisces includes third party packages provided under other open source licenses, please see COPYRIGHT.txt for additional details.

# System requirements

64 bit OS. 

If running on Linux, you need the “libFileCompression.so” file included with the Pisces solution. If you use a precompiled Pisces binary, this should already be in place alongside the application.

For Pisces version 5.2.5 and above, we have rolled forwards to .net core 2.0, which seems much more solid than the 1.0 .net core release. ( https://www.microsoft.com/net/download/windows ) . We are using dotnet version 2.0.3.

For Pisces version 5.2.0, for Windows or Linux, you need .net core framework 1.0.  This is freely available from https://www.microsoft.com/net/download/core . (This particular release has been working well for our users: https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.0.5-download.md )

For Pisces versions 5.1.x and below, Mono is required on Linux. Please see the Legacy System Requirements from the associated branch ReadMe. For example, [ReadMe 5.1.3](https://git.illumina.com/Bioinformatics/Pisces5/blob/5_1_7/README.md)

# Running from a binary distribution

So long as .net core has been installed, the uncompressed binary is ready to go.
Example command lines are:

windows:

dotnet Pisces.dll -bam C:\my\path\to\TestData\example_S1.bam -g C:\my\path\to\WholeGenomeFasta

linux:

dotnet Pisces.dll -bam /my/path/to/TestData/example_S1.bam -g /my/path/to/WholeGenomeFasta 

example qsub cmd to a grid cluster:

echo "/here/is/my/dotnet /here/is/Pisces.dll -bam /my/path/to/TestData/example_S1.bam -g /my/path/to/WholeGenomeFasta "  | qsub -N PISCESJob -pe threaded 16-20 -M you@yoursmtp.com -m eas

It is necessary to supply a reference genome following the -g argument. Reference genomes may be downloaded from illumina's website at: http://support.illumina.com/sequencing/sequencing_software/igenome.ilmn . Pisces will run on non-human genomes. The genome build should match the genome to which the bam was aligned.


For Pisces versions 5.1.x and below, please see the Legacy Running Instructions from the associated branch ReadMe.
For example, [ReadMe 5.1.3](https://git.illumina.com/Bioinformatics/Pisces5/blob/5_1_7/README.md)

# Build instructions

To configure and install, build the solution and copy the build to the desired location. The build is a windows process, and the solution in this repository is for VisualStudio 2017. For the .net core 2.0 version of Pisces, we reccomend VS 15.3 or above (we are using 15.5.1). Not all versions of VS are compatible with 2.0. 

Load the solution into VS, and build in release, x64 mode. This will create a bin\x64\Release folder along side the solution file. The Release folder has everything needed to run on windows, locally. The .net core release process includes a "Publish" step.  Use publish to gather local nuget dependencies and dll's needed to support other runtime targets (such as debian). The output from the publish step should be deployable to both win and linux without further modification.

The component algorithms are intended for developers to re-use and improve them. This version is not commercially supported and provided as is under the GNU GENERAL PUBLIC LICENSE. For first time use, we recommend testing with the example in the "testdata" folder.

# Docker files

https://github.com/Illumina/Pisces/tree/master/docker

# User Guide
https://github.com/Illumina/Pisces/wiki

# FAQ
https://github.com/Illumina/Pisces/wiki/Frequently-Asked-Questions

# Support

Questions on open source Pisces (outside of Illumina products):

[Tamsen Dunn](https://www.linkedin.com/in/tamsen-dunn-7340145) and
[Gwenn Berry](https://www.linkedin.com/in/gwenn-berry-43071939)

If you are using Pisces, feel free to introduce yourself!


