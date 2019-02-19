
namespace VariantPhasing.Interfaces
{
    public interface INeighborhood
    {
        int LastPositionOfInterestWithLookAhead { get; }
        int LastPositionOfInterestInVcf { get; }
        int FirstPositionOfInterest { get; }
        string ReferenceName { get; }
    }
}
