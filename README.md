# [Unity HPC# SoA Source Generator](https://github.com/Saesentsessis/Unity-SoA-Generator)

A high-performance C# Source Generator designed exclusively for Unity's Burst Compiler
and Job System. It automatically transforms standard C# structs into purely unmanaged,
tightly packed Structure of Arrays (SoA) containers.

Zero allocations. Zero GC pressure. 100% Burst compatible.

## The Problem & The Solution

In Data-Oriented Design (DOD), Array of Structs (AoS) layouts cause massive CPU
cache-miss penalties during linear iteration because irrelevant data is pulled into
the L1 cache. Structure of Arrays (SoA) solves this by separating fields into contiguous
parallel arrays, maximizing the hardware prefetcher.

Writing and maintaining SoA containers manually is tedious, error-prone, and destroys
code readability.

**This generator automates the entire process.** By simply tagging a struct with
`[GenerateSoA]` attribute, the generator evaluates memory layout at compile time
and emits a highly optimized, Burst-ready native container, that handles dynamic
resizing, custom cache-line alignment, and unmanaged memory allocation.

## Core Features

- **Burst-First Architecture:** Generated containers use raw pointers, `UnsafeUtils.MemCpy`,
and mathematical bit-shifting. No managed arrays, no spans, no overhead.
- **Dual-Block Vertical Bit-Packing:** Booleans are automatically stripped from the
primitive data block and packed into 64-bit aligned native bit arrays at the tail of the
allocation. Saves up to 87.5% of memory per boolean with zero multithreading race conditions.
- **Deep Hierarchy Flattening:** Automatically decomposes nested structs, calculates
their absolute byte offsets, and reconstructs them on the stack.
- **Access Modifier Bypass:** Reconstructs structs containing private or readonly fields
without requiring custom constructors or `partial` keyword.
- **Job System Integration:** Natively implements `INativeDisposable` for seamless
integration into `JobHandle` dependency chains.
- **Safe Native Container:** Optionally generates a safe `NativeContainer` that replaces raw
`*` pointers with `NativeArray<T>` and `NativeBitArray`, protected by Unity's safety checks. 

## Unity-SoA vs. Cysharp's SoA Generator

Cysharp makes incredible tools, and their [StructureOfArraysGenerator](https://github.com/Cysharp/StructureOfArraysGenerator)
is the gold standard for general .NET applications. However, standard .NET memory management
is incompatible with Unity's High-Performance C# (HPC#) ecosystem.

This generator is built explicitly from the ground up for **Unity.**

| Feature                 | General .NET SoA               | This Generator                                  |
|-------------------------|--------------------------------|-------------------------------------------------|
| **Memory Allocation**   | Array Pooling/`Memory<T>`      | `AllocatorManager`(Temp, TempJob, Persistent)   |
| **Burst Compatibility** | Limited(`Span` restrictions)   | 100% Native (Raw `void*` and `UnsafeUtility`)   |
| **Boolean Packing**     | 1 byte per `bool`(87.5% waste) | **Dual-Block Bit Arrays** (Perfect density)     |
| **Multithreading**      | Standard C# Threading          | Unity Job System Safety(`AtomicSafetyHandle`)   |
| **Cache Alignment**     | CLR Default                    | Explicit Cache-Line padding                     |
| **Disposal**            | Garbage Collected (GC)         | Native `Dispose(JobHandle)` dependency tracking |

## Requirements

- Unity **2021.3** or newer
- [`com.saesentsessis.unity-collections-specialized`](https://github.com/Saesentsessis/Unity-Collections-Specialized) **0.1.1** or newer
- [`com.unity.collections`](https://docs.unity3d.com/Packages/com.unity.collections@latest) **2.1.4** or newer
- [`com.unity.burst`](https://docs.unity3d.com/Packages/com.unity.burst@latest) **1.8.0** or newer

## Installation

### Method 1: OpenUPM (Recommended)

You can install this package via the [OpenUPM](https://openupm.com/) CLI:

```bash
openupm add com.saesentsessis.unity-collections-specialized
```

Or manually add the scoped registry to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.saesentsessis.unity-collections-specialized": "0.1.1",
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.saesentsessis"
      ]
    }
  ]
}
```

### Method 2: Unity package installer

1. Download the latest `.unitypackage` from [GitHub Releases page](https://github.com/Saesentsessis/Unity-SoA-Generator/releases).
   - _Direct Link:_ [Unity-Collections-Specialized-Installer.unitypackage](https://github.com/Saesentsessis/Unity-SoA-Generator/releases/download/1.0.0/Unity-SoA-Generator-Installer.unitypackage)
2. Import the downloaded package into your Unity project.
3. The installer will automatically configure OpenUPM in your `manifest.json` file and install the package dependencies.

### Method 3: Manual installation

1. Open Unity and navigate to `Window` > `Package Manager`.
2. Click on the `+` icon in the top left corner and select `Add package from git URL...`.
3. Enter the following URL:
```
https://github.com/Saesentsessis/Unity-SoA-Generator.git?path=Unity-SoA-Generator/Assets/root
```
4. Click Add.
5. Repeat all steps for the dependent repository:
```
https://github.com/Saesentsessis/Unity-Collections-Specialized.git?path=Unity-Collections-Specialized/Assets/root
```

You can specify exact release version of this package like this:

```
https://github.com/Saesentsessis/Unity-SoA-Generator.git?path=Unity-SoA-Generator/Assets/root#1.0.0
```

## Quick Start

Define your standard data structure and decorate it with `[GenerateSoA]` attribute.

```C#
using Saesentsessis.DOD.SoA.CodeGen;
using Unity.Mathematics;

namespace Game.Simulation 
{
    [GenerateSoA(GenerateNativeContainer = true)]
    public struct PointMass
    {
        public float3 Position;
        public float3 PreviousPosition;
        public float3 Velocity;
        
        public float InverseMass;
        public ushort PhysicalMaterialIndex;
        
        public bool IsActive;
        public bool IsPinned;
    }
}
```

Upon compilation, the generator automatically emits two files:
1. `UnsafePointMassSoA.g.cs`: The core unmanaged memory container utilizing raw pointers
(`float3* PositionPtr`, `UnsafeBitArray IsActiveBitArray`).
2. `NativePointMassSoA.g.cs`: A safe, job-ready wrapper utilizing `NativeArray<T>` and
`NativeBitArray`, protected by Unity's collection checks.

## Usage Guide

### Safe Usage(`Native Container`)

Use the generated `Native{Name}SoA` for safe, bounds-checked operations that integrate
cleanly with Unity Jobs.

```C#
using Unity.Collections;
using Unity.Jobs;

// Allocate the safe container using a standard Unity allocator.
var pointMasses = new NativePointMassSoA(10_000, Allocator.TempJob);

// Add data (capacity expands automatically like NativeList)
pointMasses.SetCapacity(50_000);

// Booleans are exposed as Unity NativeBitArray's
bool isPinned = pointMasses.IsPinnedBitArray.IsSet(0);

// Pass parameters directly inside a job as NativeArray<T>'s
GravityJob addGravityJob = new GravityJob 
{
    Velocities = pointMasses.Velocity,
    Gravity = new float3(0f, -9.81f, 0f)
};

// Schedule the job as normal
JobHandle physicsDeps = addGravityJob.Schedule();

// Dispose and attach to JobHandle dependencies
physicsDeps = pointMasses.Dispose(physicsDeps);
```

### Advanced Usage (Raw Pointers)

Use the generated `Unsafe{Name}SoA` for maximum performance and direct pointer manipulation
inside Burst-compiled code.

```C#
using Unity.Collections;
using Unity.Jobs;

// Allocate the unsafe container
var pointMasses = new UnsafePointMassSoA(10_000, Allocator.Temp);
pointMasses.SetCapacity(50_000);

// Access raw, contiguous pointer arrays directly inside Burst code
unsafe
{
    float3* velocityPtr = pointMasses.VelocityPtr;
    float3* velocityEndPtr = velocityPtr + pointMasses.Capacity;
    float3 gravity = new float3(0f, -9.81f, 0f);
    
    while (velocityPtr < velocityEndPtr)
        *velocityPtr++ += gravity;
}

// Booleans are exposed as Unity native UnsafeBitArrays
bool isPinned = pointMasses.IsPinnedBitArray.IsSet(0);

// Don't forget to free memory when your're done using it.
pointMasses.Dispose();
```

## Technical Deep Dive

Understanding how generator manipulates memory is crucial for writing high-performance
code. Here is what happens under the hood.

### 1. Dual-Block Memory Architecture
When `allowBooleandBitPacking` is enabled, the generator abandons standard linear memory
packing. A single `bool` consumes 1 byte in C#, meaning 87.5% of cache line is wasted.

To solve this without introducing memory race conditions between threads, the generator
splits the unmanaged allocation into two distinct blocks:

1. **The Primitive Block:** Contains all standard, tightly packed, data types
(`float3`, `int`, etc.).
2. **The Bit-Array Block:** Padded to the nearest 8-byte boundary, this block packs
booleans consecutively.

If JobA writes to `IsActive` bit array and JobB writes to `IsPinned` for the same entity
concurrently, **they will not race**, because the generator maps them to entirely separate
virtual `UnsafeBitArray` memory slices within the tail block.

### 2. The Stack-Pointer Bypass (AST Flattening)

If your struct contains nested structs or `private` fields, traditional SoA generators
require you to write mapping constructors or mark the struct as `partial`.

This generator builds an internal **Abstract Syntax Tree (AST)** of your struct's memory
layout at compile-time, matching the CLR's exact alignment rules. When you call
`GetPointMass(index)` accessor, it bypasses access modifiers entirely:

```C#
// Example of generated accessor code
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public PointMass GetPointMass(int index)
{
    PointMass result = default;
    byte* resultPtr = (byte*)&result;

    // Writes directly to the stack-allocated memory via calculated byte offsets
    *(float3*)resultPtr = PositionPtr[index];
    *(float3*)(resultPtr + 12) = PreviousPositionPtr[index];
    *(float3*)(resultPtr + 24) = VelocityPtr[index];
    *(float*)(resultPtr + 36) = InverseMassPtr[index];
    *(ushort*)(resultPtr + 40) = PhysicalMaterialIndexPtr[index];
    
    // Bit arrays are evaluated and cast directly into the stack struct
    *(bool*)(resultPtr + 42) = IsActiveBitArray.IsSet(index);
    *(bool*)(resultPtr + 43) = IsPinnedBitArray.IsSet(index);

    return result;
}
```

Because the result struct is instantly overwritten, Burst optimizes away the `default`
initialization entirely. The result is pure CPU register moves.

### 3. Allocation Limits

Due to how the generator intercepts Unity's `AllocatorManager`, bit-packed containers
safely bypass standard element-size multiplication constraints.
- Containers consisting **only of primitives** can exceed the 2GB memory address space limit
safely.
- Containers utilizing **bit-packing** enforce a strict 2GB overall allocation limit via a
`CheckByteSizeInRange` guard to prevent integer overflow during block size calculations.

## API & Configuration

### `[GenerateSoA]`

The primary attribute used to mark a struct for SoA generation. It exposes a several
configuration parameters to dictate the underlying memory architecture.
- `allowFieldHierarchyFlattening` _(default: true)_<br>
If set to `true`, the generator recursively decomposes nested structs whose size-to-alignment
ratio would introduce padding. This guarantees 100% memory density by replacing the nested
struct's single array accessor with separate, parallel arrays for each constituent primitive
field.
- `allowBooleanBitPacking` _(default: true)_<br>
Transforms `bool` fields into a separate vertically packed bit array block, located after
all primitive fields. This may introduce a slight padding gap to align the bit block to
an 8-byte boundary, but eliminates per-boolean byte waste.
- `StructName` _(default: null)_<br>
Overrides the generated struct identifier. If null, defaults to `Unsafe{TargetName}SoA`,
`Native{TargetName}SoA`.
- `StructNamespace` _(default: null)_<br>
Overrides the target namespace. If null, generates into the exact same namespace as the
target struct.
- `GenerateNativeContainer` _(default: false)_<br>
Instructs the generator to emit the safe `[NativeContainer]` wrapper structure alongside
the internal unsafe implementation.

### `[GenerateSoAUniformAccessor]`

When applied to a nested struct field inside a generated SoA container, this attribute
instructs the generator to create a convenience getter method that reconstructs the target
struct on-the-fly for a given index.

> Note, that accessors are only generated if the structure was flattened.

```C#
[StructLayout(LayoutKind.Sequential)]
public struct TransformData { public float X; public short Y; }

[GenerateSoA]
public struct SimulationData
{
    [GenerateSoAUniformAccessor]
    public TransformData Transform;
}
```

This generates `public TransformData GetTransform(int index)` inside the SoA container.

> ⚠️**Performance Warning:** If the target struct was flattened due to alignment optimization
> (`allowFieldHierarchyFlattening = true`), accessing this property requires fetching data
> from multiple disjoint parallel arrays. This breaks cache locality and will incur multiple
> CPU cache line fetches, degrading performance compared to direct, per-field array iteration.
> Use strictly for convenience outside of hot loops.

## Generated API Surface

The generated `Unsafe{Name}SoA` container implements `IStructureOfArrays`, `IDisposable`,
and `INativeDisposable`. Its primary API includes:

- `public void* DataPtr { get; }`: Dense storage pointer for the entire SoA block.
- `public int Capacity { get; set; }`: Adjusts the total element capacity, handling internal
buffer reallocation (`UnsafeUtility.MemCpy`) automatically.
- `public long ByteSize { get; }`: Returns the total allocated memory footprint in bytes.
- **Field Accessors:** Generates strongly-typed pointers for each standard field (e.g.,
`public float3* VelocityPtr { get; }`).
- **Flag Accessors:** If bit-packing is enabled, generates block-aligned `UnsafeBitArray`
properties for each boolean field.

If `GenerateNativeContainer = true` is specified, the `Native{Name}SoA` container mirrors
this API but replaces raw pointers with `NativeArray<T>` and `NativeBitArray`, integrating
directly with `AtomicSafetyHandle` for deterministic race-condition detection in Job System.

## License

Licensed under the [MIT License](LICENSE).
