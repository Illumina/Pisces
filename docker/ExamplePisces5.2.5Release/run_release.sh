#!/bin/bash

VERSION="5.2.5.20"
tar -xzf Pisces/binaries/$VERSION/Pisces_$VERSION.tar.gz

#dotnet Pisces.dll -bam {bamfile} -g {genome}
dotnet Pisces_$VERSION/Pisces.dll -v


