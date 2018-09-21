namespace Common.IO.Utility
{
    public interface IApplicationOptions
    {
        string QuotedCommandLineArgumentsString { get; }
        string[] CommandLineArguments { get; set; }

        string LogFileNameBase { get; }
        string LogFolder { get; }

        string OutputDirectory { get; set; }

        void Save(string filepath);
        void SetIODirectories(string programName);
        string GetMainInputDirectory();
    }
}