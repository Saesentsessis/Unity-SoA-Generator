using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using StructureOfArraysGenerator.Core;
using StructureOfArraysGenerator.Layout;

namespace StructureOfArraysGenerator;

[Generator]
public class SoAGenerator : IIncrementalGenerator
{   
    private const string GenerateSoAAttributeName = "GenerateSoAAttribute";
    private const string GenerateSoAAttributeShortName = "GenerateSoA";
    
    private const string OverrideNamePropertyName = "StructName";
    private const string OverrideNamespacePropertyName = "StructNamespace";
    private const string GenerateNativeContainerPropertyName = "GenerateNativeContainer";
    
    private const string GenerateSoAUniformAccessorAttributeName = "GenerateSoAUniformAccessorAttribute";
    private const string GenerateSoAUniformAccessorAttributeShortName = "GenerateSoAUniformAccessor";
    
    private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted, // Removes 'global::'
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: 
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | 
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsSyntaxTargetForGeneration(node),
            transform: static (context, _) => GetSemanticTargetForGeneration(context)
        ).Where(static m => m is not null).Select((m, _) => m!.Value);
        
        context.RegisterSourceOutput(structDeclarations, ExecuteGeneration);
    }

    /// <summary>
    /// Fast path: Operates purely on the text/syntax.
    /// Checks if the node is a struct declaration with at least one attribute.
    /// </summary>
    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is StructDeclarationSyntax { AttributeLists.Count: > 0 };
    }
    
    /// <summary>
    /// Slow path: Operates on the Semantic Model.
    /// Resolves the symbols to ensure the attribute is exactly ours.
    /// </summary>
    private static StructMetadata? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var structDeclaration = (StructDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(structDeclaration) is not INamedTypeSymbol structSymbol)
            return null;

        var soaAttribute = structSymbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name is GenerateSoAAttributeName or GenerateSoAAttributeShortName);

        if (soaAttribute == null)
            return null;

        GenerationParams generationParams = 0;

        if (ExtractConstructorArgumentOrDefault(soaAttribute.ConstructorArguments, 0, true))
            generationParams |= GenerationParams.AllowFlattening;
        
        if (ExtractConstructorArgumentOrDefault(soaAttribute.ConstructorArguments, 1, true))
            generationParams |= GenerationParams.AllowBitPacking;

        var fields = new List<FieldMetadata>();
        var flags = new List<BitFlagMetadata>();
        var accessors = new List<UniformAccessorMetadata>();
        var paramsView = new GenerationParamsView(generationParams);
        using var layoutCache = new LayoutCache();
        
        ProcessFields(structSymbol, string.Empty, paramsView, layoutCache, fields, flags, accessors);
        
        fields.Sort((a, b) => b.Alignment.CompareTo(a.Alignment));

        var name = ExtractNamedArgumentOrNull<string>(soaAttribute.NamedArguments, OverrideNamePropertyName);
        var @namespace = ExtractNamedArgumentOrNull<string>(soaAttribute.NamedArguments, OverrideNamespacePropertyName);
        var generateNativeContainer = ExtractNamedArgumentOrDefault<bool>(soaAttribute.NamedArguments, GenerateNativeContainerPropertyName);

        name ??= structSymbol.Name + "SoA";
        
        @namespace ??= structSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : structSymbol.ContainingNamespace.ToDisplayString();

        return new StructMetadata(
            name,
            @namespace,
            generateNativeContainer,
            fields.ToImmutableArray(),
            flags.ToImmutableArray(),
            accessors.ToImmutableArray()
        );
    }

    private static void ProcessFields(
        INamedTypeSymbol structSymbol,
        string pathPrefix,
        in GenerationParamsView parameters,
        LayoutCache layoutCache,
        List<FieldMetadata> fields,
        List<BitFlagMetadata> flags,
        List<UniformAccessorMetadata> accessors)
    {
        foreach (var field in structSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst)
                continue;

            var currentPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}_{field.Name}";

            if (parameters.AllowBitPacking && field.Type.SpecialType == SpecialType.System_Boolean)
            {
                flags.Add(new BitFlagMetadata(
                    field.Name,
                    currentPath
                ));
                
                continue;
            }
            
            var fieldLayout = layoutCache.GetOrCreate(field.Type);

            if (parameters.AllowFlattening && fieldLayout.HasPadding && field.Type is INamedTypeSymbol { TypeKind: TypeKind.Struct } fieldType)
            {
                if (fieldLayout.HasChildren)
                {
                    if (field.GetAttributes().Any(a => a.AttributeClass?.Name
                            is GenerateSoAUniformAccessorAttributeName
                            or GenerateSoAUniformAccessorAttributeShortName))
                        accessors.Add(
                            new UniformAccessorMetadata(
                                field.Name,
                                field.Type.ToDisplayString(TypeFormat),
                                BuildAccessorNodes(fieldType, currentPath, in parameters, layoutCache).ToImmutableArray()
                            )
                        );
                    
                    // Recursively flatten
                    ProcessFields(fieldType, currentPath, in parameters, layoutCache, fields, flags, accessors);
                }

                continue;
            }

            // Record perfectly packed struct or primitive
            fields.Add(new FieldMetadata(
                currentPath,
                field.Type.ToDisplayString(TypeFormat),
                fieldLayout.AlignedSize,
                fieldLayout.RawSize,
                fieldLayout.Alignment
            ));
        }
    }
    
    private static AccessorField[] BuildAccessorNodes(
        INamedTypeSymbol structType,
        string pathPrefix,
        in GenerationParamsView parameters,
        LayoutCache layoutCache,
        int baseByteOffset = 0)
    {
        var layout = GetStructLayout(structType);

        // LayoutKind.Auto and LayoutKind.Explicit is not supported.
        if (layout.Kind != LayoutKind.Sequential)
            return Array.Empty<AccessorField>();
        
        var nodes = new List<AccessorField>();
        int currentLocalByteOffset = 0;
        
        foreach (var member in structType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.IsStatic || member.IsConst)
                continue;
            
            var fieldLayout = layoutCache.GetOrCreate(member.Type);

            var effectiveAlignment = layout.Pack > 0
                ? Math.Min(fieldLayout.Alignment, layout.Pack)
                : fieldLayout.Alignment;

            var alignmentGap = (effectiveAlignment - currentLocalByteOffset % effectiveAlignment) %
                               effectiveAlignment;
            currentLocalByteOffset += alignmentGap;

            var absoluteByteOffset = baseByteOffset + currentLocalByteOffset;
            
            var currentPath = string.IsNullOrEmpty(pathPrefix) ? member.Name : $"{pathPrefix}_{member.Name}";

            if (fieldLayout.HasPadding && member.Type is INamedTypeSymbol { TypeKind: TypeKind.Struct } fieldType)
            {
                // It's a flattened nested struct — recurse deeper
                var children = BuildAccessorNodes(fieldType, currentPath, in parameters, layoutCache, absoluteByteOffset);

                // If we encountered any struct that has LayoutKind.Auto - drop any further recursion.
                if (children.Length == 0)
                    return Array.Empty<AccessorField>();

                nodes.Add(new AccessorField(
                    fieldType.SpecialType,
                    fieldType.ToDisplayString(TypeFormat),
                    string.Empty,
                    absoluteByteOffset,
                    children
                ));
            }
            else
            {
                var leafPtrName = parameters.AllowBitPacking && member.Type.SpecialType == SpecialType.System_Boolean
                    ? $"{currentPath}BitArray"
                    : $"{currentPath}Ptr";
                
                // It's a perfectly packed struct or a primitive — it maps to exactly one SoA array
                nodes.Add(new AccessorField(
                    member.Type.SpecialType,
                    member.Type.ToDisplayString(TypeFormat),
                    leafPtrName,
                    absoluteByteOffset,
                    Array.Empty<AccessorField>()
                ));
            }

            currentLocalByteOffset += fieldLayout.AlignedSize;
        }

        nodes.Sort((a, b) => b.RootByteOffset.CompareTo(a.RootByteOffset));

        return nodes.ToArray();
    }
    
    private static (LayoutKind Kind, int Pack) GetStructLayout(INamedTypeSymbol structSymbol)
    {
        // C# structs default to Sequential, Pack = 0 (natural alignment)
        LayoutKind kind = LayoutKind.Sequential;
        int pack = 0;

        var layoutAttr = structSymbol.GetAttributes().FirstOrDefault(a => 
            a.AttributeClass?.Name is "StructLayoutAttribute" or "StructLayout");

        if (layoutAttr == null || layoutAttr.ConstructorArguments.Length <= 0)
            return (kind, pack);

        kind = layoutAttr.ConstructorArguments[0].Value switch
        {
            // LayoutKind is passed as the first constructor argument (either int or short depending on targeting)
            int kindInt => (LayoutKind)kindInt,
            short kindShort => (LayoutKind)kindShort,
            _ => kind
        };

        // Pack is passed as a named argument
        var packNamedArg = layoutAttr.NamedArguments.FirstOrDefault(n => n.Key == "Pack");
        
        if (packNamedArg.Value.Value is int packVal)
            pack = packVal;

        return (kind, pack);
    }

    private static T? ExtractNamedArgumentOrNull<T>(in ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string key)
        where T : class
    {
        foreach (var pair in namedArguments)
        {
            if (pair.Key.Equals(key) == false)
                continue;

            return pair.Value.Value as T;
        }

        return null;
    }

    private static T ExtractNamedArgumentOrDefault<T>(in ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string key)
        where T : unmanaged
    {
        foreach (var pair in namedArguments)
        {
            if (pair.Key.Equals(key) == false)
                continue;

            return (T)pair.Value.Value!;
        }
        
        return default;
    }
    
    private static T ExtractConstructorArgumentOrDefault<T>(in ImmutableArray<TypedConstant> constants, int index, T fallback)
        where T : unmanaged
    {
        if (constants.IsDefaultOrEmpty || constants.Length <= index)
            return fallback;

        if (constants[index].Value is T result)
            return result;

        return fallback;
    }
    
    private static void ExecuteGeneration(SourceProductionContext context, StructMetadata metadata)
    {
        if (metadata.IsEmpty)
            return;
        
        var unsafeStructName = "Unsafe" + metadata.Name;

        FieldMetadata field;
        BitFlagMetadata flag;
        var fields = metadata.Fields;
        var flags = metadata.Flags;
        
        var fieldPtrNames = Array.Empty<string>();
        var flagBitArrayNames = Array.Empty<string>();
        var accessors = metadata.Accessors;
        
        var fieldOffsets = Array.Empty<int>();
        var fieldOffsetNames = Array.Empty<string>();
        var flagBitIndexNames = Array.Empty<string>();

        var elementSize = 0;
        var rawElementSize = 0;
        var alignment = 1;
        
        if (fields.Length > 0)
        {
            fieldPtrNames = new string[fields.Length];
            fieldOffsets = new int[fields.Length - 1];
            fieldOffsetNames = new string[fields.Length - 1];
            
            elementSize = fields[0].Size;
            rawElementSize = fields[0].RawSize;
            alignment = fields[0].Alignment;
            fieldPtrNames[0] = $"{fields[0].Path}Ptr";
        
            for (int i = 1; i < fields.Length; i++)
            {
                field = fields[i];
                fieldPtrNames[i] = $"{field.Path}Ptr";
                fieldOffsets[i - 1] = elementSize;
                fieldOffsetNames[i - 1] = $"{field.Path}ByteOffset";
                elementSize += field.Size;
                rawElementSize += field.RawSize;
                alignment = Math.Max(field.Alignment, alignment);
            }
        }

        if (flags.Length > 0)
        {
            flagBitArrayNames = new string[flags.Length];
            flagBitIndexNames = new string[flags.Length - 1];
            
            flagBitArrayNames[0] = $"{flags[0].Path}BitArray";

            for (int i = 1; i < flags.Length; i++)
            {
                flag = flags[i];
                flagBitArrayNames[i] = $"{flag.Path}BitArray";
                flagBitIndexNames[i - 1] = $"{flag.Path}BitIndex";
            }
        }
        
        var writer = new CodeBuilder(new StringBuilder(16_384));

        #region Unsafe Generation
        
        writer.AppendLine(
                """
                //┌────────────────────────────────────────────────────────────────────────────────┐
                //│ <auto-generated>                                                               │
                //│     This code was generated by a tool.                                         │
                //│     (https://github.com/Saesentsessis/Unity-SoA-Generator)                     │
                //│                                                                                │
                //│     Changes to this file may cause incorrect behavior and will be lost if the  │
                //│     code is regenerated.                                                       │
                //│ </auto-generated>                                                              │
                //└────────────────────────────────────────────────────────────────────────────────┘
                using System;
                using System.Runtime.CompilerServices;
                using System.Diagnostics;
                using Unity.Burst;
                using Unity.Collections;
                using Unity.Collections.Specialized;
                using Unity.Collections.LowLevel.Unsafe;
                using Unity.Mathematics;
                using Unity.Jobs;
                using Saesentsessis.DOD.SoA;
                """)
            .AppendLine();

        var hasNamespace = string.IsNullOrEmpty(metadata.Namespace) == false;
        
        if (hasNamespace)
            writer.Append("namespace ").AppendLine(metadata.Namespace)
                .OpenScope();
                
        writer.Append("public unsafe partial struct ").Append(unsafeStructName).AppendLine(" : IStructureOfArrays,").IncrementIndent()
            .AppendLine("IDisposable,")
            .AppendLine("INativeDisposable,")
            .Append("IEquatable<").Append(unsafeStructName).AppendLine(">").DecrementIndent()
            .OpenScope();

        #region Fields

        #region Constants

        var maxCapacity = flags.Length > 0
            ? int.MaxValue / elementSize
            : int.MaxValue;
        
        writer.Append("public const int MaxCapacity = ").Append(maxCapacity).AppendLine(";")
            .Append("public const int ElementSize = ").Append(elementSize).EndLine()
            .Append("public const int PaddingPerElement = ").Append(elementSize - rawElementSize).EndLine()
            .Append("public const int FlagCount = ").Append(flags.Length).EndLine()
            .Append("private const int Alignment = ").Append(alignment).EndLine();

        // Write constant offsets for fields
        for (int i = 0; i < fieldOffsets.Length; i++)
            writer.Append("private const int ").Append(fieldOffsetNames[i]).Append(" = ").Append(fieldOffsets[i]).EndLine();
        
        // Write constant bit indices for flags
        for (int i = 0; i < flagBitIndexNames.Length; i++)
            writer.Append("private const int ").Append(flagBitIndexNames[i]).Append(" = ").Append(i + 1).EndLine();
        
        #endregion // Constants

        #region Instance
        
        writer.AppendLine();
        
        writer.AppendLine("[NativeDisableUnsafePtrRestriction]")
            .AppendLine("private void* _dataPtr;")
            .AppendLine("private int _capacity;")
            .AppendLine("private AllocatorManager.AllocatorHandle _allocator;");
        
        #endregion // Instance
        
        #endregion // Fields
        
        #region Constructors
        
        writer.AppendLine()
            // Summary
            .AppendLine("/// <summary>")
            .Append("/// Initializes and returns an instance of ").Append(unsafeStructName).AppendLine(".")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"initialCapacity\">The initial capacity of the container.</param>")
            .AppendLine("/// <param name=\"allocator\">The allocator to use.</param>")
            .AppendLine("/// <param name=\"options\">Whether newly allocated bytes should be zeroed out.</param>")
            // Constructor declaration
            .Append("public ").Append(unsafeStructName).AppendLine("(int initialCapacity, AllocatorManager.AllocatorHandle allocator,").IncrementIndent()
            .AppendLine("NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)").DecrementIndent()
            .OpenScope()
            // Constructor body start
            .AppendLine("_allocator = allocator;")
            .AppendLine("_capacity = initialCapacity;")
            .AppendLine();

        if (flags.Length > 0)
            writer.AppendLine("var byteSize = Bitwise.ComputeByteSize(ElementSize, _capacity, FlagCount);")
                .AppendLine("CollectionUtils.CheckByteSizeInRange(byteSize, int.MaxValue);")
                .AppendLine("_dataPtr = _allocator.Allocate((int)byteSize, Alignment, 1);");
        else
            writer.AppendLine("_dataPtr = _allocator.Allocate(ElementSize, Alignment, _capacity);");
            
        writer.AppendLine()
            .AppendLine("if ((options & NativeArrayOptions.ClearMemory) == 0)").IncrementIndent()
            .AppendLine("return;").DecrementIndent()
            .AppendLine()
            .AppendLine(flags.Length > 0
                ? "UnsafeUtility.MemClear(_dataPtr, byteSize);"
                : "UnsafeUtility.MemClear(_dataPtr, (long)_capacity * ElementSize);")
            // Constructor body end
            .CloseScope();

        #endregion // Constructors

        #region Properties

        #region Shared
        
        // IStructureOfArrays.ElementSize and IStructureOfArrays.FlagCount properties
        writer.AppendLine()
            .AppendLine("int IStructureOfArrays.ElementSize => CollectionUtils.AssumePositive(ElementSize);")
            .AppendLine("int IStructureOfArrays.FlagCount => CollectionUtils.AssumePositive(FlagCount);");
        
        // DataPtr property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Dense storage for SoA fields data.")
            .AppendLine("/// </summary>")
            .AppendLine("public void* DataPtr")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => _dataPtr;")
            .CloseScope();
        
        // Capacity property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The number of elements that can fit in the internal buffer.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <value>The number of elements that can fit in the internal buffer.</value>")
            .AppendLine("public int Capacity")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("readonly get => CollectionUtils.AssumePositive(_capacity);")
            .AppendLine("set => SetCapacity(value);")
            .CloseScope();
        
        // Allocator property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The allocator used to create the internal buffer.")
            .AppendLine("/// </summary>")
            .AppendLine("public AllocatorManager.AllocatorHandle Allocator")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => _allocator;")
            .CloseScope();
        
        // IsCreated property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Whether this list has been allocated (and not yet deallocated).")
            .AppendLine("/// </summary>")
            .AppendLine("/// <value>True if this list has been allocated (and not yet deallocated).</value>")
            .AppendLine("public readonly bool IsCreated")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => _dataPtr != null;")
            .CloseScope();
        
        // ByteSize property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The size of an internal buffer in bytes (e.g. amount of allocated memory).")
            .AppendLine("/// </summary>")
            .AppendLine("public readonly long ByteSize")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => Bitwise.ComputeByteSize(ElementSize, _capacity, FlagCount);")
            .CloseScope();
        
        #endregion // Shared

        #region Generated

        // Fields
        if (fields.Length > 0)
        {
            field = fields[0];

            // First property
            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// Raw memory accessor for ").Append(field.Path).AppendLine(" array.")
                .AppendLine("/// </summary>")
                .Append("public ").Append(field.TypeName).Append("* ").AppendLine(fieldPtrNames[0])
                .OpenScope()
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .Append("get => (").Append(field.TypeName).AppendLine("*)_dataPtr;")
                .CloseScope();

            // Other properties
            for (int i = 1; i < fields.Length; i++)
            {
                field = fields[i];
                var fieldPtrName = fieldPtrNames[i];
                var fieldOffsetName = fieldOffsetNames[i - 1];

                writer.AppendLine()
                    .AppendLine("/// <summary>")
                    .Append("/// Raw memory accessor for ").Append(field.Path).AppendLine(" array.")
                    .AppendLine("/// </summary>")
                    .Append("public ").Append(field.TypeName).Append("* ").AppendLine(fieldPtrName)
                    .OpenScope()
                    .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .Append("get => (").Append(field.TypeName).Append("*)((byte*)_dataPtr + (long)_capacity * ")
                    .Append(fieldOffsetName).AppendLine(");")
                    .CloseScope();
            }
        }

        // Flags
        if (flags.Length > 0)
        {
            flag = flags[0];

            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// Flag accessor for ").Append(flag.Path).AppendLine(" as bit array.")
                .AppendLine("/// </summary>")
                .Append("public UnsafeBitArray ").AppendLine(flagBitArrayNames[0])
                .OpenScope()
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLine("get")
                .OpenScope()
                .AppendLine("var byteSize = Bitwise.GetBlockCount(_capacity);")
                .AppendLine("var byteOffset = Bitwise.AlignUp((long)_capacity * ElementSize, 8);")
                .AppendLine("var result = new UnsafeBitArray((byte*)_dataPtr + byteOffset, byteSize, AllocatorManager.None);")
                .AppendLine("result.Length = _capacity;")
                .AppendLine("return result;")
                .CloseScope()
                .CloseScope();

            for (int i = 1; i < flags.Length; i++)
            {
                flag = flags[i];
                var flagBitArrayName = flagBitArrayNames[i];
                var flagBitIndexName = flagBitIndexNames[i - 1];
                
                writer.AppendLine()
                    .AppendLine("/// <summary>")
                    .Append("/// Flag accessor for ").Append(flag.Path).AppendLine(" as bit array.")
                    .AppendLine("/// </summary>")
                    .Append("public UnsafeBitArray ").AppendLine(flagBitArrayName)
                    .OpenScope()
                    .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLine("get")
                    .OpenScope()
                    .AppendLine("var byteSize = Bitwise.GetBlockCount(_capacity);")
                    .Append("var byteOffset = Bitwise.AlignUp((long)_capacity * ElementSize, 8) + byteSize * ").Append(flagBitIndexName).EndLine()
                    .AppendLine("var result = new UnsafeBitArray((byte*)_dataPtr + byteOffset, byteSize, AllocatorManager.None);")
                    .AppendLine("result.Length = _capacity;")
                    .AppendLine("return result;")
                    .CloseScope()
                    .CloseScope();
            }
        }

        #endregion // Generated
        
        #region Uniform Accessors
        
        foreach (var accessor in accessors)
        {
            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// An accessor of a flattened property of type ").Append(accessor.TypeName).Append('.').AppendLine()
                .AppendLine("/// </summary>")
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .Append("public ").Append(accessor.TypeName).Append(" Get").Append(accessor.Name).AppendLine("(int index)")
                .OpenScope();
            
            if (accessor.Nodes.IsDefaultOrEmpty)
            {
                writer.Append("throw new InvalidCastException(\"Unable to reconstruct ").Append(accessor.TypeName)
                    .AppendLine(" inside accessor. All of the nested struct properties must be declared with LayoutKind.Sequential.\");")
                    .CloseScope();
                continue;
            }
            
            writer.Append(accessor.TypeName).AppendLine(" result;")
                .AppendLine("byte* resultPtr = (byte*)&result;")
                .AppendLine();
            
            WriteNodes(writer, accessor.Nodes);
            
            writer.AppendLine()
                .AppendLine("return result;")
                .CloseScope();
        }

        #endregion // Uniform Accessors
        
        #endregion // Properties

        #region Methods

        // SetCapacity method
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Sets the capacity.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"capacity\">The new capacity.</param>")
            .AppendLine("public void SetCapacity(int capacity)")
            .OpenScope()
            .AppendLine("SetCapacity(ref _allocator, capacity);")
            .CloseScope();
        
        // SetCapacity method
        writer.AppendLine()
            .AppendLine("private void SetCapacity<U>(ref U allocator, int capacity)").IncrementIndent()
            .AppendLine("where U : unmanaged, AllocatorManager.IAllocator").DecrementIndent()
            .OpenScope()
            .AppendLine("CollectionUtils.CheckCapacityInRange(capacity, MaxCapacity);")
            .AppendLine()
            .AppendLine("var sizeOf = ElementSize;")
            .AppendLine("var newCapacity = math.max(capacity, CollectionHelper.CacheLineSize / sizeOf);")
            .AppendLine("long ceilPow2 = math.ceilpow2((long)newCapacity);")
            .AppendLine("newCapacity = (int)math.min(MaxCapacity, ceilPow2);")
            .AppendLine()
            .AppendLine("if (newCapacity == _capacity)").IncrementIndent()
            .AppendLine("return;").DecrementIndent()
            .AppendLine()
            .AppendLine("ResizeExact(ref allocator, newCapacity);")
            .CloseScope();
        
        // ResizeExact method
        writer.AppendLine()
            .AppendLine("private void ResizeExact(int capacity)")
            .OpenScope()
            .AppendLine("ResizeExact(ref _allocator, capacity);")
            .CloseScope();
        
        // ResizeExact method
        writer.AppendLine()
            .AppendLine("private void ResizeExact<U>(ref U allocator, int newCapacity)")
            .AppendLine("where U : unmanaged, AllocatorManager.IAllocator")
            .OpenScope()
            .AppendLine("newCapacity = math.max(0, newCapacity);")
            .AppendLine()
            .AppendLine("CollectionUtils.CheckAllocator(Allocator);")
            .AppendLine("void* newDataPtr = null;")
            .AppendLine()
            .AppendLine("if (newCapacity > 0)")
            .OpenScope();
        
        if (flags.Length > 0)
            writer.AppendLine("var byteSize = Bitwise.ComputeByteSize(ElementSize, newCapacity, FlagCount);")
                .AppendLine("CollectionUtils.CheckByteSizeInRange(byteSize, int.MaxValue);")
                .AppendLine("newDataPtr = _allocator.Allocate((int)byteSize, Alignment, 1);");
        else
            writer.AppendLine("newDataPtr = _allocator.Allocate(ElementSize, Alignment, newCapacity);");

        writer.AppendLine()
            .AppendLine("if (Capacity > 0)")
            .OpenScope()
            .AppendLine("var itemsToCopy = math.min(newCapacity, _capacity);");

        // ResizeExact: Copy fields
        if (fields.Length > 0)
        {
            field = fields[0];
            writer.AppendLine()
                .Append("// Copying ").AppendLine(field.Path)
                .AppendLine("void* dstPtr = newDataPtr;")
                .AppendLine("void* srcPtr = _dataPtr;")
                .Append("UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)itemsToCopy * ").Append(field.Size).AppendLine(");");

            for (int i = 1; i < fields.Length; i++)
            {
                field = fields[i];
                var fieldOffsetName = fieldOffsetNames[i - 1];

                writer.AppendLine()
                    .Append("// Copying ").AppendLine(field.Path)
                    .Append("dstPtr = (byte*)newDataPtr + (long)newCapacity * ").Append(fieldOffsetName).EndLine()
                    .Append("srcPtr = (byte*)_dataPtr + (long)_capacity * ").Append(fieldOffsetName).EndLine()
                    .Append("UnsafeUtility.MemCpy(dstPtr, srcPtr, (long)itemsToCopy * ").Append(field.Size).AppendLine(");");
            }
        }

        // ResizeExact: Copy flags
        if (flags.Length > 0)
        {
            flag = flags[0];
            writer.AppendLine()
                .Append("// Copying ").AppendLine(flag.Path)
                .Append("byte* dstBlockPtr = (byte*)newDataPtr").AppendLine(fields.Length == 0 ? ";" : " + Bitwise.AlignUp((long)newCapacity * ElementSize, 8);")
                .Append("byte* srcBlockPtr = (byte*)_dataPtr").AppendLine(fields.Length == 0 ? ";" : " + Bitwise.AlignUp((long)_capacity * ElementSize, 8);")
                .AppendLine("var copySize = Bitwise.GetBlockCount(itemsToCopy);")
                .AppendLine("UnsafeUtility.MemCpy(dstBlockPtr, srcBlockPtr, copySize);");

            if (flags.Length > 1)
                writer.AppendLine()
                    .AppendLine("var dstBlockSize = Bitwise.GetBlockCount(newCapacity);")
                    .AppendLine("var srcBlockSize = Bitwise.GetBlockCount(_capacity);");

            for (int i = 1; i < flags.Length; i++)
            {
                flag = flags[i];
                
                writer.AppendLine()
                    .Append("// Copying ").AppendLine(flag.Path)
                    .AppendLine("dstBlockPtr += dstBlockSize;")
                    .AppendLine("srcBlockPtr += srcBlockSize;")
                    .AppendLine("UnsafeUtility.MemCpy(dstBlockPtr, srcBlockPtr, copySize);");
            }
        }

        writer.CloseScope()
            .CloseScope()
            .AppendLine()
            .AppendLine("if (CollectionUtils.ShouldDeallocate(allocator.Handle))").IncrementIndent()
            .AppendLine("AllocatorManager.Free(allocator.Handle, _dataPtr);").DecrementIndent()
            .AppendLine()
            .AppendLine("_dataPtr = newDataPtr;")
            .AppendLine("_capacity = newCapacity;")
            .CloseScope();
        
        // Equality methods
        writer.AppendLine()
            .Append("public bool Equals(").Append(unsafeStructName).AppendLine(" other)")
            .OpenScope()
            .AppendLine("return _dataPtr == other._dataPtr && _capacity == other._capacity;")
            .CloseScope()
            .AppendLine()
            .AppendLine("[BurstDiscard]")
            .AppendLine("public override bool Equals(object obj)")
            .OpenScope()
            .Append("return obj is ").Append(unsafeStructName).AppendLine(" other && Equals(other);")
            .CloseScope()
            .AppendLine()
            .AppendLine("public override int GetHashCode() => (int)_dataPtr * 397 ^ _capacity;")
            .AppendLine()
            .Append("public static bool operator ==(").Append(unsafeStructName).Append(" left, ").Append(unsafeStructName).AppendLine(" right) => left.Equals(right);")
            .Append("public static bool operator !=(").Append(unsafeStructName).Append(" left, ").Append(unsafeStructName).AppendLine(" right) => !left.Equals(right);");
        
        // Dispose method
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Releases all resources (memory).")
            .AppendLine("/// </summary>")
            .AppendLine("public void Dispose()")
            .OpenScope()
            .AppendLine("if (IsCreated == false)").IncrementIndent()
            .AppendLine("return;").DecrementIndent()
            .AppendLine()
            .AppendLine("if (CollectionUtils.ShouldDeallocate(Allocator))")
            .OpenScope()
            .AppendLine("AllocatorManager.Free(Allocator, DataPtr);")
            .AppendLine("_allocator = AllocatorManager.Invalid;")
            .CloseScope()
            .AppendLine()
            .AppendLine("_dataPtr = null;")
            .AppendLine("_capacity = 0;")
            .CloseScope();
        
        // Dispose method (Native)
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Creates and schedules a job that frees the memory of this data structure.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"inputDeps\">The dependency for the new job.</param>")
            .AppendLine("/// <returns>The handle of the new job. The job depends upon `inputDeps` and frees the memory of this data structure.</returns>")
            .AppendLine("public JobHandle Dispose(JobHandle inputDeps)")
            .OpenScope()
            .AppendLine("if (IsCreated == false)").IncrementIndent()
            .AppendLine("return inputDeps;").DecrementIndent()
            .AppendLine()
            .AppendLine("if (CollectionUtils.ShouldDeallocate(Allocator))")
            .OpenScope()
            .AppendLine("var jobHandle = new UnsafeDisposeJob")
            .OpenScope()
            .AppendLine("Ptr = _dataPtr,")
            .AppendLine("Allocator = _allocator").DecrementIndent()
            .AppendLine("}.Schedule(inputDeps);")
            .AppendLine()
            .AppendLine("_dataPtr = null;")
            .AppendLine("_capacity = 0;")
            .AppendLine("_allocator = AllocatorManager.Invalid;")
            .AppendLine()
            .AppendLine("return jobHandle;")
            .CloseScope()
            .AppendLine()
            .AppendLine("_dataPtr = null;")
            .AppendLine("_capacity = 0;")
            .AppendLine()
            .AppendLine("return inputDeps;")
            .CloseScope();
        
        // Create method
        writer.AppendLine()
            .Append("internal static ").Append(unsafeStructName).AppendLine("* Create<U>(int initialCapacity, ref U allocator, NativeArrayOptions options)").IncrementIndent()
            .AppendLine("where U : unmanaged, AllocatorManager.IAllocator").DecrementIndent()
            .OpenScope()
            .Append("var sizeOf = UnsafeUtility.SizeOf<").Append(unsafeStructName).AppendLine(">();")
            .Append("var alignOf = UnsafeUtility.AlignOf<").Append(unsafeStructName).AppendLine(">();")
            .Append("var containerPtr = (").Append(unsafeStructName).AppendLine("*)allocator.Allocate(sizeOf, alignOf);")
            .Append("*containerPtr = new ").Append(unsafeStructName).AppendLine("(initialCapacity, allocator.Handle, options);")
            .AppendLine("return containerPtr;")
            .CloseScope();
        
        #endregion
        
        // Struct and namespace end
        writer.CloseScope();
        
        if (hasNamespace)
            writer.CloseScope();
        
        context.AddSource($"{unsafeStructName}.g.cs", writer.ToSourceText(Encoding.UTF8));

        #endregion // Unsafe Generation

        if (metadata.GenerateNativeContainer == false)
            return;

        #region Native Generation

        var nativeStructName = "Native" + metadata.Name;
        
        writer.Clear();
        
        writer.AppendLine(
                """
                //┌────────────────────────────────────────────────────────────────────────────────┐
                //│ <auto-generated>                                                               │
                //│     This code was generated by a tool.                                         │
                //│     (https://github.com/Saesentsessis/Unity-SoA-Generator)                     │
                //│                                                                                │
                //│     Changes to this file may cause incorrect behavior and will be lost if the  │
                //│     code is regenerated.                                                       │
                //│ </auto-generated>                                                              │
                //└────────────────────────────────────────────────────────────────────────────────┘
                using System;
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;
                using System.Diagnostics;
                using Unity.Burst;
                using Unity.Collections;
                using Unity.Collections.Specialized;
                using Unity.Collections.LowLevel.Unsafe;
                using Unity.Mathematics;
                using Unity.Jobs;
                using Saesentsessis.DOD.SoA;
                """)
            .AppendLine();

        if (hasNamespace)
            writer.Append("namespace ").AppendLine(metadata.Namespace)
                .OpenScope();
        
        writer.AppendLine("[StructLayout(LayoutKind.Sequential)]")
            .AppendLine("[NativeContainer]")
            .AppendLine("[DebuggerDisplay(\"Length = {_containerPtr == null ? default : _containerPtr->Length}, Capacity = {_containerPtr == null ? default : _containerPtr->Capacity}\")]")
            .Append("public unsafe partial struct ").Append(nativeStructName).AppendLine(" : IStructureOfArrays,").IncrementIndent()
            .AppendLine("IDisposable,")
            .AppendLine("INativeDisposable,")
            .Append("IEquatable<").Append(nativeStructName).AppendLine(">").DecrementIndent()
            .OpenScope();

        #region Fields
        
        #region Instance

        writer.AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("// State field to track the safety handle lifecycle. Should be called")
            .AppendLine("// exactly \"m_Safety\", as Unity searches this field by name.")
            .AppendLine("internal AtomicSafetyHandle m_Safety;")
            .Append("internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<").Append(nativeStructName).AppendLine(">();")
            .AppendLineRaw("#endif")
            .AppendLine()
            .AppendLine("[NativeDisableUnsafePtrRestriction]")
            .Append("private ").Append(unsafeStructName).AppendLine("* _containerPtr;");
        
        #endregion // Instance
        
        #endregion // Fields

        #region Constructors

        writer.AppendLine()
            .AppendLine("/// <summary>")
            .Append("/// Initializes and returns an instance of ").Append(nativeStructName).AppendLine(".")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"initialCapacity\">The initial capacity of the container.</param>")
            .AppendLine("/// <param name=\"allocator\">The allocator to use.</param>")
            .AppendLine("/// <param name=\"options\">Whether newly allocated bytes should be zeroed out.</param>")
            .Append("public ").Append(nativeStructName).AppendLine("(int initialCapacity, AllocatorManager.AllocatorHandle allocator,").IncrementIndent()
            .AppendLine("NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)").DecrementIndent()
            .OpenScope()
            .Append("_containerPtr = ").Append(unsafeStructName).AppendLine(".Create(initialCapacity, ref allocator, options);")
            .AppendLine()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("CollectionUtils.CheckAllocator(allocator.Handle);")
            .AppendLine()
            .AppendLine("m_Safety = AtomicSafetyHandle.Create();")
            .AppendLine("AtomicSafetyHandle.SetAllowReadOrWriteAccess(m_Safety, true);")
            .AppendLineRaw("#endif")
            .CloseScope();

        #endregion // Constructors

        #region Properties

        #region Shared
        
        // IStructureOfArrays.ElementSize and IStructureOfArrays.FlagCount properties
        writer.AppendLine()
            .Append("int IStructureOfArrays.ElementSize => CollectionUtils.AssumePositive(").Append(unsafeStructName).AppendLine(".ElementSize);")
            .Append("int IStructureOfArrays.FlagCount => CollectionUtils.AssumePositive(").Append(unsafeStructName).AppendLine(".FlagCount);");
        
        // Data property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Dense storage for SoA fields data.")
            .AppendLine("/// </summary>")
            .AppendLine("public void* DataPtr")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => _containerPtr->DataPtr;")
            .CloseScope();
        
        // Capacity property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The number of elements that can fit in the internal buffer.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <value>The number of elements that can fit in the internal buffer.</value>")
            .AppendLine("public int Capacity")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("readonly get")
            .OpenScope()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("AtomicSafetyHandle.CheckReadAndThrow(m_Safety);")
            .AppendLineRaw("#endif")
            .AppendLine("return CollectionUtils.AssumePositive(_containerPtr->Capacity);")
            .CloseScope()
            .AppendLine("set")
            .OpenScope()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);")
            .AppendLineRaw("#endif")
            .AppendLine("_containerPtr->SetCapacity(value);")
            .CloseScope()
            .CloseScope();
        
        // Allocator property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The allocator used to create the internal buffer.")
            .AppendLine("/// </summary>")
            .AppendLine("public AllocatorManager.AllocatorHandle Allocator")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => IsCreated ? _containerPtr->Allocator : AllocatorManager.Invalid;")
            .CloseScope();
        
        // IsCreated property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Whether this list has been allocated (and not yet deallocated).")
            .AppendLine("/// </summary>")
            .AppendLine("/// <value>True if this list has been allocated (and not yet deallocated).</value>")
            .AppendLine("public readonly bool IsCreated")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get => _containerPtr != null;")
            .CloseScope();
        
        // ByteSize property
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// The size of an internal buffer in bytes (e.g. amount of allocated memory).")
            .AppendLine("/// </summary>")
            .AppendLine("public readonly long ByteSize")
            .OpenScope()
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLine("get")
            .OpenScope()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("AtomicSafetyHandle.CheckReadAndThrow(m_Safety);")
            .AppendLineRaw("#endif")
            .AppendLine("return CollectionUtils.AssumePositive(_containerPtr->ByteSize);")
            .CloseScope()
            .CloseScope();

        #endregion // Shared

        #region Generated

        // Fields
        for (int i = 0; i < fields.Length; i++)
        {
            field = fields[i];
            var fieldPtrName = fieldPtrNames[i];

            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// NativeArray accessor for ").Append(field.Path).AppendLine(".")
                .AppendLine("/// </summary>")
                .Append("public unsafe NativeArray<").Append(field.TypeName).Append("> ").AppendLine(field.Path)
                .OpenScope()
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLine("get")
                .OpenScope()
                .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
                .AppendLine("AtomicSafetyHandle.CheckReadAndThrow(m_Safety);")
                .AppendLineRaw("#endif")
                .Append("var fieldPtr = _containerPtr->").Append(fieldPtrName).AppendLine(";")
                .AppendLine("var capacity = _containerPtr->Capacity;")
                .Append("var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<").Append(field.TypeName).AppendLine(">").IncrementIndent()
                .AppendLine("(fieldPtr, capacity, Unity.Collections.Allocator.None);").DecrementIndent()
                .AppendLine()
                .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
                .AppendLine("NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, m_Safety);")
                .AppendLineRaw("#endif")
                .AppendLine("return result;")
                .CloseScope()
                .CloseScope();
        }

        // Flags
        for (int i = 0; i < flags.Length; i++)
        {
            flag = flags[i];
            var flagBitArrayName = flagBitArrayNames[i];
            
            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// Flag accessor for ").Append(flag.Path).AppendLine(" as bit array.")
                .AppendLine("/// </summary>")
                .Append("public unsafe NativeBitArray ").AppendLine(flagBitArrayName)
                .OpenScope()
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLine("get")
                .OpenScope()
                .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
                .AppendLine("AtomicSafetyHandle.CheckReadAndThrow(m_Safety);")
                .AppendLineRaw("#endif")
                .Append("var unsafeResult = _containerPtr->").Append(flagBitArrayName).AppendLine(";")
                .AppendLine("var result = NativeBitArrayUnsafeUtility.ConvertExistingDataToNativeBitArray(unsafeResult.Ptr, unsafeResult.Capacity, AllocatorManager.None);")
                .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
                .AppendLine("NativeBitArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, m_Safety);")
                .AppendLineRaw("#endif")
                .AppendLine("return result;")
                .CloseScope()
                .CloseScope();
        }

        #endregion // Generated
        
        #region Uniform Accessors
        
        foreach (var accessor in accessors)
        {
            writer.AppendLine()
                .AppendLine("/// <summary>")
                .Append("/// An accessor of a flattened property of type ").Append(accessor.TypeName).AppendLine(".")
                .AppendLine("/// </summary>")
                .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .Append("public ").Append(accessor.TypeName).Append(" Get").Append(accessor.Name).AppendLine("(int index)")
                .OpenScope()
                .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
                .AppendLine("AtomicSafetyHandle.CheckReadAndThrow(m_Safety);")
                .AppendLineRaw("#endif")
                .Append("return _containerPtr->Get").Append(accessor.Name).AppendLine("(index);")
                .CloseScope();
        }

        #endregion // Uniform Accessors

        #endregion // Properties

        #region Methods

        // SetCapacity method
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Sets the capacity.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"capacity\">The new capacity.</param>")
            .AppendLine("public void SetCapacity(int capacity)").IncrementIndent()
            .AppendLine("=> Capacity = capacity;").DecrementIndent();
        
        // Equality methods
        writer.AppendLine()
            .Append("public bool Equals(").Append(nativeStructName).AppendLine(" other)")
            .OpenScope()
            .AppendLine("return _containerPtr == other._containerPtr;")
            .CloseScope()
            .AppendLine()
            .AppendLine("[BurstDiscard]")
            .AppendLine("public override bool Equals(object obj)")
            .OpenScope()
            .Append("return obj is ").Append(nativeStructName).AppendLine(" other && Equals(other);")
            .CloseScope()
            .AppendLine()
            .AppendLine("public override int GetHashCode() => (int)_containerPtr * 397 ^ Capacity;")
            .AppendLine()
            .Append("public static bool operator ==(").Append(nativeStructName).Append(" left, ").Append(nativeStructName).AppendLine(" right) => left.Equals(right);")
            .Append("public static bool operator !=(").Append(nativeStructName).Append(" left, ").Append(nativeStructName).AppendLine(" right) => !left.Equals(right);");

        // Dispose method
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Releases all resources (memory and safety handles).")
            .AppendLine("/// </summary>")
            .AppendLine("public void Dispose()")
            .OpenScope()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("if (AtomicSafetyHandle.IsDefaultValue(m_Safety) == false)").IncrementIndent()
            .AppendLine("AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);").DecrementIndent()
            .AppendLineRaw("#endif")
            .AppendLine("if (IsCreated == false)").IncrementIndent()
            .AppendLine("return;").DecrementIndent()
            .AppendLine()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLineRaw("#if UNITY_6000_0_OR_NEWER")
            .AppendLine("CollectionHelper.DisposeSafetyHandle(ref m_Safety);")
            .AppendLineRaw("#else")
            .AppendLine("CollectionUtils.DisposeSafetyHandle(ref m_Safety);")
            .AppendLineRaw("#endif // UNITY_6000_0_OR_NEWER")
            .AppendLineRaw("#endif // ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("_containerPtr->Dispose();")
            .AppendLine("_containerPtr = null;")
            .CloseScope();
        
        // Dispose method (Native)
        writer.AppendLine()
            .AppendLine("/// <summary>")
            .AppendLine("/// Creates and schedules a job that releases all resources (memory and safety handles) of this collection.")
            .AppendLine("/// </summary>")
            .AppendLine("/// <param name=\"inputDeps\">The dependency for the new job.</param>")
            .AppendLine("/// <returns>The handle of the new job. The job depends upon `inputDeps` and releases all resources (memory and safety handles) of this collection.</returns>")
            .AppendLine("public JobHandle Dispose(JobHandle inputDeps)")
            .OpenScope()
            .AppendLineRaw("#if ENABLE_UNITY_COLLECTIONS_CHECKS")
            .AppendLine("if (AtomicSafetyHandle.IsDefaultValue(m_Safety) == false)").IncrementIndent()
            .AppendLine("AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);").DecrementIndent()
            .AppendLineRaw("#endif")
            .AppendLine("if (IsCreated == false)").IncrementIndent()
            .AppendLine("return inputDeps;").DecrementIndent()
            .AppendLine()
            .AppendLine("var handle = _containerPtr->Dispose(inputDeps);")
            .AppendLine("_containerPtr = null;")
            .AppendLine()
            .AppendLine("return handle;")
            .CloseScope();
        
        #endregion // Methods
        
        writer.CloseScope();

        if (hasNamespace)
            writer.CloseScope();
        
        context.AddSource($"{nativeStructName}.g.cs", writer.ToSourceText(Encoding.UTF8));

        #endregion // Native Generation
    }
    
    private static void WriteNodes(CodeBuilder writer, IReadOnlyList<AccessorField> nodes)
    {
        if (nodes.Count == 0)
            return;

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];

            if (node.HasChildren)
            {
                WriteNodes(writer, node.Children);
                continue;
            }
            
            if (node.RootByteOffset == 0)
            {
                writer.Append("*(").Append(node.TypeName).Append("*)resultPtr = ")
                    .Append(node.LeafPointerName).AppendLine(node.IsFlag ? ".IsSet(index);" : "[index];");

                continue;
            }
            
            writer.Append("*(").Append(node.TypeName).Append("*)(resultPtr + ").Append(node.RootByteOffset).Append(") = ")
                .Append(node.LeafPointerName).AppendLine(node.IsFlag ? ".IsSet(index);" : "[index];");
        }
    }
    
    [Flags]
    private enum GenerationParams
    {
        AllowFlattening = 1 << 0,
        AllowBitPacking = 1 << 1,
    }

    private readonly struct GenerationParamsView
    {
        private readonly GenerationParams _value;

        public GenerationParamsView(GenerationParams value)
        {
            _value = value;
        }

        public bool AllowFlattening => (_value & GenerationParams.AllowFlattening) != 0;
        public bool AllowBitPacking => (_value & GenerationParams.AllowBitPacking) != 0;
    }
}