using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lux
{
    public class Lexer
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new List<Token>();
        private int _start   = 0;
        private int _current = 0;
        private int _line    = 1;

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            { "let",      TokenType.Let      },
            { "fun",      TokenType.Fun      },
            { "return",   TokenType.Return   },
            { "if",       TokenType.If       },
            { "else",     TokenType.Else     },
            { "while",    TokenType.While    },
            { "for",      TokenType.For      },
            { "in",       TokenType.In       },
            { "true",     TokenType.True     },
            { "false",    TokenType.False    },
            { "null",     TokenType.Null     },
            { "break",    TokenType.Break    },
            { "continue", TokenType.Continue },
            { "this",     TokenType.This     },
            { "try",      TokenType.Try      },
            { "catch",    TokenType.Catch    },
            { "throw",    TokenType.Throw    },
        };

        public Lexer(string source) => _source = source;

        public List<Token> Tokenize()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }
            _tokens.Add(new Token(TokenType.EOF, "", null, _line));
            return _tokens;
        }

        /// <summary>
        /// Like <see cref="Tokenize"/> but collects all lex errors into
        /// <paramref name="errors"/> instead of stopping at the first one.
        /// </summary>
        public List<Token> TokenizeAll(List<ValidationError> errors)
        {
            while (!IsAtEnd())
            {
                _start = _current;
                try { ScanToken(); }
                catch (LexError e) { errors.Add(new ValidationError("lex", e.Line, e.Message)); }
            }
            _tokens.Add(new Token(TokenType.EOF, "", null, _line));
            return _tokens;
        }

        private void ScanToken()
        {
            char c = Advance();
            switch (c)
            {
                case '(': AddToken(TokenType.LParen);   break;
                case ')': AddToken(TokenType.RParen);   break;
                case '{': AddToken(TokenType.LBrace);   break;
                case '}': AddToken(TokenType.RBrace);   break;
                case '[': AddToken(TokenType.LBracket); break;
                case ']': AddToken(TokenType.RBracket); break;
                case ',': AddToken(TokenType.Comma);    break;
                case '.': AddToken(TokenType.Dot);      break;
                case ';': AddToken(TokenType.Semicolon);break;
                case ':': AddToken(TokenType.Colon);    break;
                case '*': AddToken(TokenType.Star);     break;
                case '%': AddToken(TokenType.Percent);  break;
                case '-': AddToken(Match('=') ? TokenType.MinusEqual : TokenType.Minus); break;
                case '+': AddToken(Match('=') ? TokenType.PlusEqual  : TokenType.Plus);  break;
                case '/':
                    if (Match('/')) { while (Peek() != '\n' && !IsAtEnd()) Advance(); }
                    else AddToken(TokenType.Slash);
                    break;
                case '!': AddToken(Match('=') ? TokenType.BangEqual    : TokenType.Bang);    break;
                case '=':
                    if (Match('='))      AddToken(TokenType.EqualEqual);
                    else if (Match('>')) AddToken(TokenType.Arrow);
                    else                 AddToken(TokenType.Equal);
                    break;
                case '<': AddToken(Match('<') ? TokenType.ShiftLeft  : Match('=') ? TokenType.LessEqual    : TokenType.Less);    break;
                case '>': AddToken(Match('>') ? TokenType.ShiftRight : Match('=') ? TokenType.GreaterEqual : TokenType.Greater); break;
                case '&': AddToken(Match('&') ? TokenType.And    : TokenType.BitAnd); break;
                case '|': AddToken(Match('|') ? TokenType.Or     : TokenType.BitOr);  break;
                case '~': AddToken(TokenType.BitNot); break;
                case '"': ScanString(); break;
                case '\n': _line++; break;
                case ' ': case '\r': case '\t': break;
                default:
                    if (char.IsDigit(c))                   ScanNumber();
                    else if (char.IsLetter(c) || c == '_') ScanIdentifier();
                    else throw new LexError($"Unexpected character '{c}'", _line);
                    break;
            }
        }

        private void ScanString()
        {
            while (Peek() != '"' && !IsAtEnd())
            {
                if (Peek() == '\n') _line++;
                if (Peek() == '\\') Advance(); // skip escape char so \" doesn't end the string early
                if (!IsAtEnd()) Advance();
            }
            if (IsAtEnd()) throw new LexError("Unterminated string", _line);
            Advance(); // closing "
            string raw = _source.Substring(_start + 1, _current - _start - 2);
            string value = raw
                .Replace("\\n",  "\n")
                .Replace("\\t",  "\t")
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"");
            AddToken(TokenType.String, value);
        }

        private void ScanNumber()
        {
            while (char.IsDigit(Peek())) Advance();
            if (Peek() == '.' && char.IsDigit(PeekNext()))
            {
                Advance();
                while (char.IsDigit(Peek())) Advance();
            }
            double value = double.Parse(
                _source.Substring(_start, _current - _start),
                CultureInfo.InvariantCulture);
            AddToken(TokenType.Number, value);
        }

        private void ScanIdentifier()
        {
            while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Advance();
            string text = _source.Substring(_start, _current - _start);
            TokenType type = Keywords.TryGetValue(text, out TokenType kw) ? kw : TokenType.Identifier;
            AddToken(type);
        }

        private char Advance()     => _source[_current++];
        private bool IsAtEnd()     => _current >= _source.Length;
        private char Peek()        => IsAtEnd() ? '\0' : _source[_current];
        private char PeekNext()    => (_current + 1 >= _source.Length) ? '\0' : _source[_current + 1];
        private bool Match(char e) { if (IsAtEnd() || _source[_current] != e) return false; _current++; return true; }

        private void AddToken(TokenType type, object? literal = null)
            => _tokens.Add(new Token(type, _source.Substring(_start, _current - _start), literal, _line));
    }
}
