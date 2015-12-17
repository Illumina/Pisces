using System;
using System.Collections.Generic;
using CallSomaticVariants.Models.Alleles;

namespace CallSomaticVariants.Interfaces
{
    public interface IVcfWriter 
    {
        void Write(IEnumerable<BaseCalledAllele> calledVariants);
    }

    public interface IVcfFileWriter : IVcfWriter, IDisposable
    {
        void WriteHeader();
    }
}