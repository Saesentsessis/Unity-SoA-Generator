using System.Collections.Immutable;

namespace StructureOfArraysGenerator.Core;

public readonly struct StructMetadata
{
    public string Name { get; }
    public string Namespace { get; }
    public bool GenerateNativeContainer { get; }
    public ImmutableArray<FieldMetadata> Fields { get; }
    public ImmutableArray<BitFlagMetadata> Flags { get; }
    public ImmutableArray<UniformAccessorMetadata> Accessors { get; }

    public StructMetadata(string name, string @namespace, bool generateNativeContainer, ImmutableArray<FieldMetadata> fields, ImmutableArray<BitFlagMetadata> flags, ImmutableArray<UniformAccessorMetadata> accessors)
    {
        Name = name;
        Namespace = @namespace;
        GenerateNativeContainer = generateNativeContainer;
        Fields = fields;
        Flags = flags;
        Accessors = accessors;
    }

    public bool IsEmpty => Fields.IsDefaultOrEmpty && Flags.IsDefaultOrEmpty;
}
