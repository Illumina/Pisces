using System;
using System.Collections.Generic;
using System.IO;
using Pisces.IO.Interfaces;

namespace Pisces.IO
{
    public interface IVcfWriter<T> 
    {
        void Write(IEnumerable<T> BaseCalledAlleles, IRegionMapper mapper = null);
        void WriteRemaining(IRegionMapper mapper = null, Dictionary<int, List<Tuple<string, string>>> forcedAllelesByPos = null);
    }

    public interface IVcfFileWriter<T> : IVcfWriter<T>, IDisposable
    {
        void WriteHeader();
    }
}