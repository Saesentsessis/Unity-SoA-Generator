using System;

namespace Saesentsessis.DOD.SoA.CodeGen
{
    /// <summary>
    /// Marks a struct to be processed by the SoA Source Generator, which will emit a 
    /// Burst-compatible, contiguous Structure of Arrays container for this type.
    /// Generated file would be called as {TargetName}SoA.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class GenerateSoAAttribute : Attribute
    {
        /// <summary>
        /// If set to true, the generator will recursively decompose any nested structs
        /// whose size-to-alignment ratio would otherwise introduce memory padding gaps.
        /// This mathematically guarantees 100% memory density, but will replace the
        /// nested struct's single array accessor with separate, parallel arrays for
        /// each of its constituent primitive fields.
        /// </summary>
        public bool AllowFieldHierarchyFlattening { get; }

        /// <summary>
        /// If set to true, the generator will transform any boolean fields into a separate
        /// vertically packed bit array block located after all other primitive fields. 
        /// While this may introduce a slight padding gap to align the bit block to an 8-byte 
        /// boundary, it mathematically prevents the 87.5% memory waste inherent to standard 
        /// 1-byte aligned C# booleans.
        /// </summary>
        public bool AllowBooleanBitPacking { get; }

        /// <summary>
        /// Defines a custom identifier for the generated SoA container. 
        /// If omitted or left null, the generator defaults to appending the "SoA" suffix 
        /// to the target struct's name (e.g., <c>PointMassSoA</c>).
        /// </summary>
        public string StructName { get; set; } = string.Empty;

        /// <summary>
        /// Defines a custom namespace declaration for the generated SoA container. 
        /// If omitted or left null, the generated struct will be injected into the 
        /// exact same namespace as the target struct.
        /// </summary>
        public string StructNamespace { get; set; } = string.Empty;

        /// <summary>
        /// Instructs source generator to generate additional structure, that would the role
        /// as a safe wrapper over unmanaged memory.
        /// </summary>
        public bool GenerateNativeContainer { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateSoAAttribute"/> class, defining 
        /// layout optimization strategies for the generated container.
        /// </summary>
        /// <param name="allowFieldHierarchyFlattening">
        /// If set to true, the generator will recursively decompose any nested structs
        /// whose size-to-alignment ratio would otherwise introduce memory padding gaps.
        /// This mathematically guarantees 100% memory density, but will replace the
        /// nested struct's single array accessor with separate, parallel arrays for
        /// each of its constituent primitive fields.
        /// </param>
        /// <param name="allowBooleanBitPacking">
        /// If set to true, the generator will transform any boolean fields into a separate
        /// vertically packed bit array block located after all other primitive fields. 
        /// While this may introduce a slight padding gap to align the bit block to an 8-byte 
        /// boundary, it mathematically prevents the 87.5% memory waste inherent to standard 
        /// 1-byte aligned C# booleans.
        /// </param>
        public GenerateSoAAttribute(bool allowFieldHierarchyFlattening = true, bool allowBooleanBitPacking = true)
        {
            AllowFieldHierarchyFlattening = allowFieldHierarchyFlattening;
            AllowBooleanBitPacking = allowBooleanBitPacking;
        }
    }
}