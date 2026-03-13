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
        public LuxError(string message, int line) : base(message)
        {
            Line      = line;
            CallStack = Interpreter.CaptureCallStack();
        }
    }
}
