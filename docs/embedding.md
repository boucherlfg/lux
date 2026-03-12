# Embedding Lux in C#

Reference the `LuxLang.Lib` project (or its compiled DLL) from your C# project, then use the `Lux` namespace.

---

## Quick start

```csharp
using Lux;

var interp = new Interpreter();
interp.Run("print(\"hello from Lux\")");
```

---

## Running scripts

```csharp
// From a source string
interp.Run(luxSourceCode);

// From a file
interp.Run(File.ReadAllText("script.lux"));
```

---

## Exposing C# functions to Lux

Use `Register` to make a C# delegate callable from Lux scripts.

```csharp
// Action (void return)
interp.Register("log", (Action<string>)(msg => Console.WriteLine(msg)));

// Func
interp.Register("add",  (Func<double, double, double>)((a, b) => a + b));
interp.Register("now",  (Func<string>)(() => DateTime.Now.ToString()));
```

In Lux:

```lux
log("hello")
let sum = add(1, 2)   // 3.0
```

---

## Exposing C# objects to Lux

### Snapshot wrap

Properties are read once at wrap time. Method calls are live.

```csharp
interp.RegisterObject("config", myConfigObject);
```

### Live wrap

Every property read/write in Lux hits the live C# object.

```csharp
interp.RegisterObjectLive("player", myPlayerObject);
```

In Lux:

```lux
player.Health = player.Health - 10
player.TakeDamage(10)
```

---

## Calling Lux functions from C#

Define a function in your script, then call it from C#.

```lux
// script.lux
fun greet(name) {
    return "Hello, " + name + "!"
}
```

```csharp
interp.Run(File.ReadAllText("script.lux"));

// Raw result (object?)
var raw = interp.CallFunction("greet", "World");

// Typed result
string msg = interp.CallFunction<string>("greet", "World");   // "Hello, World!"
double score = interp.CallFunction<double>("getScore");
```

---

## Reading and writing Lux globals from C#

```csharp
// Write
interp.SetGlobal("difficulty", 3);

// Read
double difficulty = interp.GetGlobal<double>("difficulty");
string name       = interp.GetGlobal<string>("playerName");
```

---

## Type conversion reference

| C# type | Lux type |
|---|---|
| `double` / any numeric | `number` |
| `string` | `string` |
| `bool` | `bool` |
| `null` | `null` |
| `IDictionary` | `dict` |
| `IEnumerable` (non-string) | `list` |
| any other object | `object` (via `RegisterObjectLive`) |

When reading back from Lux, `CallFunction<T>` and `GetGlobal<T>` coerce automatically:

- Lux `number` → any C# numeric type (`int`, `float`, `long`, …)
- Lux `string` → `string`
- Lux `bool` → `bool`
- Any Lux value → `string` (calls `str()` internally)

---

## Error handling

Lux errors surface as `LuxError` (runtime), `ParseError`, or `LexError`, all in the `Lux` namespace. They carry a `Line` number.

```csharp
try
{
    interp.Run(source);
}
catch (LuxError ex)
{
    Console.Error.WriteLine($"Runtime error on line {ex.Line}: {ex.Message}");
}
catch (ParseError ex)
{
    Console.Error.WriteLine($"Parse error on line {ex.Line}: {ex.Message}");
}
catch (LexError ex)
{
    Console.Error.WriteLine($"Lex error on line {ex.Line}: {ex.Message}");
}
```

---

## Complete example

```csharp
using Lux;

var interp = new Interpreter();

// Expose a C# API
interp.Register("log",  (Action<string>)(Console.WriteLine));
interp.Register("now",  (Func<string>)(() => DateTime.UtcNow.ToString("u")));

// Run the script
interp.Run(@"
    log(""Script started at "" + now())
    fun add(a, b) { return a + b }
");

// Call back into Lux
double result = interp.CallFunction<double>("add", 10, 32);
Console.WriteLine($"add(10, 32) = {result}");   // 42
```
