using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SequencingFiles;

namespace CallSomaticVariants.Tests.MockBehaviors
{
    public static class TestUtility
    {
        public static byte[] GetXCTagData(string value)
        {
            var tagUtils = new TagUtils();
            tagUtils.AddStringTag("XC", value);
            return tagUtils.ToBytes();
        }

        public static CigarAlignment GetReadCigarFromStitched(string stitchedCigar, int readLength, bool reverse)
        {
            var cigar = new CigarAlignment(stitchedCigar);
            if (reverse)
                cigar.Reverse();

            var totalLengthSofar = 0;
            var newCigar = new CigarAlignment();

            for (var i = 0; i < cigar.Count; i++)
            {
                var operation = cigar[i];
                if (operation.IsReadSpan())
                {
                    if (totalLengthSofar + operation.Length > readLength)
                    {
                        newCigar.Add(new CigarOp(operation.Type, (uint)(readLength - totalLengthSofar)));
                        break;
                    }

                    newCigar.Add(operation);
                    totalLengthSofar += (int)operation.Length;

                    if (totalLengthSofar == readLength) 
                        break;
                }
                else
                {
                    newCigar.Add(operation);
                }
            }

            if (reverse) 
                newCigar.Reverse();

            return newCigar;
        }
    }
}
