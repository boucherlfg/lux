namespace Lux
{
    public enum TokenType
    {
        // Literals
        Number, String, True, False, Null,

        // Identifiers & keywords
        Identifier,
        Let, Fun, Return, If, Else, While, For, In, Break, Continue, This,
        Try, Catch, Throw,

        // Arithmetic
        Plus, Minus, Star, Slash, Percent,

        // Comparison
        EqualEqual, BangEqual, Less, LessEqual, Greater, GreaterEqual,

        // Logical
        And, Or, Bang,

        // Assignment
        Equal, PlusEqual, MinusEqual, Arrow,

        // Delimiters
        LParen, RParen, LBrace, RBrace, LBracket, RBracket,
        Comma, Dot, Semicolon, Colon,

        EOF
    }

    public record Token(TokenType Type, string Lexeme, object? Literal, int Line)
    {
        public override string ToString() => $"{Type} '{Lexeme}' line:{Line}";
    }
}
