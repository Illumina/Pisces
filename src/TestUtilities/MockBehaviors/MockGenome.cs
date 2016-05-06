using System;
using System.Collections.Generic;
using System.Linq;
using Pisces.Domain.Interfaces;
using Pisces.Domain.Models;

namespace TestUtilities.MockBehaviors
{
    public class MockGenome : IGenome
    {
        private List<ChrReference> _chrReferences;
 
        public MockGenome(List<ChrReference> chrReferences)
        {
            _chrReferences = chrReferences;
            Directory = "MockDirectory";
        }

        public MockGenome(List<ChrReference> chrReferences, string directory)
        {
            _chrReferences = chrReferences;
            Directory = directory;
        }

        public List<string> ChromosomesToProcess {
            get { return _chrReferences.Select(c => c.Name).ToList(); }
            set { }
        }

        public IEnumerable<Tuple<string, long>> ChromosomeLengths {
            get { return _chrReferences.Select(c => new Tuple<string, long>(c.Name, c.Sequence.Length)); }
        }

        public ChrReference GetChrReference(string chrName)
        {
            ChrReference chrTemp = _chrReferences.First(c => c.Name == chrName);
            ChrReference newGuy = new ChrReference()
            {
                Name = chrTemp.Name,
                Sequence = chrTemp.Sequence
            };
            return newGuy;
        }

        public string Directory { get; private set; }
    }
}
