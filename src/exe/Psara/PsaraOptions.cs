using System.IO;
using Common.IO.Utility;
using Pisces.Domain.Options;

namespace Psara
{
    public class PsaraOptions : BaseApplicationOptions
    {
        public GeometricFilterParameters GeometricFilterParameters = new GeometricFilterParameters();

        public const string DefaultLogFolderName = "PsaraLogs";
        public string LogFileNameBase = "PsaraLog.txt";
        public string OutputDirectory = "";
        public string InputVcf = "";

        public string LogFolder
        {
            get
            {
                string vcfDir = Path.GetDirectoryName(this.InputVcf);

                if (string.IsNullOrEmpty(OutputDirectory))
                {
                    if (string.IsNullOrEmpty(vcfDir)) //the rare case when the input vcf is "myvcf.vcf" and has no parent folder
                        return DefaultLogFolderName;
                    else
                    {
                        var logFolder = Path.Combine(vcfDir, DefaultLogFolderName);
                        if (!Directory.Exists(logFolder))
                            Directory.CreateDirectory(logFolder);

                        return logFolder; //no output folder was given
                    }
                }
                else //an output folder was given
                {
                    var logFolder = Path.Combine(OutputDirectory, DefaultLogFolderName);
                    if (!Directory.Exists(logFolder))
                        Directory.CreateDirectory(logFolder);

                    return logFolder;

                }
            }
        }

        public string LogFileName
        {
            get
            {
                return Path.Combine(LogFolder, LogFileNameBase);
            }
        }
    
    }
}
