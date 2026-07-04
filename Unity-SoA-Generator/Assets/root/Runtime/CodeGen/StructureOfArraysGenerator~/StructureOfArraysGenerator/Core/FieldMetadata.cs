namespace StructureOfArraysGenerator.Core;

public readonly struct FieldMetadata
{
    public string Path { get; }
    public string TypeName { get; }
    public int Size { get; }
    public int RawSize { get; }
    public int Alignment { get; }

    public FieldMetadata(string path, string typeName, int size, int rawSize, int alignment)
    {
        Path = path;
        TypeName = typeName;
        Size = size;
        RawSize = rawSize;
        Alignment = alignment;
    }
}
