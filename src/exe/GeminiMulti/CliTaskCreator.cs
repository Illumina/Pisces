using Pisces.Processing.Utility;

namespace GeminiMulti
{
    public class CliTaskCreator
    {
        /// <summary>
        /// Get a per-chromosome CliTask for the given chromosome, executable path, and logging directory. Assumes all passed args are properly quoted, and will quote the parameters added within the function.
        /// </summary>
        /// <param name="args">Assumed to be a properly-ordered arguments array with any values being properly quoted.</param>
        /// <param name="chrom"></param>
        /// <param name="exePath"></param>
        /// <param name="outdir"></param>
        /// <param name="chromRefId"></param>
        /// <param name="logger">Logger. Caller's responsibility to dispose.</param>
        /// <returns></returns>
        public virtual ICliTask GetCliTask(string[] args, string chrom, string exePath, string outdir, int chromRefId)
        {
            return new CliTask("Gemini_" + chrom, "dotnet",
                $"\"{exePath}\" " +
                string.Join(" ", args) + " --chromRefId \"" + chromRefId + "\" --outFolder \"" + outdir + "\"");

        }

    }
}