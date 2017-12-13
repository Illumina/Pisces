using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Pisces.Tests.UnitTests;
using TestUtilities;

namespace Pisces.Tests
{
    //Dummy anchor points for the BaseTestPaths to pick up the assembly details.
    //This is because .NetCore is now the excutable, but we want the test paths with respect to this assembly.

    public class DummyClass { }
    public class TestPaths : BaseTestPaths<DummyClass> { }
}
