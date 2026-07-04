namespace StructureOfArraysGenerator.Layout;

public readonly struct LayoutInfo
{
    public int AlignedSize { get; }
    public int Alignment { get; }
    public int RawSize { get; }
    public int ChildCount { get; }

    public LayoutInfo(int alignedSize, int alignment, int rawSize, int childCount)
    {
        AlignedSize = alignedSize;
        Alignment = alignment;
        RawSize = rawSize;
        ChildCount = childCount;
    }

    public static LayoutInfo Invalid => new LayoutInfo(0, 0, 0, 0);

    public bool HasPadding => AlignedSize > RawSize;
    public bool HasChildren => ChildCount > 0;
}
