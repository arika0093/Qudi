using System;
using System.Text;

namespace Qudi.Generator;

/// <summary>
/// Utility class for efficiently building indented strings.
/// </summary>
/// <param name="IndentLevel">Initial indent level (0 or greater).</param>
/// <remarks>Initializes a new instance of the <see cref="IndentedStringBuilder"/> class with an existing StringBuilder.</remarks>
internal class IndentedStringBuilder(StringBuilder stringBuilder, int indentLevel = 0)
{
    /// <summary>Initializes a new instance of the <see cref="IndentedStringBuilder"/> class.</summary>
    public IndentedStringBuilder(int indentLevel = 0)
        : this(new StringBuilder(), indentLevel) { }

    /// <summary>Number of spaces per indent level.</summary>
    public const int IndentSize = 4;

    /// <summary>Current indent level (0 or greater).</summary>
    public int IndentLevel { get; private set; } = indentLevel;

    /// <summary>Returns a string of spaces representing the current indent.</summary>
    public string Indent => new(' ', IndentLevel * IndentSize);

    /// <summary>Returns a new builder with the indent level increased by one.</summary>
    public void IncreaseIndent() => IndentLevel += 1;

    /// <summary>Returns a new builder with the indent level decreased by one (not below zero).</summary>
    public void DecreaseIndent() => IndentLevel = Math.Max(0, IndentLevel - 1);

    /// <summary>
    /// Generates a new IndentedStringBuilder with increased indent level.
    /// </summary>
    /// <returns></returns>
    public IndentedStringBuilder MakeIndentedBuilder() => new(IndentLevel + 1);

    /// <summary>
    /// Appends the specified text with the current indent. Newlines in the text are handled per line.
    /// </summary>
    /// <param name="text">The text to append (may contain multiple lines).</param>
    public void AppendLine(string text)
    {
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        foreach (var line in lines)
        {
            stringBuilder.AppendLine(Indent + line);
        }
    }

    /// <summary>
    /// Appends the specified text without any indentation or modification.
    /// (e.g. write #if directives)
    /// </summary>
    /// <param name="text">The text to append (may contain multiple lines).</param>
    public void AppendLineRaw(string text)
    {
        stringBuilder.AppendLine(text);
    }

    /// <summary>
    /// Appends the specified text with the current indent if the condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="text">The text to append (may contain multiple lines).</param>
    public void AppendLineIf(bool condition, string text)
    {
        if (condition)
        {
            AppendLine(text);
        }
    }

    /// <summary>
    /// Begins a new scope with increased indentation.
    /// </summary>
    public IDisposable? BeginScope(string braceOpen = "{", string braceClose = "}")
    {
        if (!string.IsNullOrEmpty(braceOpen))
        {
            AppendLine(braceOpen);
        }
        IncreaseIndent();
        return new ClosedScope(this, braceClose);
    }

    /// <summary>
    /// Begins a new scope with increased indentation if the condition is true.
    /// </summary>
    public IDisposable? BeginScopeIf(bool condition, string openText = "{", string closeText = "}")
    {
        if (!condition)
        {
            return null;
        }
        return BeginScope(openText, closeText);
    }

    /// <summary>Returns the current contents of the builder as a string.</summary>
    public override string ToString() => stringBuilder.ToString();

    /// <summary>
    /// A scope that decreases the indent level when disposed.
    /// </summary>
    private sealed class ClosedScope(IndentedStringBuilder builder, string? closeText = null)
        : IDisposable
    {
        private bool _disposed = false;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                builder.DecreaseIndent();
                if (!string.IsNullOrEmpty(closeText))
                {
                    builder.AppendLine(closeText!);
                }
                _disposed = true;
            }
        }
    }
}
