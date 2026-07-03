using System;

namespace Saesentsessis.DOD.SoA.CodeGen
{
    /// <summary>
    /// Instructs the source generator to create a convenience property that reconstructs
    /// this specific nested struct on-the-fly for a given index.
    /// Warning: If the target struct was flattened due to alignment optimization,
    /// accessing this property requires fetching data from multiple disjoint parallel arrays.
    /// This breaks cache locality and will incur multiple CPU cache line fetches,
    /// degrading performance compared to direct, per-field array iteration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GenerateSoAUniformAccessorAttribute : Attribute {}
}