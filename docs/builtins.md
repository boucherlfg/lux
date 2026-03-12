# Built-in Functions

These functions are available globally in every Lux script.

---

## print(...)

Writes its arguments to stdout, separated by spaces, followed by a newline. Accepts any number of arguments.

```lux
print("Hello")             // Hello
print("x =", 42)          // x = 42
print(1, 2, 3)             // 1 2 3
```

---

## input()

Reads a line from stdin and returns it as a string.

```lux
let name = input()
print("Hello, " + name)
```

---

## str(value)

Converts any value to its string representation.

```lux
str(42)       // "42"
str(3.14)     // "3.14"
str(true)     // "true"
str(null)     // "null"
str([1,2,3])  // "[1, 2, 3]"
```

---

## num(value)

Converts a value to a number.

| Input        | Result                              |
|--------------|-------------------------------------|
| number       | same value                          |
| `true`       | `1`                                 |
| `false`      | `0`                                 |
| parseable string | parsed number                   |
| anything else | runtime error                      |

```lux
num("42")     // 42
num(true)     // 1
num("3.14")   // 3.14
```

---

## typeof(value)

Returns the runtime type name of a value as a string.

| Value type | Returns      |
|------------|--------------|
| number     | `"number"`   |
| string     | `"string"`   |
| bool       | `"bool"`     |
| null       | `"null"`     |
| list       | `"list"`     |
| dict       | `"dict"`     |
| object     | `"object"`   |
| function   | `"function"` |

```lux
typeof(42)          // "number"
typeof("hello")     // "string"
typeof([1, 2])      // "list"
typeof(fun(x) => x) // "function"
```

---

## len(value)

Returns the length of a string, list, or dict.

```lux
len("hello")      // 5
len([1, 2, 3])    // 3
len({"a": 1})     // 1
```

---

## range(end) / range(start, end) / range(start, end, step)

Returns a list of numbers.

| Call                    | Result                    |
|-------------------------|---------------------------|
| `range(5)`              | `[0, 1, 2, 3, 4]`         |
| `range(2, 5)`           | `[2, 3, 4]`               |
| `range(0, 10, 2)`       | `[0, 2, 4, 6, 8]`         |
| `range(5, 0, -1)`       | `[5, 4, 3, 2, 1]`         |

```lux
for i in range(3) {
    print(i)    // 0, 1, 2
}
```

---

## floor(value)

Returns the largest integer less than or equal to `value`. The result is still a number.

```lux
floor(3.7)    // 3
floor(-1.2)   // -2
floor(3.0)    // 3
```

---

## assert(condition, message)

Throws a runtime error with `message` if `condition` is falsy.

```lux
assert(x > 0, "x must be positive")
```

---

## import(path)

Loads and executes a `.lux` file relative to the current script's directory. Returns a namespace object containing all top-level declarations from the module.

```lux
let math = import("mathutils.lux")
print(math.add(3, 4))    // 7
print(math.PI)           // 3.14159...
```

Only user-defined globals are exported (built-ins like `print` are not included in the namespace).

---

## getType(value)

Returns a reflection object describing `value`. Useful for inspecting objects at runtime.

The returned object has these fields:

| Field                          | Description                                        |
|--------------------------------|----------------------------------------------------|
| `name`                         | Type name string (same as `typeof`)                |
| `fields(instance)`             | List of field/key names on `instance`              |
| `hasField(instance, name)`     | `true` if `instance` has the named field           |
| `getField(instance, name)`     | Read a field by name                               |
| `setField(instance, name, val)`| Write a field by name                              |
| `getMethod(instance, name)`    | Returns a method-info object (see below)           |

### Method-info object

Returned by `getType(val).getMethod(instance, name)`:

| Field                        | Description                               |
|------------------------------|-------------------------------------------|
| `name`                       | Method name string                        |
| `getArgs()`                  | List of parameter name strings            |
| `invoke(instance, ...args)`  | Call the method with the given receiver   |

```lux
fun Point(x, y) {
    this.x = x
    this.y = y
    this.sum = fun() { return this.x + this.y }
}

let p = Point(3, 4)
let t = getType(p)
print(t.name)                   // "object"
print(t.fields(p))              // ["x", "y", "sum", "ctor"]
print(t.hasField(p, "x"))       // true
print(t.getField(p, "x"))       // 3

let m = t.getMethod(p, "sum")
print(m.name)                   // "sum"
print(m.getArgs())              // []
print(m.invoke(p))              // 7
```
