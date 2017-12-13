using System.Resources;
using Pisces.Domain.Models;

namespace Pisces.Interfaces
{
    public interface IAlignmentSource
    {
        Read GetNextRead();
        int? LastClearedPosition { get; }

        bool SourceIsStitched { get; }

        bool SourceIsCollapsed { get; }
    }
}