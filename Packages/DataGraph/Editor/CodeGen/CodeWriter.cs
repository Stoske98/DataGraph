using System.Text;

namespace DataGraph.Editor.CodeGen
{
    /// <summary>
    /// Helper for building C# source code strings with proper
    /// indentation, braces, and newlines.
    /// </summary>
    internal sealed class CodeWriter
    {
        private readonly StringBuilder _sb = new();
        private int _indent;

        /// <summary>
        /// Writes a line with current indentation.
        /// </summary>
        public CodeWriter Line(string text)
        {
            _sb.Append(' ', _indent * 4);
            _sb.AppendLine(text);
            return this;
        }

        /// <summary>
        /// Writes an empty line.
        /// </summary>
        public CodeWriter BlankLine()
        {
            _sb.AppendLine();
            return this;
        }

        /// <summary>
        /// Opens a brace block: writes the line then '{' on next line
        /// and increases indent.
        /// </summary>
        public CodeWriter BeginBlock(string header)
        {
            Line(header);
            Line("{");
            _indent++;
            return this;
        }

        /// <summary>
        /// Opens just '{' and increases indent.
        /// </summary>
        public CodeWriter BeginBrace()
        {
            Line("{");
            _indent++;
            return this;
        }

        /// <summary>
        /// Closes a brace block: decreases indent and writes '}'.
        /// </summary>
        public CodeWriter EndBlock()
        {
            _indent--;
            Line("}");
            return this;
        }

        /// <summary>
        /// Returns the built source code.
        /// </summary>
        public override string ToString() => _sb.ToString();
    }
}
