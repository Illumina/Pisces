using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;

namespace StitchingLogic
{
    public enum ReadNumber
    {
        Read1, Read2
    }

    public class StitchedSite
    {
        public List<CigarOp> R1Ops { get; set; }
        public List<CigarOp> R2Ops { get; set; }

        public StitchedSite()
        {
            R1Ops = new List<CigarOp>();
            R2Ops = new List<CigarOp>();
        }
        public List<CigarOp> GetOpsForRead(ReadNumber num)
        {
            if (num == ReadNumber.Read1)
            {
                return R1Ops;
            }
            else return R2Ops;
        }

        public void SetOpsForRead(ReadNumber num, List<CigarOp> ops)
        {
            if (num == ReadNumber.Read1)
            {
                R1Ops = ops;
            }
            else
            {
                R2Ops = ops;
            }
        }

        public void AddOpsForRead(ReadNumber num, List<CigarOp> ops)
        {
            if (num == ReadNumber.Read1)
            {
                R1Ops.AddRange(ops);
            }
            else
            {
                R2Ops.AddRange(ops);
            }

        }
        public bool HasValue()
        {
            return R1Ops.Count > 0 || R2Ops.Count > 0;
        }

        public string Stringify()
        {
            return string.Join("", R1Ops.Select(x => x?.Type)) + "/" +
                   string.Join("", R2Ops.Select(x => x?.Type));
        }

        public void Reset()
        {
            R1Ops.Clear();
            R2Ops.Clear();
        }
    }

    public class UnmappedStretch : StitchedSite
    {
        public bool R1HasInsertion()
        {
            return R1Ops.Any(x => x.Type == 'I');
        }
        public bool R2HasInsertion()
        {
            return R2Ops.Any(x => x.Type == 'I');
        }

        public bool IsPrefix;
        public bool IsSuffix;

    }

}