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

`catch` binds the thrown value (or runtime error message) to a variable.

```lux
try {
    // code that might fail
} catch (e) {
    // e holds the thrown value or error message string
}
```

Runtime errors (e.g. division by zero, type errors) are also caught and exposed as a string message.

---

## Examples

### Catching a thrown string

```lux
try {
    throw "something went wrong"
} catch (e) {
    print("Caught: " + e)
}
```

### Catching a thrown object

```lux
try {
    throw {"code": 404, "msg": "not found"}
} catch (e) {
    print("Error " + str(e["code"]) + ": " + e["msg"])
}
```

### Catching a runtime error

```lux
try {
    let x = 1 / 0
} catch (e) {
    print("Runtime error: " + e)
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
    print("Caught: " + e)
}
```

---

## Notes

- There is no `finally` block.
- `catch` catches both explicit `throw` values and interpreter-level runtime errors.
- A `throw` inside a `catch` block will propagate to an outer `try/catch` or terminate the script.
