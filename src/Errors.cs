using System;

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
        public LuxError(string message, int line) : base(message) => Line = line;
    }
}
