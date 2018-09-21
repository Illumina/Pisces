using System.Collections.Generic;
using System.Linq;
using Alignment.Domain.Sequencing;

namespace StitchingLogic
{
    public struct StitchableItem
    {
        public CigarOp CigarOp { get; private set; }
        public char? Base { get; private set; }
        public byte? Quality { get; private set; }

        public StitchableItem(CigarOp op, char? seqBase, byte? quality)
        {
            CigarOp = op;
            Base = seqBase;
            Quality = quality;
        }
    }

    public enum ReadNumber
    {
        Read1, Read2
    }

    public class StitchedSite
    {
        protected List<StitchableItem> R1Ops { get; private set; }
        protected List<StitchableItem> R2Ops { get; private set; }

        private int R1OpsCount { get; set; }
        private int R2OpsCount { get; set; }

        public StitchedSite()
        {
            R1Ops = new List<StitchableItem>();
            R2Ops = new List<StitchableItem>();
        }

        public List<StitchableItem> GetOpsForRead(ReadNumber num)
        {
            if (num == ReadNumber.Read1)
            {
                return R1Ops;
            }
            else return R2Ops;
        }

        public int GetNumOpsForRead(ReadNumber num)
        {
            if (num == ReadNumber.Read1)
            {
                return R1OpsCount;
            }
            else return R2OpsCount;
        }

        public void SetOpsForRead(ReadNumber num, List<StitchableItem> ops)
        {
            if (num == ReadNumber.Read1)
            {
                R1Ops = ops;
                R1OpsCount = ops.Count;
            }
            else
            {
                R2Ops = ops;
                R2OpsCount = ops.Count;
            }
        }

        public void AddOpsForRead(ReadNumber num, StitchableItem ops)
        {
            if (num == ReadNumber.Read1)
            {
                R1OpsCount++;
                R1Ops.Add(ops);
            }
            else
            {
                R2OpsCount++;
                R2Ops.Add(ops);
            }

        }

        public void AddOpsForRead(ReadNumber num, List<StitchableItem> ops)
        {
            if (num == ReadNumber.Read1)
            {
                R1OpsCount += ops.Count;
                R1Ops.AddRange(ops);
            }
            else
            {
                R2OpsCount += ops.Count;
                R2Ops.AddRange(ops);
            }

        }

        public bool HasValue()
        {
            return R1OpsCount > 0 || R2OpsCount > 0;
        }

        public string Stringify()
        {
            return string.Join("", R1Ops?.Select(x => x.CigarOp.Type)) + "/" +
                   string.Join("", R2Ops?.Select(x => x.CigarOp.Type));
        }

        public void Reset()
        {
            R1Ops?.Clear();
            R2Ops?.Clear();
            R1OpsCount = 0;
            R2OpsCount = 0;
        }
    }

    public class UnmappedStretch : StitchedSite
    {
        public bool R1HasInsertion()
        {
            return R1Ops !=null && R1Ops.Any(x => x.CigarOp.Type == 'I');
        }
        public bool R2HasInsertion()
        {
            return R2Ops != null && R2Ops.Any(x => x.CigarOp.Type == 'I');
        }

        public bool IsPrefix;
        public bool IsSuffix;

    }

}