FROM microsoft/dotnet:2.1-sdk AS build

WORKDIR /app

#get source
RUN git clone https://github.com/Illumina/Pisces.git

ADD . /app

CMD ["./run_build.sh"]
