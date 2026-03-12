# Syntax Reference

## Comments

```lux
// Single-line comment
```

Lux has no multi-line comment syntax.

---

## Variables

Variables are declared with `let`. They are mutable after declaration.

```lux
let x = 42
let name = "Alice"
let active = true
let nothing = null
```

Assignment:

```lux
x = 100
name = "Bob"
```

Compound assignment:

```lux
x += 5   // x = x + 5
x -= 3   // x = x - 3
```

---

## Types

| Type     | Example                        |
|----------|--------------------------------|
| number   | `42`, `3.14`, `-7`             |
| string   | `"hello"`, `"world"`           |
| bool     | `true`, `false`                |
| null     | `null`                         |
| list     | `[1, 2, 3]`                    |
| dict     | `{"key": "value"}`             |
| function | `fun(x) => x * 2`              |
| object   | created by named constructors  |

---

## Operators

### Arithmetic

| Operator | Description    |
|----------|----------------|
| `+`      | Add / concatenate strings or lists |
| `-`      | Subtract       |
| `*`      | Multiply       |
| `/`      | Divide (error on divide-by-zero) |
| `%`      | Modulo         |

### Comparison

| Operator | Description       |
|----------|-------------------|
| `==`     | Equal             |
| `!=`     | Not equal         |
| `<`      | Less than         |
| `<=`     | Less or equal     |
| `>`      | Greater than      |
| `>=`     | Greater or equal  |

### Logical

| Operator | Description                              |
|----------|------------------------------------------|
| `&&`     | And (short-circuits, returns operand)    |
| `\|\|`   | Or  (short-circuits, returns operand)    |
| `!`      | Not                                      |

### Bitwise

Operands are truncated to 64-bit integers before the operation. The result is a number.

| Operator | Description         |
|----------|---------------------|
| `&`      | Bitwise AND         |
| `\|`     | Bitwise OR          |
| `~`      | Bitwise NOT (unary) |
| `<<`     | Left shift          |
| `>>`     | Right shift         |

Operands are truncated to 64-bit integers before the operation. The result is a number.

```lux
12 & 10    // 8   (1100 & 1010 = 1000)
12 | 10    // 14  (1100 | 1010 = 1110)
~0         // -1
~(-1)      // 0
1 << 3     // 8
16 >> 2    // 4
```

Precedence (high → low): `~` (unary) > `<<` `>>` > `&` > `|` > `&&` > `||`

### Unary

```lux
let neg = -x
let inv = !flag
```

---

## Truthiness

| Value              | Truthy? |
|--------------------|---------|
| `null`             | false   |
| `false`            | false   |
| `0`                | false   |
| `""` (empty string)| false   |
| `[]` (empty list)  | false   |
| `{}` (empty dict)  | false   |
| Everything else    | true    |

---

## Control Flow

### if / else if / else

```lux
if (condition) {
    // ...
} else if (other) {
    // ...
} else {
    // ...
}
```

### if-let

Evaluates an expression and binds the result to a variable if it is truthy. Useful for optional values.

```lux
if (let value = someExpression) {
    // value is bound here
} else {
    // expression was falsy
}
```

### while

```lux
let i = 0
while (i < 10) {
    print(i)
    i += 1
}
```

### for ... in

Iterates over a list or the characters of a string.

```lux
for item in [1, 2, 3] {
    print(item)
}

for ch in "hello" {
    print(ch)
}
```

### break / continue

```lux
while (true) {
    if (done) { break }
    if (skip) { continue }
    // ...
}
```

---

## Functions

### Named function declaration

```lux
fun add(a, b) {
    return a + b
}
```

- `return` with no value returns `null`.
- Omitting `return` at the end of a function also returns `null` (unless the constructor pattern applies — see [Objects](types.md#objects)).

### Anonymous function (lambda)

```lux
let square = fun(x) { return x * x }
```

### Arrow shorthand

A single-expression body using `=>`. The expression value is implicitly returned.

```lux
let square = fun(x) => x * x
let double = fun(x) => x * 2
```

### First-class functions

Functions are values — they can be passed, returned, and stored.

```lux
fun apply(f, x) { return f(x) }
print(apply(square, 5))   // 25

fun makeAdder(n) {
    return fun(x) => x + n
}
let add10 = makeAdder(10)
print(add10(7))            // 17
```

### Closures

Functions capture their enclosing scope:

```lux
fun counter() {
    let n = 0
    return fun() {
        n += 1
        return n
    }
}
let c = counter()
print(c())   // 1
print(c())   // 2
```

---

## Blocks

A block `{ ... }` creates a new scope. Variables declared inside are not visible outside.

```lux
{
    let tmp = 42
    print(tmp)
}
// tmp is not accessible here
```

---

## Semicolons

Semicolons are optional. The parser skips them automatically.

---

## String Escape Sequences

| Sequence | Meaning     |
|----------|-------------|
| `\n`     | Newline     |
| `\t`     | Tab         |
| `\\`     | Backslash   |
| `\"`     | Double quote|

---

## Indexing

Lists and strings support zero-based integer indexing. Negative indices count from the end.

```lux
let xs = [10, 20, 30]
print(xs[0])    // 10
print(xs[-1])   // 30

let s = "hello"
print(s[1])     // "e"
```

Dict indexing uses any number, string, or bool key:

```lux
let d = {"x": 1, "y": 2}
print(d["x"])   // 1
d["z"] = 3
```

---

## Property Access

Object fields and dict/list/string methods are accessed with `.`:

```lux
obj.field
obj.method(args)
"hello".upper()
[1,2,3].len()
```
