using System.Collections.Generic;

namespace Lux
{
    // ── Expressions ──────────────────────────────────────────────────────────

    public abstract record Expr;

    public record NumberLit(double Value)                                    : Expr;
    public record StringLit(string Value)                                    : Expr;
    public record BoolLit(bool Value)                                        : Expr;
    public record NullLit                                                    : Expr;
    public record ArrayLit(List<Expr> Elements)                              : Expr;
    public record DictLit(List<(Expr Key, Expr Value)> Pairs)               : Expr;

    public record IdentExpr(string Name, int Line)                           : Expr;
    public record AssignExpr(string Name, Expr Value, int Line)              : Expr;
    public record IndexAssignExpr(Expr Object, Expr Index, Expr Value, int Line) : Expr;
    public record FunExpr(List<string> Params, BlockStmt Body, int Line)         : Expr;
    public record ThisExpr(int Line)                                              : Expr;
    public record SetExpr(Expr Object, string Property, Expr Value, int Line)    : Expr;

    public record BinaryExpr(Expr Left, Token Op, Expr Right)               : Expr;
    public record LogicalExpr(Expr Left, Token Op, Expr Right)              : Expr;
    public record UnaryExpr(Token Op, Expr Right)                           : Expr;

    public record CallExpr(Expr Callee, List<Expr> Args, int Line)          : Expr;
    public record GetExpr(Expr Object, string Property, int Line)           : Expr;
    public record IndexExpr(Expr Object, Expr Index, int Line)              : Expr;

    // ── Statements ────────────────────────────────────────────────────────────

    public abstract record Stmt;

    public record ExprStmt(Expr Expression)                                             : Stmt;
    public record BlockStmt(List<Stmt> Body)                                            : Stmt;
    public record LetStmt(string Name, Expr Init, int Line)                             : Stmt;
    public record IfStmt(List<(Expr Cond, BlockStmt Body)> Branches, BlockStmt? Else)  : Stmt;
    public record WhileStmt(Expr Condition, BlockStmt Body)                             : Stmt;
    public record ForStmt(string Var, Expr Iterable, BlockStmt Body, int Line)         : Stmt;
    public record ReturnStmt(Expr? Value, int Line)                                     : Stmt;
    public record BreakStmt(int Line)                                                   : Stmt;
    public record ContinueStmt(int Line)                                                : Stmt;
    public record FunDecl(string Name, List<string> Params, BlockStmt Body, int Line)  : Stmt;
    /// <summary>if-let binding: evaluates <c>Init</c>, binds result to <c>Var</c> if truthy, else runs optional <c>Else</c> branch (may be another <c>IfLetStmt</c>, <c>IfStmt</c>, or a plain <c>BlockStmt</c>).</summary>
    public record IfLetStmt(string Var, Expr Init, BlockStmt Then, Stmt? Else, int Line) : Stmt;
    public record ThrowStmt(Expr Value, int Line)                                       : Stmt;
    public record TryCatchStmt(BlockStmt Try, string ErrorVar, BlockStmt Catch)        : Stmt;
}
