using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace StructureOfArraysGenerator;

internal class CodeBuilder(StringBuilder builder)
{
	private bool _isNewLine = true;

	private int IndentLevel { get; set; }

	public CodeBuilder IncrementIndent()
	{
		IndentLevel++;
		return this;
	}

	public CodeBuilder DecrementIndent()
	{
		IndentLevel--;
		return this;
	}
    
	public CodeBuilder Append(string value)
	{
		if (_isNewLine)
		{
			builder.AppendIndent(IndentLevel);
			_isNewLine = false;
		}

		builder.Append(value);
		return this;
	}
    
	public CodeBuilder Append(bool value)
	{
		if (_isNewLine)
		{
			builder.AppendIndent(IndentLevel);
			_isNewLine = false;
		}

		builder.Append(value);
		return this;
	}

	public CodeBuilder Append(char value)
	{
		if (_isNewLine)
		{
			builder.AppendIndent(IndentLevel);
			_isNewLine = false;
		}

		builder.Append(value);
		return this;
	}
    
	public CodeBuilder Append(int value)
	{
		if (_isNewLine)
		{
			builder.AppendIndent(IndentLevel);
			_isNewLine = false;
		}

		builder.Append(value);
		return this;
	}

	public CodeBuilder AppendLine()
	{
		builder.AppendLine();
		_isNewLine = true;
		return this;
	}
    
	public CodeBuilder AppendLine(string value)
	{
		if (_isNewLine)
			builder.AppendIndent(IndentLevel);
		
		builder.AppendLine(value);
		_isNewLine = true;
		return this;
	}

	public CodeBuilder AppendLineRaw(string value)
	{
		builder.AppendLine(value);
		_isNewLine = true;
		return this;
	}
	
	public CodeBuilder EndLine()
	{
		builder.AppendLine(";");
		_isNewLine = true;
		return this;
	}

	public CodeBuilder OpenScope()
	{
		builder.AppendIndent(IndentLevel++).AppendLine("{");
		_isNewLine = true;
		return this;
	}

	public CodeBuilder CloseScope()
	{
		builder.AppendIndent(--IndentLevel).AppendLine("}");
		_isNewLine = true;
		return this;
	}

	public CodeBuilder Clear()
	{
		builder.Length = 0;
		_isNewLine = true;
		return this;
	}

	public override string ToString()
	{
		return builder.ToString();
	}

	public SourceText ToSourceText(Encoding encoding)
	{
		return SourceText.From(ToString(), encoding);
	}
}