
namespace ReformatVcf
{
    class Program
    {
        static void Main(string[] args)
        {
            var inputFile = args[0];
            bool crush = false;
            if (args.Length > 1)
            {
                if (args[1].ToLower() == "-crush")
                {
                    crush = bool.Parse(args[2]);
                }
            }

            Reformat.DoReformating(inputFile, crush);
        }

    }
}