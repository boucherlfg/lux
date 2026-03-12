# Modules

Lux scripts can be split into multiple files and loaded with `import()`.

---

## import(path)

```lux
let mod = import("path/to/file.lux")
```

- The path is resolved relative to the importing script's directory.
- The target file is executed in its own interpreter instance.
- All top-level `let` and `fun` declarations become fields on the returned namespace object.
- Built-in functions (`print`, `import`, etc.) are not exported.

---

## Defining a module

Any `.lux` file can act as a module. Simply define top-level variables and functions.

**mathutils.lux**
```lux
let PI = 3.14159265358979

fun add(a, b) { return a + b }
fun mul(a, b) { return a * b }
fun clamp(v, lo, hi) {
    if (v < lo) { return lo }
    if (v > hi) { return hi }
    return v
}
fun sum(list) {
    let total = 0
    for n in list { total += n }
    return total
}
```

---

## Using a module

```lux
let math = import("mathutils.lux")

print(math.add(3, 4))            // 7
print(math.mul(6, 7))            // 42
print(math.clamp(15, 0, 10))     // 10
print(math.sum([1, 2, 3, 4, 5])) // 15
print("PI = " + str(math.PI))    // PI = 3.14159265358979
```

---

## Error handling

A failed import (file not found) throws a runtime error that can be caught:

```lux
try {
    let mod = import("missing.lux")
} catch (e) {
    print("Import failed: " + e)
}
```

---

## Notes

- Circular imports are detected and raise a runtime error: `Circular import detected: '...' is already being imported`.
- Each `import()` call re-executes the module file from scratch (no caching).
