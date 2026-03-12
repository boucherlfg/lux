using System;
using System.Collections.Generic;

namespace Lux
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _current = 0;

        public Parser(List<Token> tokens) => _tokens = tokens;

        public List<Stmt> Parse()
        {
            var stmts = new List<Stmt>();
            while (!IsAtEnd())
            {
                SkipSemicolons();
                if (!IsAtEnd()) stmts.Add(ParseStmt());
            }
            return stmts;
        }

        // ── Statements ────────────────────────────────────────────────────────

        private Stmt ParseStmt()
        {
            return Peek().Type switch
            {
                TokenType.Let      => ParseLet(),
                TokenType.Fun      => CheckNext(TokenType.Identifier) ? ParseFun() : ParseExprStmt(),
                TokenType.If       => ParseIf(),
                TokenType.While    => ParseWhile(),
                TokenType.For      => ParseFor(),
                TokenType.Return   => ParseReturn(),
                TokenType.Break    => ParseBreak(),
                TokenType.Continue => ParseContinue(),
                TokenType.Try      => ParseTryCatch(),
                TokenType.Throw    => ParseThrow(),
                TokenType.LBrace   => ParseBlock(),
                _                  => ParseExprStmt(),
            };
        }

        private LetStmt ParseLet()
        {
            int line = Peek().Line;
            Consume(TokenType.Let, "let");
            string name = Consume(TokenType.Identifier, "identifier after 'let'").Lexeme;
            Consume(TokenType.Equal, "'=' after variable name");
            var init = ParseExpr();
            SkipSemicolons();
            return new LetStmt(name, init, line);
        }

        private FunDecl ParseFun()
        {
            int line = Peek().Line;
            Consume(TokenType.Fun, "fun");
            string name = Consume(TokenType.Identifier, "function name").Lexeme;
            Consume(TokenType.LParen, "'(' after function name");
            var parms = new List<string>();
            if (!Check(TokenType.RParen))
            {
                parms.Add(Consume(TokenType.Identifier, "parameter name").Lexeme);
                while (Match(TokenType.Comma))
                    parms.Add(Consume(TokenType.Identifier, "parameter name").Lexeme);
            }
            Consume(TokenType.RParen, "')' after parameters");
            return new FunDecl(name, parms, ParseBlock(), line);
        }

        private Stmt ParseIf()
        {
            Consume(TokenType.If, "if");
            Consume(TokenType.LParen, "'(' after 'if'");

            // if-let: if (let x = expr) { ... }
            if (Check(TokenType.Let))
            {
                int line = Peek().Line;
                Advance(); // consume 'let'
                string varName = Consume(TokenType.Identifier, "variable name after 'let'").Lexeme;
                Consume(TokenType.Equal, "'=' after variable name");
                var init = ParseExpr();
                Consume(TokenType.RParen, "')' after if-let expression");
                var then = ParseBlock();
                Stmt? els = null;
                if (Match(TokenType.Else))
                    els = Check(TokenType.If) ? ParseIf() : (Stmt)ParseBlock();
                return new IfLetStmt(varName, init, then, els, line);
            }

            // Regular if
            var branches = new List<(Expr Cond, BlockStmt Body)>();
            var cond = ParseExpr();
            Consume(TokenType.RParen, "')' after condition");
            branches.Add((cond, ParseBlock()));

            while (Check(TokenType.Else) && CheckNext(TokenType.If))
            {
                Advance(); // else
                Advance(); // if
                Consume(TokenType.LParen, "'(' after 'else if'");
                cond = ParseExpr();
                Consume(TokenType.RParen, "')' after condition");
                branches.Add((cond, ParseBlock()));
            }

            BlockStmt? elseBranch = null;
            if (Match(TokenType.Else)) elseBranch = ParseBlock();
            return new IfStmt(branches, elseBranch);
        }

        private WhileStmt ParseWhile()
        {
            Consume(TokenType.While, "while");
            Consume(TokenType.LParen, "'(' after 'while'");
            var cond = ParseExpr();
            Consume(TokenType.RParen, "')' after condition");
            return new WhileStmt(cond, ParseBlock());
        }

        private ForStmt ParseFor()
        {
            int line = Peek().Line;
            Consume(TokenType.For, "for");
            string varName = Consume(TokenType.Identifier, "variable after 'for'").Lexeme;
            Consume(TokenType.In, "'in' after variable");
            var iterable = ParseExpr();
            return new ForStmt(varName, iterable, ParseBlock(), line);
        }

        private ReturnStmt ParseReturn()
        {
            int line = Peek().Line;
            Consume(TokenType.Return, "return");
            Expr? value = null;
            if (!Check(TokenType.Semicolon) && !Check(TokenType.RBrace) && !IsAtEnd())
                value = ParseExpr();
            SkipSemicolons();
            return new ReturnStmt(value, line);
        }

        private BreakStmt ParseBreak()
        {
            int line = Peek().Line;
            Consume(TokenType.Break, "break");
            SkipSemicolons();
            return new BreakStmt(line);
        }

        private ContinueStmt ParseContinue()
        {
            int line = Peek().Line;
            Consume(TokenType.Continue, "continue");
            SkipSemicolons();
            return new ContinueStmt(line);
        }

        private BlockStmt ParseBlock()
        {
            Consume(TokenType.LBrace, "'{'");
            var stmts = new List<Stmt>();
            while (!Check(TokenType.RBrace) && !IsAtEnd())
            {
                SkipSemicolons();
                if (!Check(TokenType.RBrace)) stmts.Add(ParseStmt());
            }
            Consume(TokenType.RBrace, "'}'");
            return new BlockStmt(stmts);
        }

        private ExprStmt ParseExprStmt()
        {
            var expr = ParseExpr();
            SkipSemicolons();
            return new ExprStmt(expr);
        }

        // ── Expressions ───────────────────────────────────────────────────────

        private Expr ParseExpr() => ParseAssignment();

        private Expr ParseAssignment()
        {
            var left = ParseOr();

            if (Match(TokenType.Equal))
            {
                int line = Previous().Line;
                var val  = ParseAssignment();
                return left switch
                {
                    IdentExpr id  => new AssignExpr(id.Name, val, id.Line),
                    IndexExpr idx => new IndexAssignExpr(idx.Object, idx.Index, val, line),
                    GetExpr gx    => new SetExpr(gx.Object, gx.Property, val, gx.Line),
                    _ => throw new ParseError("Invalid assignment target", line),
                };
            }

            // Compound assignment:  x += y  /  x -= y
            if (Match(TokenType.PlusEqual) || Match(TokenType.MinusEqual))
            {
                var op   = Previous();
                int line = op.Line;
                var val  = ParseAssignment();
                var binType   = op.Type == TokenType.PlusEqual ? TokenType.Plus : TokenType.Minus;
                var binLexeme = op.Type == TokenType.PlusEqual ? "+" : "-";
                var binTok    = new Token(binType, binLexeme, null, line);
                var combined  = new BinaryExpr(left, binTok, val);
                return left switch
                {
                    IdentExpr id  => new AssignExpr(id.Name, combined, id.Line),
                    IndexExpr idx => new IndexAssignExpr(idx.Object, idx.Index, combined, line),
                    GetExpr gx    => new SetExpr(gx.Object, gx.Property, combined, gx.Line),
                    _ => throw new ParseError("Invalid assignment target", line),
                };
            }

            return left;
        }

        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenType.Or)) { var op = Previous(); left = new LogicalExpr(left, op, ParseAnd()); }
            return left;
        }

        private Expr ParseAnd()
        {
            var left = ParseBitOr();
            while (Match(TokenType.And)) { var op = Previous(); left = new LogicalExpr(left, op, ParseBitOr()); }
            return left;
        }

        private Expr ParseBitOr()
        {
            var left = ParseBitAnd();
            while (Match(TokenType.BitOr)) { var op = Previous(); left = new BinaryExpr(left, op, ParseBitAnd()); }
            return left;
        }

        private Expr ParseBitAnd()
        {
            var left = ParseEquality();
            while (Match(TokenType.BitAnd)) { var op = Previous(); left = new BinaryExpr(left, op, ParseEquality()); }
            return left;
        }

        private Expr ParseEquality()
        {
            var left = ParseComparison();
            while (Match(TokenType.EqualEqual, TokenType.BangEqual))
            { var op = Previous(); left = new BinaryExpr(left, op, ParseComparison()); }
            return left;
        }

        private Expr ParseComparison()
        {
            var left = ParseShift();
            while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
            { var op = Previous(); left = new BinaryExpr(left, op, ParseShift()); }
            return left;
        }

        private Expr ParseShift()
        {
            var left = ParseTerm();
            while (Match(TokenType.ShiftLeft, TokenType.ShiftRight))
            { var op = Previous(); left = new BinaryExpr(left, op, ParseTerm()); }
            return left;
        }

        private Expr ParseTerm()
        {
            var left = ParseFactor();
            while (Match(TokenType.Plus, TokenType.Minus))
            { var op = Previous(); left = new BinaryExpr(left, op, ParseFactor()); }
            return left;
        }

        private Expr ParseFactor()
        {
            var left = ParseUnary();
            while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
            { var op = Previous(); left = new BinaryExpr(left, op, ParseUnary()); }
            return left;
        }

        private Expr ParseUnary()
        {
            if (Match(TokenType.Bang, TokenType.Minus, TokenType.BitNot))
                return new UnaryExpr(Previous(), ParseUnary());
            return ParseCall();
        }

        private Expr ParseCall()
        {
            var expr = ParsePrimary();
            while (true)
            {
                if (Match(TokenType.LParen))
                {
                    int line = Previous().Line;
                    var args = new List<Expr>();
                    if (!Check(TokenType.RParen))
                    {
                        args.Add(ParseExpr());
                        while (Match(TokenType.Comma)) args.Add(ParseExpr());
                    }
                    Consume(TokenType.RParen, "')' after arguments");
                    expr = new CallExpr(expr, args, line);
                }
                else if (Match(TokenType.Dot))
                {
                    int line = Previous().Line;
                    string prop = Consume(TokenType.Identifier, "property name after '.'").Lexeme;
                    expr = new GetExpr(expr, prop, line);
                }
                else if (Match(TokenType.LBracket))
                {
                    int line = Previous().Line;
                    var index = ParseExpr();
                    Consume(TokenType.RBracket, "']' after index");
                    expr = new IndexExpr(expr, index, line);
                }
                else break;
            }
            return expr;
        }

        private Expr ParsePrimary()
        {
            if (Match(TokenType.Number))     return new NumberLit((double)Previous().Literal!);
            if (Match(TokenType.String))     return new StringLit((string)Previous().Literal!);
            if (Match(TokenType.True))       return new BoolLit(true);
            if (Match(TokenType.False))      return new BoolLit(false);
            if (Match(TokenType.Null))       return new NullLit();
            if (Match(TokenType.Identifier)) return new IdentExpr(Previous().Lexeme, Previous().Line);
            if (Match(TokenType.This))       return new ThisExpr(Previous().Line);

            if (Match(TokenType.LBracket))
            {
                var elements = new List<Expr>();
                if (!Check(TokenType.RBracket))
                {
                    elements.Add(ParseExpr());
                    while (Match(TokenType.Comma)) elements.Add(ParseExpr());
                }
                Consume(TokenType.RBracket, "']' after list elements");
                return new ArrayLit(elements);
            }

            if (Match(TokenType.LBrace))
            {
                var pairs = new List<(Expr, Expr)>();
                if (!Check(TokenType.RBrace))
                {
                    var key = ParseExpr();
                    Consume(TokenType.Colon, "':' after dict key");
                    var val = ParseExpr();
                    pairs.Add((key, val));
                    while (Match(TokenType.Comma))
                    {
                        key = ParseExpr();
                        Consume(TokenType.Colon, "':' after dict key");
                        val = ParseExpr();
                        pairs.Add((key, val));
                    }
                }
                Consume(TokenType.RBrace, "'}' after dict entries");
                return new DictLit(pairs);
            }

            if (Match(TokenType.LParen))
            {
                var expr = ParseExpr();
                Consume(TokenType.RParen, "')' after expression");
                return expr;
            }

            if (Match(TokenType.Fun))
            {
                int line = Previous().Line;
                Consume(TokenType.LParen, "'(' after 'fun'");
                var parms = new List<string>();
                if (!Check(TokenType.RParen))
                {
                    parms.Add(Consume(TokenType.Identifier, "parameter name").Lexeme);
                    while (Match(TokenType.Comma))
                        parms.Add(Consume(TokenType.Identifier, "parameter name").Lexeme);
                }
                Consume(TokenType.RParen, "')' after parameters");
                BlockStmt body;
                if (Match(TokenType.Arrow))
                {
                    // fun(x) => expr  shorthand
                    var ret = new ReturnStmt(ParseExpr(), Previous().Line);
                    body = new BlockStmt(new List<Stmt> { ret });
                }
                else
                {
                    body = ParseBlock();
                }
                return new FunExpr(parms, body, line);
            }

            throw new ParseError($"Unexpected token '{Peek().Lexeme}'", Peek().Line);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool IsAtEnd()   => Peek().Type == TokenType.EOF;
        private Token Peek()     => _tokens[_current];
        private Token Previous() => _tokens[_current - 1];
        private Token Advance()  => _tokens[_current++];

        private bool Check(TokenType t) => !IsAtEnd() && Peek().Type == t;
        private bool CheckNext(TokenType t)
            => _current + 1 < _tokens.Count && _tokens[_current + 1].Type == t;

        private bool Match(params TokenType[] types)
        {
            foreach (var t in types) { if (Check(t)) { Advance(); return true; } }
            return false;
        }

        private Token Consume(TokenType type, string expected)
        {
            if (Check(type)) return Advance();
            throw new ParseError($"Expected {expected}, got '{Peek().Lexeme}'", Peek().Line);
        }

        private void SkipSemicolons() { while (Match(TokenType.Semicolon)) { } }

        private TryCatchStmt ParseTryCatch()
        {
            Consume(TokenType.Try, "try");
            var tryBlock = ParseBlock();
            Consume(TokenType.Catch, "'catch' after try block");
            Consume(TokenType.LParen, "'(' after 'catch'");
            string errorVar = Consume(TokenType.Identifier, "error variable name").Lexeme;
            Consume(TokenType.RParen, "')' after error variable");
            var catchBlock = ParseBlock();
            return new TryCatchStmt(tryBlock, errorVar, catchBlock);
        }

        private ThrowStmt ParseThrow()
        {
            int line = Peek().Line;
            Consume(TokenType.Throw, "throw");
            var value = ParseExpr();
            SkipSemicolons();
            return new ThrowStmt(value, line);
        }
    }
}
