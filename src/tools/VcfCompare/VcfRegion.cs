namespace VcfCompare
{
    public class VcfRegion
    {
        public string Chromosome { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public override string ToString()
        {
            return Chromosome + ":" + Start + "-" + End;
        }
    }
}