using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pisces.IO.Sequencing;
using Common.IO;


namespace VariantQualityRecalibration
{
    public class VcfRewriter : IDisposable
    {
        private const int VcfHeaderOffset = 4;
        StreamWriter writer;

        public VcfRewriter(string vcfOutPath)
        {
            writer = new StreamWriter(new FileStream(vcfOutPath, FileMode.CreateNew));
        }

        public void WriteHeader(List<string> headerLines, string quotedCommandLineArgumentsString)
        {
            writer.NewLine = "\n";
            WriteHeaders(writer, headerLines, quotedCommandLineArgumentsString);
        }

        public void WriteVariantLine(VcfVariant varaint)
        {
            writer.WriteLine(varaint);
        }

        public void Dispose()
        {
            writer.Close();
            writer.Dispose();
        }

        private static void WriteHeaders(StreamWriter writer, List<string> headerLines, string quotedCommandLineString)
        {
            foreach (string headerLine in headerLines.Take(VcfHeaderOffset))
                writer.WriteLine(headerLine);

            var currentVersion = FileUtilities.LocalAssemblyVersion<QualityRecalibration>();
            writer.WriteLine("##VariantQualityRecalibration=VQR " + currentVersion);

            if (!string.IsNullOrEmpty(quotedCommandLineString))
                writer.WriteLine("##VQR_cmdline=" + quotedCommandLineString);

            for (var i = VcfHeaderOffset; i < headerLines.Count; i++)
                writer.WriteLine(headerLines[i]);

        }

    }
}
