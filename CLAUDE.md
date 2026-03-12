# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the executable (CLI entry point)
dotnet build LuxLang.csproj

# Run a Lux script
dotnet run --project LuxLang.csproj -- examples/hello.lux

# Launch the REPL (no arguments)
dotnet run --project LuxLang.csproj

# Build the embeddable library (no entry point)
dotnet build LuxLang.Lib.csproj

# Build everything
dotnet build LuxLang.sln
```

There are no automated tests. Manual testing is done by running scripts in `examples/`.

## Maintenance rules

These rules apply every time a new language feature is implemented (new syntax, new built-in, new method, new statement form, etc.):

1. **Add an example script.** Create `examples/<feature>.lux` that exercises the new feature end-to-end. Run it with `dotnet run --project LuxLang.csproj -- examples/<feature>.lux` and verify the output before committing.

2. **Update the docs.** Edit the relevant file(s) in `docs/`:
   - New syntax or control-flow construct → `docs/syntax.md`
   - New type or method on an existing type → `docs/types.md`
   - New built-in global function → `docs/builtins.md`
   - New error-handling behaviour → `docs/error-handling.md`
   - Changes to `import` or module semantics → `docs/modules.md`

3. **Update CLAUDE.md.** If the feature changes the pipeline, adds a new key file, changes the object model, or alters the C# embedding API, update the relevant section of this file.

## Architecture

The solution has two projects:

- **`LuxLang.csproj`** — CLI executable. Contains all `src/` files plus `src/Program.cs` (entry point). Targets `.NET 4.8`, C# 9.
- **`LuxLang.Lib.csproj`** — Embeddable library (same sources minus `Program.cs`). Used when hosting Lux inside another C# application.

### Pipeline

```
source string
  → Lexer        (src/Lexer.cs)      List<Token>
  → Parser       (src/Parser.cs)     List<Stmt>  (AST nodes from src/Ast.cs)
  → Interpreter  (src/Interpreter.cs) executes tree-walk
```

### Key files

| File | Purpose |
|---|---|
| `src/Token.cs` | `Token` record + `TokenType` enum |
| `src/Ast.cs` | All AST node types (`Expr` and `Stmt` record hierarchies) |
| `src/LuxEnvironment.cs` | Linked-list scope chain; `Define`/`Get`/`Set` walk the chain |
| `src/Interpreter.cs` | Tree-walk evaluator; also defines `LuxFunction`, `LuxObject`, `LuxList`, `LuxDict`, `NativeFunc`, and all built-in functions |
| `src/LuxBridge.cs` | C# ↔ Lux interop: `Register`, `RegisterObject`, `RegisterObjectLive`, `CallFunction<T>`, `GetGlobal<T>`, `SetGlobal` |
| `src/Errors.cs` | `LexError`, `ParseError`, `LuxError` (all carry a `Line` number) |
| `src/Program.cs` | CLI entry point: file runner + REPL |

### Object model

There are no classes in Lux. Objects are created by calling any function that sets fields on `this`. When a function returns without an explicit `return` value and `this` has fields, `this` is implicitly returned along with a `ctor` back-reference (constructor pattern). An `IfLetStmt` is a special form that evaluates an expression, binds the result to a name, and enters the `then` branch only if the value is truthy.

### C# embedding (LuxBridge)

`LuxBridge` provides extension methods on `Interpreter`:
- `interp.Register("name", delegate)` — expose a C# delegate as a Lux function
- `interp.RegisterObject("name", obj)` — snapshot-wrap a C# object (properties read at wrap time)
- `interp.RegisterObjectLive("name", obj)` — live-wrap via `LiveProperty` (reads/writes hit the C# object)
- `interp.CallFunction<T>("name", args...)` — call a Lux function from C#
- `interp.GetGlobal<T>("name")` / `interp.SetGlobal("name", value)` — access globals

`LuxObject` supports `LiveProperty` slots — fields whose getter/setter delegate to C# lambdas — used by `WrapLive` and available for custom host integrations.
