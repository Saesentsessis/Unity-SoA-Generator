using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace StructureOfArraysGenerator.Tests;

public class GeneratorTests
{
	private const string AttributesSource = 
		"""
		using System;

		namespace Saesentsessis.DOD.SoA 
		{
		    [AttributeUsage(AttributeTargets.Struct)]
		    public class GenerateSoAAttribute : Attribute
		    {
		        public bool AllowFieldHierarchyFlattening { get; }
		        public bool AllowBooleanBitPacking { get; }
		        public string StructName { get; set; } = string.Empty;
		        public string StructNamespace { get; set; } = string.Empty;
		        public bool GenerateNativeContainer { get; set; } = false;

		        public GenerateSoAAttribute(bool allowFieldHierarchyFlattening = true, bool allowBooleanBitPacking = true)
		        {
		            AllowFieldHierarchyFlattening = allowFieldHierarchyFlattening;
		            AllowBooleanBitPacking = allowBooleanBitPacking;
		        }
		    }
		    
		    [AttributeUsage(AttributeTargets.Field)]
		    public class GenerateSoAUniformAccessorAttribute : Attribute {}
		}
		""";
	
	[Fact]
	public async Task CustomNameAndNativeContainer_GeneratesCorrectly()
	{
		var testCode = AttributesSource + """
		                                  [Saesentsessis.DOD.SoA.GenerateSoA(StructName = "EntitySoA", StructNamespace = "Game.Simulation", GenerateNativeContainer = true)]
		                                  public struct TestData { public int Health; }
		                                  """;

		var expectedUnsafe = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "CustomNameAndNative_Unsafe_Generated.txt"));
		var expectedNative = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "CustomNameAndNative_Native_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeEntitySoA.g.cs", expectedUnsafe),
					(typeof(SoAGenerator), "NativeEntitySoA.g.cs", expectedNative)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task BitPacking_GeneratesBitArrays()
	{
		var testCode = AttributesSource + """
		                                  [Saesentsessis.DOD.SoA.GenerateSoA(allowFieldHierarchyFlattening: true, allowBooleanBitPacking: true)]
		                                  public struct TestData { public int Id; public bool IsActive; }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "BitPacking_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeTestDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task FlatteningDisabled_KeepsNestedStructIntact()
	{
		var testCode = AttributesSource + """
		                                  using System.Runtime.InteropServices;

		                                  [StructLayout(LayoutKind.Sequential)]
		                                  public struct Nested { public byte A; public int B; } // Contains padding

		                                  [Saesentsessis.DOD.SoA.GenerateSoA(allowFieldHierarchyFlattening: false, allowBooleanBitPacking: true)]
		                                  public struct TestData { public Nested Child; public float C; }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "FlatteningDisabled_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeTestDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task UniformAccessor_GeneratesGetterMethod()
	{
		var testCode = AttributesSource + """
		                                  using System.Runtime.InteropServices;

		                                  [StructLayout(LayoutKind.Sequential)]
		                                  public struct Nested { public byte A; public int B; }

		                                  [Saesentsessis.DOD.SoA.GenerateSoA]
		                                  public struct TestData 
		                                  { 
		                                      [Saesentsessis.DOD.SoA.GenerateSoAUniformAccessor]
		                                      public Nested Child; 
		                                  }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "UniformAccessor_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeTestDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task ValidStruct_GeneratesSoA()
	{
		var testCode = AttributesSource + """
		                                  [Saesentsessis.DOD.SoA.GenerateSoA]
		                                  public partial struct TestData { public byte a; public float b; public ushort c; public int d; }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(),
			"ExpectedResults", "ValidStruct_Generated.txt"));
			
		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeTestDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}

	[Fact]
	public async Task NestedStruct_GeneratesSoA()
	{
		var testCode = AttributesSource + """
		                                  using System.Runtime.InteropServices;
		                                  
		                                  public struct Nested { public byte Byte; public short Short; }

		                                  [Saesentsessis.DOD.SoA.GenerateSoA]
		                                  public struct TestData { private Nested Nested; public int Integer; }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(),
			"ExpectedResults", "NestedStruct_Generated.txt"));
		
		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeTestDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task StaticAndConstFields_AreIgnoredByGenerator()
	{
		// Ensures the generator omits static and const fields from the SoA layout.
		var testCode = AttributesSource + """
		                                  [Saesentsessis.DOD.SoA.GenerateSoA]
		                                  public struct DataWithStatics 
		                                  { 
		                                      public const int MaxHealth = 100;
		                                      public static readonly float Multiplier = 2.5f;
		                                      public short ValidField; 
		                                  }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "IgnoredFields_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeDataWithStaticsSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
	
	[Fact]
	public async Task BitPackingDisabled_TreatsBooleanAsStandardField()
	{
		// Tests that booleans generate standard pointers rather than UnsafeBitArray 
		// when allowBooleanBitPacking is set to false.
		var testCode = AttributesSource + """
		                                  [Saesentsessis.DOD.SoA.GenerateSoA(allowBooleanBitPacking: false)]
		                                  public struct StandardBoolData 
		                                  { 
		                                      public bool IsActive; 
		                                      public int Id; 
		                                  }
		                                  """;

		var expectedCode = await File.ReadAllTextAsync(Path.Combine(TestHelper.GetSourceDirectory(), "ExpectedResults", "NoBitPacking_Generated.txt"));

		var test = new SoAGeneratorTest
		{
			TestCode = testCode,
			CompilerDiagnostics = CompilerDiagnostics.None,
			TestState =
			{
				GeneratedSources =
				{
					(typeof(SoAGenerator), "UnsafeStandardBoolDataSoA.g.cs", expectedCode)
				}
			}
		};

		await test.RunAsync();
	}
		
	private class SoAGeneratorTest : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, XUnitVerifier>
	{
		protected override IEnumerable<ISourceGenerator> GetSourceGenerators()
		{
			yield return new SoAGenerator().AsSourceGenerator();
		}
	}
}