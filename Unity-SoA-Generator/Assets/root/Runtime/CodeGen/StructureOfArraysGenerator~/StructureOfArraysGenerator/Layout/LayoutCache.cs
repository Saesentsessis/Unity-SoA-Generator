using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace StructureOfArraysGenerator.Layout;

public class LayoutCache : IDisposable
{
    private static string GetPath<T>(T symbol) where T : ITypeSymbol
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private readonly Dictionary<string, LayoutInfo> _cache = new();

    public LayoutInfo GetOrCreate<T>(T symbol) where T : ITypeSymbol
    {
        var path = GetPath(symbol);

        if (_cache.TryGetValue(path, out var info) == false)
            _cache.Add(path, info = CalculateLayoutInfo(symbol, []));
        
        return info;
    }

    private bool Get(string path, out LayoutInfo info)
    {
        return _cache.TryGetValue(path, out info);
    }

    private LayoutInfo CalculateLayoutInfo(ITypeSymbol typeSymbol, HashSet<ITypeSymbol> visitedTypes)
    {
        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte: return new LayoutInfo(1, 1, 1, 0);
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Char: return new LayoutInfo(2, 2, 2, 0);
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Single: return new LayoutInfo(4, 4, 4, 0);
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double: return new LayoutInfo(8, 8, 8, 0);
        }

        if (typeSymbol.TypeKind != TypeKind.Struct || typeSymbol is not INamedTypeSymbol namedStruct || visitedTypes.Add(typeSymbol) == false)
            return LayoutInfo.Invalid;
        
        var currentOffset = 0;
        var maxAlignment = 1;
        var rawSize = 0;
        var childCount = 0;

        foreach (var field in namedStruct.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst)
                continue;
            
            childCount++;
            
            var fieldLayout = CalculateLayoutInfo(field.Type, visitedTypes);
            
            if (fieldLayout.Equals(LayoutInfo.Invalid))
                continue;
            
            int alignmentGap = (fieldLayout.Alignment - currentOffset % fieldLayout.Alignment) % fieldLayout.Alignment;
            
            currentOffset += alignmentGap;
            currentOffset += fieldLayout.AlignedSize;

            rawSize += fieldLayout.AlignedSize;
            maxAlignment = Math.Max(maxAlignment, fieldLayout.Alignment);
        }
        
        visitedTypes.Remove(namedStruct);

        var trailingGap = (maxAlignment - currentOffset % maxAlignment) % maxAlignment;
        var totalAlignedSize = currentOffset + trailingGap;

        return new LayoutInfo(totalAlignedSize, maxAlignment, rawSize, childCount);
    }
    
    public void Dispose()
    {
        _cache.Clear();
    }
}