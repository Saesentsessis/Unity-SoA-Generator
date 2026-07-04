using Saesentsessis.DOD.SoA.CodeGen;
using Unity.Mathematics;

namespace Saesentsessis.DOD.SoA.Tests
{
    [GenerateSoA(GenerateNativeContainer = true)]
    public struct SimpleEntity
    {
        public float Health;
        public int Id;
        public float3 Position;
    }

    [GenerateSoA(GenerateNativeContainer = true)]
    public struct FlaggedEntity
    {
        public float Speed;
        public int Score;
        public bool IsActive;
        public bool IsVisible;
    }

    public struct InnerPadded
    {
        public byte Tag;
        public int Value;
    }

    [GenerateSoA(GenerateNativeContainer = true)]
    public struct NestedPadded
    {
        [GenerateSoAUniformAccessor]
        public InnerPadded Inner;
        public float Weight;
    }

    [GenerateSoA(allowBooleanBitPacking: false, GenerateNativeContainer = true)]
    public struct NoBitPackEntity
    {
        public int Id;
        public bool Flag;
    }
}
