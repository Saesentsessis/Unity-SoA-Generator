using System.Text;

namespace StructureOfArraysGenerator;

public static class StringBuilderExtensions
{
	public static StringBuilder AppendIndent(this StringBuilder builder, int indentLevel)
	{
		while (indentLevel > 0)
		{
			builder.Append('\t');
			indentLevel--;
		}
        
		return builder;
	}
}