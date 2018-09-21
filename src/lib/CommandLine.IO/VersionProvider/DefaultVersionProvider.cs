using CommandLine.Util;
using Common.IO;

namespace CommandLine.VersionProvider
{
    public sealed class DefaultVersionProvider : IVersionProvider
    {
        public string GetProgramVersion() => $"{PiscesSuiteAppInfo.Title} {PiscesSuiteAppInfo.InformationalVersion}";

        public string GetDataVersion()    => $"{PiscesSuiteAppInfo.Version}";
    }
}
