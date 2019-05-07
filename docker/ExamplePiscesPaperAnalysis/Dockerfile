 #jFROM microsoft/dotnet:2.1-sdk AS pisces_build
#
#COPY --from=happy_build /opt /opt
#
#jWORKDIR /app
#
##get source
#

FROM ubuntu:16.04 AS happy_build

RUN apt-get update && apt-get upgrade -y
#tjd+
#RUN apt-get install build-essential zlib1g-dev libbz2-dev pkg-config cmake libncurses5-dev autoconf -y
RUN apt-get install build-essential zlib1g-dev liblzo2-dev libbz2-dev pkg-config cmake libncurses5-dev autoconf -y
#tjd-
RUN apt-get install git bzip2 wget -y
RUN apt-get install python2.7 python2.7-dev python \
                    python-setuptools \
                    python-pip \
                    python-psutil \
                    cython \
                    python-numpy \
                    python-pandas \
                    python-distribute \
                    python-pysam \
                    python-scipy \
                    -y
RUN apt-get install software-properties-common python-software-properties -y
RUN apt-get clean -y

#tjd+
#RUN pip install bx-python
RUN pip install bx-python==0.7.3 
#tjd-

RUN echo oracle-java8-installer shared/accepted-oracle-license-v1-1 select true | debconf-set-selections && \
    add-apt-repository -y ppa:webupd8team/java && \
    apt-get update && \
    apt-get install -y oracle-java8-installer && \
    rm -rf /var/lib/apt/lists/* && \
    rm -rf /var/cache/oracle-jdk8-installer

# copy git repository into the image
RUN mkdir -p /opt/hap.py-source
COPY . /opt/hap.py-source/

# make minimal HG19 reference sequence
RUN mkdir -p /opt/hap.py-data

# get + install ant
WORKDIR /opt
RUN wget http://archive.apache.org/dist/ant/binaries/apache-ant-1.9.7-bin.tar.gz && \
    tar xzf apache-ant-1.9.7-bin.tar.gz && \
    rm apache-ant-1.9.7-bin.tar.gz
ENV PATH $PATH:/opt/apache-ant-1.9.7/bin

# run hap.py installer in the image, don't run tests since we don't have a reference file
WORKDIR /opt/hap.py-source
RUN python install.py /opt/hap.py --with-rtgtools --no-tests

## download HG19 reference data
## This downloads from UCSC
#WORKDIR /opt/hap.py-data
#ENV PATH $PATH:/opt/hap.py/bin
#RUN /opt/hap.py-source/src/sh/make_hg19.sh && samtools faidx /opt/hap.py-data/hg19.fa
## Run tests
#ENV HG19 /opt/hap.py-data/hg19.fa
#WORKDIR /opt/hap.py
#RUN /opt/hap.py-source/src/sh/run_tests.sh
#
## remove source folder
#WORKDIR /
#RUN rm -rf /opt/hap.py-source


# Install Dotnet 2.1
RUN apt-get update && apt-get -y install wget
RUN wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get -y install apt-transport-https curl
RUN apt-get update
RUN apt-get -y install dotnet-sdk-2.1

WORKDIR /app
RUN git clone https://github.com/Illumina/Pisces.git
RUN tar -xzf Pisces/binaries/5.2.5.20/Pisces_5.2.5.20.tar.gz 

# Now we should have 
# /opt/hap.py/bin/hap.py
# dotnet /app/Pisces_5.2.5.20/Pisces.dll

WORKDIR /app
COPY run_analysis.sh /app


