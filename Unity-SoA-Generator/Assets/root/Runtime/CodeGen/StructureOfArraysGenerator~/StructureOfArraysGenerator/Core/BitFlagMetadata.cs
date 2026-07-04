namespace StructureOfArraysGenerator.Core;

public readonly struct BitFlagMetadata
{
    public string Name { get; }
    public string Path { get; }

    public BitFlagMetadata(string name, string path)
    {
        Name = name;
        Path = path;
    }
}
