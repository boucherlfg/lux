using System;
using System.Collections.Generic;

namespace Lux
{
    public class LexError : Exception
    {
        public int Line { get; }
        public LexError(string message, int line) : base(message) => Line = line;
    }

    public class ParseError : Exception
    {
        public int Line { get; }
        public ParseError(string message, int line) : base(message) => Line = line;
    }

    public class LuxError : Exception
    {
        public int Line { get; }
        /// <summary>Snapshot of the Lux call stack at the point the error was raised.</summary>
        public IReadOnlyList<string> CallStack { get; }

        public LuxError(string message, int line, IReadOnlyList<string> callStack) : base(message)
        {
            Line      = line;
            CallStack = callStack;
        }

        /// <summary>Convenience overload for external/embedding code where no call stack is available.</summary>
        public LuxError(string message, int line) : this(message, line, System.Array.Empty<string>()) { }
    }

    /// <summary>A single syntax error returned by <see cref="Interpreter.Validate"/>.</summary>
    public sealed class ValidationError
    {
        /// <summary>Phase where the error was detected: <c>"lex"</c> or <c>"parse"</c>.</summary>
        public string Kind    { get; }
        /// <summary>1-based source line number.</summary>
        public int    Line    { get; }
        /// <summary>Human-readable description of the error.</summary>
        public string Message { get; }

        public ValidationError(string kind, int line, string message)
        {
            Kind    = kind;
            Line    = line;
            Message = message;
        }

        public override string ToString() => $"[{Kind} error] line {Line}: {Message}";
    }
}
