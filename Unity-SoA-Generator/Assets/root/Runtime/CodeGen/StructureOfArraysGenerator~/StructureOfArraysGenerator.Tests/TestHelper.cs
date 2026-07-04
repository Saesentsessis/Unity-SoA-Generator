using System.IO;
using System.Runtime.CompilerServices;

namespace StructureOfArraysGenerator.Tests;

public class TestHelper
{
	/// <summary>
	/// Injects the absolute file path of the calling .cs file at compile time,
	/// then returns the directory containing that file.
	/// </summary>
	public static string GetSourceDirectory([CallerFilePath] string callerFilePath = "")
	{
		return Path.GetDirectoryName(callerFilePath) ?? string.Empty;
	}
}