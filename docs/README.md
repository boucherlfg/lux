# Lux Language

Lux is a dynamically-typed, interpreted scripting language with a clean C-like syntax. It supports first-class functions, closures, objects, lists, dicts, and error handling.

## Documentation

- [Syntax Reference](syntax.md) — Variables, control flow, functions, operators
- [Types & Methods](types.md) — Numbers, strings, lists, dicts, objects
- [Built-in Functions](builtins.md) — `print`, `range`, `import`, `getType`, etc.
- [Error Handling](error-handling.md) — `try`, `catch`, `throw`
- [Modules](modules.md) — `import` and namespaces
- [Embedding in C#](embedding.md) — Using the DLL from C# code

## Quick Start

```lux
// Hello, World!
print("Hello, World!")

// Variables
let name = "Alice"
let age = 30
print("Name: " + name + ", Age: " + str(age))

// Function
fun greet(person) {
    return "Hello, " + person + "!"
}
print(greet("World"))

// List + for loop
let fruits = ["apple", "banana", "cherry"]
for fruit in fruits {
    print(fruit)
}
```

## Running Lux

```
dotnet run -- path/to/script.lux
```
