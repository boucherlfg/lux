# Types & Methods

## number

All numbers are 64-bit floating point. Integer values print without a decimal point.

```lux
let x = 42
let pi = 3.14159
let neg = -7
```

Arithmetic: `+`, `-`, `*`, `/`, `%`

---

## string

Strings are immutable sequences of characters. String literals use double quotes.

```lux
let s = "Hello, World!"
let multiword = "line one\nline two"
```

### Concatenation

The `+` operator concatenates strings. Non-string operands are stringified automatically.

```lux
"Hello, " + "World"   // "Hello, World"
"Count: " + 42        // "Count: 42"
```

### Indexing

```lux
let s = "hello"
print(s[0])    // "h"
print(s[-1])   // "o"
```

### String methods

| Method                     | Description                                        |
|----------------------------|----------------------------------------------------|
| `s.len()`                  | Length of the string                               |
| `s.upper()`                | Uppercase copy                                     |
| `s.lower()`                | Lowercase copy                                     |
| `s.trim()`                 | Strip leading/trailing whitespace                  |
| `s.contains(sub)`          | `true` if `sub` appears in `s`                     |
| `s.startsWith(prefix)`     | `true` if `s` starts with `prefix`                 |
| `s.endsWith(suffix)`       | `true` if `s` ends with `suffix`                   |
| `s.replace(old, new)`      | Replace all occurrences of `old` with `new`        |
| `s.split(sep)`             | Split by `sep`, returns a list of strings          |

```lux
let msg = "hello world"
print(msg.upper())              // "HELLO WORLD"
print(msg.contains("world"))   // true
print(msg.split(" "))          // ["hello", "world"]
```

---

## bool

`true` or `false`.

```lux
let flag = true
if (!flag) { print("off") }
```

---

## null

Represents the absence of a value.

```lux
let x = null
if (x == null) { print("no value") }
```

---

## list

An ordered, mutable sequence of any values.

```lux
let nums = [1, 2, 3]
let mixed = [1, "two", true, null]
let empty = []
```

### Indexing & assignment

```lux
nums[0]      // 1
nums[-1]     // 3  (last element)
nums[1] = 99
```

### List concatenation

```lux
let a = [1, 2]
let b = [3, 4]
let c = a + b   // [1, 2, 3, 4]
```

### List methods

| Method               | Description                                          |
|----------------------|------------------------------------------------------|
| `l.len()`            | Number of elements                                   |
| `l.push(value)`      | Append `value` to the end                            |
| `l.pop()`            | Remove and return the last element                   |
| `l.first()`          | Return first element (or `null` if empty)            |
| `l.last()`           | Return last element (or `null` if empty)             |
| `l.contains(value)`  | `true` if `value` is in the list                     |
| `l.join(sep)`        | Join all elements as strings with separator `sep`    |
| `l.reverse()`        | Return a new reversed list (original is unchanged)   |

```lux
let xs = [3, 1, 2]
xs.push(4)
print(xs.len())        // 4
print(xs.reverse())    // [4, 2, 1, 3]
print(xs.join(", "))   // "3, 1, 2, 4"
```

---

## dict

An unordered mapping from keys to values. Keys must be numbers, strings, or bools.

```lux
let d = {"name": "Alice", "age": 30}
let empty = {}
```

### Indexing & assignment

```lux
d["name"]       // "Alice"
d["age"] = 31
d["newKey"] = true
```

### Dict methods

| Method               | Description                                          |
|----------------------|------------------------------------------------------|
| `d.len()`            | Number of key-value pairs                            |
| `d.has(key)`         | `true` if `key` exists                               |
| `d.get(key, default)`| Value for `key`, or `default` if missing             |
| `d.set(key, value)`  | Insert or update a key-value pair                    |
| `d.remove(key)`      | Remove the entry for `key`                           |
| `d.keys()`           | List of all keys                                     |
| `d.values()`         | List of all values                                   |

```lux
let d = {"x": 1, "y": 2}
print(d.has("x"))          // true
print(d.get("z", 0))       // 0
d.set("z", 3)
print(d.keys())            // ["x", "y", "z"]
d.remove("y")
print(d.len())             // 2
```

---

## object

Objects are bags of named fields. They are created by calling a function that sets fields on `this`.

### Named constructor

```lux
fun Vec2(x, y) {
    this.x = x
    this.y = y
    this.toString = fun() => "(" + str(this.x) + ", " + str(this.y) + ")"
    this.add = fun(other) {
        return Vec2(this.x + other.x, this.y + other.y)
    }
}

let a = Vec2(3, 4)
let b = Vec2(1, 2)
print(a.toString())         // "(3, 4)"
let c = a.add(b)
print(c.toString())         // "(4, 6)"
```

When a function returns without an explicit value and `this` has fields set, `this` is returned automatically. A `ctor` field is also set pointing back to the constructor function, enabling re-invocation via reflection.

### Anonymous object (IIFE)

An immediately-invoked function expression (IIFE) creates a one-off object:

```lux
let point = fun() {
    this.x = 10
    this.y = 20
}()

print(point.x)   // 10
```

### Field access & mutation

```lux
obj.field          // read
obj.field = value  // write
```

---

## function

Functions are first-class values with type `"function"`.

```lux
let f = fun(x) => x * 2
print(typeof(f))   // "function"
```

See [Syntax Reference — Functions](syntax.md#functions) for full details.
