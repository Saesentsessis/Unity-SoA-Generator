using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace StructureOfArraysGenerator.Core;

public readonly struct AccessorField
{
    public SpecialType SpecialType { get; }
    public string TypeName { get; }
    public string LeafPointerName { get; }
    public int RootByteOffset { get; }
    public AccessorField[] Children { get; }

    public AccessorField(SpecialType specialType, string typeName, string leafPointerName, int rootByteOffset, AccessorField[] children)
    {
        SpecialType = specialType;
        TypeName = typeName;
        LeafPointerName = leafPointerName;
        RootByteOffset = rootByteOffset;
        Children = children;
    }

    public bool HasChildren => Children.Length > 0;
    public bool IsFlag => SpecialType == SpecialType.System_Boolean;
}

public readonly struct UniformAccessorMetadata
{
    public string Name { get; }
    public string TypeName { get; }
    public ImmutableArray<AccessorField> Nodes { get; }

    public UniformAccessorMetadata(string name, string typeName, ImmutableArray<AccessorField> nodes)
    {
        Name = name;
        TypeName = typeName;
        Nodes = nodes;
    }
}
