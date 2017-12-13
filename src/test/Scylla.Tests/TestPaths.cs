using TestUtilities;

namespace Scylla.Tests
{
    //Dummy anchor points for the BaseTestPaths to pick up the assembly details.
    //This is because .NetCore is now the excutable, but we want the test paths with respect to this assembly.

    public class DummyClass { }
    public class TestPaths : BaseTestPaths<DummyClass> { }
}
