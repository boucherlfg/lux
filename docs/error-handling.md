# Error Handling

Lux uses `try`, `catch`, and `throw` for error handling.

---

## throw

`throw` raises an error. You can throw any value — a string, number, dict, or object.

```lux
throw "something went wrong"
throw {"code": 404, "msg": "not found"}
```

---

## try / catch

`catch` binds a **error object** to the given variable. The error object always has three fields:

| Field | Type | Description |
|---|---|---|
| `value` | any | The thrown payload (string, dict, object, …) or the runtime-error message |
| `line` | number | The source line where the error was raised |
| `stacktrace` | list of strings | Call-stack frames at the time of the error, most-recent first |

```lux
try {
    // code that might fail
} catch (e) {
    print(e.value)       // the thrown value or error message
    print(e.line)        // line number of the throw / runtime error
    print(e.stacktrace)  // list of "at <fn> (line <n>)" strings
}
```

Runtime errors (e.g. division by zero, type errors) are also caught and their message is exposed as `e.value`.

---

## Examples

### Catching a thrown string

```lux
try {
    throw "something went wrong"
} catch (e) {
    print("Caught: " + e.value)     // "something went wrong"
    print("Line: "  + str(e.line))  // source line of the throw
}
```

### Catching a thrown object

```lux
try {
    throw {"code": 404, "msg": "not found"}
} catch (e) {
    print("Error " + str(e.value["code"]) + ": " + e.value["msg"])
}
```

### Catching a runtime error

```lux
try {
    let x = 1 / 0
} catch (e) {
    print("Runtime error: " + e.value)  // "Division by zero"
    print("Line: " + str(e.line))
}
```

### Inspecting the stack trace

```lux
fun level2() { throw "deep error" }
fun level1() { level2() }

try {
    level1()
} catch (e) {
    print(e.value)                  // "deep error"
    print(e.stacktrace)             // ["at level2 (line N)", "at level1 (line M)"]
}
```

### Throwing from inside a function

```lux
fun divide(a, b) {
    if (b == 0) { throw "division by zero" }
    return a / b
}

try {
    print(divide(10, 2))   // 5
    print(divide(5, 0))    // throws
} catch (e) {
    print("Caught: "      + e.value)
    print("Line: "        + str(e.line))
    print("Stack trace: " + str(e.stacktrace))
}
```

---

## Uncaught errors

If a `throw` or runtime error is not caught by any `try/catch`, the interpreter prints the error to stderr with its full stack trace:

```
[Unhandled Throw] line 6: something went wrong
  at inner (line 2)
  at outer (line 9)

[Runtime Error] line 6: Division by zero
  at inner (line 2)
  at outer (line 9)
```

---

## Validation (syntax checking without execution)

Use `Interpreter.Validate(source)` to check a Lux source string for lex and parse errors **without executing it**. This is useful as a build/lint step.

```csharp
var interp = new Interpreter();
var errors = interp.Validate(source);

if (errors.Count == 0)
{
    Console.WriteLine("No syntax errors.");
}
else
{
    foreach (var e in errors)
        Console.Error.WriteLine(e);  // e.g. "[parse error] line 3: Expected '}', got 'EOF'"
}
```

Each `ValidationError` exposes:

| Property | Type | Description |
|---|---|---|
| `Kind` | `string` | `"lex"` or `"parse"` |
| `Line` | `int` | 1-based source line number |
| `Message` | `string` | Human-readable error description |

`Validate` never throws — it always returns a (possibly empty) list.

### CLI: `--check`

You can validate a file from the command line without running it:

```
lux --check path/to/script.lux
```

Exits with code `0` and prints `<path>: OK` if there are no errors.  
Exits with code `1` and prints each error to stderr if errors are found.

---

## Notes

- There is no `finally` block.
- `catch` catches both explicit `throw` values and interpreter-level runtime errors.
- A `throw` inside a `catch` block will propagate to an outer `try/catch` or terminate the script.
- The `stacktrace` list is empty when the error is raised at the top level (outside any function call).

