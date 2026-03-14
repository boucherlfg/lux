using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

#nullable enable
namespace Lux
{
    // ── Callable interface ────────────────────────────────────────────────────

    /// <summary>Anything callable from Lux (user functions, native functions, lambdas).</summary>
    public interface ICallable
    {
        /// <summary>Expected argument count. <c>-1</c> means variadic (any number of args).</summary>
        int Arity { get; }
        object? Call(Interpreter interp, List<object?> args);
    }

    // ── Native (built-in) function ────────────────────────────────────────────

    /// <summary>A built-in function implemented in C# and exposed to Lux scripts.</summary>
    public sealed class NativeFunc : ICallable
    {
        private readonly string _name;
        private readonly int    _arity;
        private readonly Func<Interpreter, List<object?>, object?> _fn;

        public NativeFunc(string name, int arity, Func<Interpreter, List<object?>, object?> fn)
        {
            _name  = name;
            _arity = arity;
            _fn    = fn;
        }

        public int     Arity                                               => _arity;
        public object? Call(Interpreter interp, List<object?> args)       => _fn(interp, args);
        public override string ToString()                                  => $"<native {_name}>";
    }

    // ── User-defined function ─────────────────────────────────────────────────

    /// <summary>A user-defined Lux function with its captured closure environment.</summary>
    public sealed class LuxFunction : ICallable
    {
        private readonly FunDecl       _decl;
        private readonly LuxEnvironment _closure;

        public LuxFunction(FunDecl decl, LuxEnvironment closure)
        {
            _decl    = decl;
            _closure = closure;
        }

        /// <summary>Number of declared parameters.</summary>
        public int           Arity  => _decl.Params.Count;
        /// <summary>Declared parameter names in order.</summary>
        public List<string>  Params => _decl.Params;

        /// <summary>
        /// Call the function with the given arguments.
        /// If <paramref name="receiver"/> is supplied it is bound to <c>this</c>;
        /// otherwise a fresh <see cref="LuxObject"/> is created (constructor pattern).
        /// Named constructors automatically store themselves as the <c>ctor</c> field
        /// on the returned object so reflection can re-invoke them.
        /// </summary>
        public object? Call(Interpreter interp, List<object?> args, LuxObject? receiver = null)
        {
            LuxFunction fn = this;
            while (true)
            {
                var env = new LuxEnvironment(fn._closure);
                // Bind parameters
                for (int i = 0; i < fn._decl.Params.Count; i++)
                    env.Define(fn._decl.Params[i], args[i]);
                // Inject 'this': use the receiver (method call) or a fresh object
                var thisObj = receiver ?? new LuxObject();
                env.Define("this", thisObj);
                try
                {
                    interp.ExecuteBlock(fn._decl.Body.Body, env);
                }
                catch (ReturnException ret) { return ret.Value; }
                catch (TailCallException tc)
                {
                    if (tc.Callee is LuxFunction nextFn)
                    {
                        // Reuse this stack frame — no C# recursion
                        fn       = nextFn;
                        args     = tc.Args;
                        receiver = tc.Receiver;
                        continue;
                    }
                    // Native callee — just call it normally
                    return tc.Callee.Call(interp, tc.Args);
                }
                // Implicit return: if 'this' has any fields, return it (constructor pattern)
                if (thisObj.Fields.Count > 0)
                {
                    if (fn._decl.Name != "(anonymous)")
                        thisObj.Fields["ctor"] = fn;
                    return thisObj;
                }
                return null;
            }
        }

        // ICallable interface — plain call with no receiver
        public object? Call(Interpreter interp, List<object?> args) => Call(interp, args, null);

        public override string ToString() => $"<fun {_decl.Name}>";
    }

    // ── Lux object type ───────────────────────────────────────────────────────

    /// <summary>
    /// A Lux object — a bag of named fields.
    /// Fields may be values, functions (methods), or <see cref="LiveProperty"/> slots
    /// that delegate to C# getters/setters.
    /// Named constructors automatically inject a <c>ctor</c> field pointing back
    /// to the creating function, enabling reflection-based re-construction.
    /// </summary>
    public sealed class LuxObject
    {
        public Dictionary<string, object?> Fields { get; } = new Dictionary<string, object?>();

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var kv in Fields)
            {
                var v = kv.Value is LiveProperty lp ? lp.Getter() : kv.Value;
                parts.Add(kv.Key + ": " + Interpreter.Stringify(v));
            }
            return "{" + string.Join(", ", parts) + "}";
        }
    }

    /// <summary>
    /// A live property slot inside a <see cref="LuxObject"/>.
    /// The getter is invoked every time Lux reads the field;
    /// the setter is invoked every time Lux writes it (null = read-only).
    /// </summary>
    public sealed class LiveProperty
    {
        public Func<object?>    Getter { get; }
        public Action<object?>? Setter { get; }

        public LiveProperty(Func<object?> getter, Action<object?>? setter = null)
        {
            Getter = getter;
            Setter = setter;
        }
    }

    // ── Lux dict type ─────────────────────────────────────────────────────────

    public sealed class LuxDict
    {
        public Dictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public LuxDict() { }

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var kv in Items)
                parts.Add(Interpreter.Stringify(kv.Key) + ": " + Interpreter.Stringify(kv.Value));
            return "{" + string.Join(", ", parts) + "}";
        }
    }

    // ── Lux list type ─────────────────────────────────────────────────────────

    public sealed class LuxList
    {
        public List<object?> Items { get; } = new List<object?>();

        public LuxList() { }
        public LuxList(IEnumerable<object?> items) => Items.AddRange(items);

        public override string ToString()
            => "[" + string.Join(", ", Items.ConvertAll(v => Interpreter.Stringify(v))) + "]";
    }

    // ── Control-flow signals ──────────────────────────────────────────────────

    /// <summary>Thrown internally when a Lux <c>return</c> statement is executed.</summary>
    public sealed class ReturnException   : Exception { public object? Value { get; } public ReturnException(object? v)  { Value = v; } }
    /// <summary>Thrown internally when a Lux <c>break</c> statement is executed.</summary>
    public sealed class BreakException    : Exception { }
    /// <summary>Thrown internally when a Lux <c>continue</c> statement is executed.</summary>
    public sealed class ContinueException : Exception { }
    /// <summary>
    /// Thrown when a Lux <c>throw</c> statement is executed.
    /// Caught by <c>try/catch</c> blocks in Lux scripts; also catchable from C# host code
    /// to intercept script-level errors.
    /// </summary>
    public sealed class LuxThrowException : Exception
    {
        public object?               Value     { get; }
        public int                   Line      { get; }
        /// <summary>Snapshot of the Lux call stack at the point <c>throw</c> was executed.</summary>
        public IReadOnlyList<string> CallStack { get; }
        public LuxThrowException(object? value, int line) : base("")
        {
            Value     = value;
            Line      = line;
            CallStack = Interpreter.CaptureCallStack();
        }
    }

    /// <summary>Thrown internally when a tail call is detected; caught by the <c>LuxFunction</c> trampoline loop.</summary>
    public sealed class TailCallException : Exception
    {
        public ICallable     Callee   { get; }
        public List<object?> Args     { get; }
        public LuxObject?    Receiver { get; }
        public TailCallException(ICallable callee, List<object?> args, LuxObject? receiver)
            : base("") { Callee = callee; Args = args; Receiver = receiver; }
    }

    // ── Interpreter ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tree-walking interpreter for the Lux scripting language.
    ///
    /// Quick-start:
    /// <code>
    ///   var interp = new Interpreter();
    ///   interp.Register("log", (Action&lt;string&gt;)(msg => Console.WriteLine(msg)));
    ///   interp.Run(source);
    ///   double result = interp.CallFunction&lt;double&gt;("myLuxFun", 1, 2);
    /// </code>
    ///
    /// Built-in functions available to scripts:
    /// <list type="bullet">
    ///   <item><c>print(...)</c> — write to stdout</item>
    ///   <item><c>input()</c> — read a line from stdin</item>
    ///   <item><c>str(v)</c> / <c>num(v)</c> — type conversions</item>
    ///   <item><c>type(v)</c> — runtime type name string</item>
    ///   <item><c>len(v)</c> — length of string, list, or dict</item>
    ///   <item><c>range(start?, end, step?)</c> — numeric range list</item>
    ///   <item><c>assert(cond, msg)</c> — runtime assertion</item>
    ///   <item><c>import(path)</c> — load a .lux file relative to <see cref="BasePath"/>; returns a namespace object</item>
    ///   <item><c>getType(val)</c> — reflection: returns a type-descriptor object with <c>fields</c>, <c>hasField</c>, <c>getField</c>, <c>setField</c>, <c>getMethod</c></item>
    /// </list>
    /// </summary>
    public class Interpreter
    {
        private readonly LuxEnvironment _globals = new LuxEnvironment();
        private readonly HashSet<string> _builtinNames = new HashSet<string>();
        private LuxEnvironment _env;

        // ── Call-stack tracking ───────────────────────────────────────────────

        private readonly List<(string Name, int Line)> _callStack = new List<(string, int)>();

        /// <summary>
        /// The interpreter that is currently executing on this thread.
        /// Used by <see cref="LuxError"/> and <see cref="LuxThrowException"/> to snapshot
        /// the Lux call stack at the moment an error is raised.
        /// </summary>
        [System.ThreadStatic]
        private static Interpreter? _currentInterp;

        private void PushCall(string name, int line) => _callStack.Add((name, line));
        private void PopCall() { if (_callStack.Count > 0) _callStack.RemoveAt(_callStack.Count - 1); }

        /// <summary>Returns a list of human-readable stack-frame strings, most-recent first.</summary>
        internal List<string> SnapshotCallStack()
        {
            var frames = new List<string>();
            for (int i = _callStack.Count - 1; i >= 0; i--)
            {
                var (name, line) = _callStack[i];
                frames.Add($"at {name} (line {line})");
            }
            return frames;
        }

        /// <summary>
        /// Captures a call-stack snapshot from the currently active interpreter on this thread.
        /// Returns an empty list when called outside of an interpreter execution.
        /// Called automatically by <see cref="LuxError"/> and <see cref="LuxThrowException"/>.
        /// </summary>
        internal static List<string> CaptureCallStack()
            => _currentInterp?.SnapshotCallStack() ?? new List<string>();

        /// <summary>
        /// Base directory used to resolve relative paths in <c>import()</c> calls.
        /// Set automatically when running a file via <see cref="Run"/>; override if
        /// you supply source code directly from a string.
        /// </summary>
        public string? BasePath { get; set; }

        /// <summary>Canonical path of the file this interpreter is executing. Used for circular-import detection.</summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Canonical paths of every file currently open in the import chain that led to this interpreter.
        /// Populated automatically by <c>import()</c>; seed with the root file's path via <see cref="FilePath"/>.
        /// </summary>
        public HashSet<string> ImportStack { get; set; } = new HashSet<string>();

        /// <summary>
        /// Cache of already-executed modules: canonical path → exported namespace object.
        /// Shared across all child interpreters in the same session so each file runs at most once.
        /// </summary>
        public Dictionary<string, LuxObject> ImportCache { get; set; } = new Dictionary<string, LuxObject>();

        public Interpreter()
        {
            _env = _globals;
            RegisterBuiltins();
        }

        /// <summary>Define or overwrite a global variable accessible to Lux scripts.</summary>
        public void Define(string name, object? value) => _globals.Define(name, value);
        /// <summary>Read the current value of a Lux global variable.</summary>
        public object? GetGlobal(string name)          => _globals.Get(name, 0);

        /// <summary>
        /// Returns the names of all globals defined by user scripts (excludes built-ins such as
        /// <c>print</c>, <c>import</c>, <c>getType</c>, etc.).
        /// Used by <c>import()</c> to build the exported namespace object.
        /// </summary>
        public IEnumerable<string> GetUserGlobalNames()
        {
            foreach (var name in _globals.GetNames())
                if (!_builtinNames.Contains(name)) yield return name;
        }

        // Invoke any Lux callable from C# code
        public object? Invoke(object? callable, List<object?> args)
        {
            if (callable is LuxFunction lf)  return lf.Call(this, args, null);
            if (callable is ICallable fn)    return fn.Call(this, args);
            throw new LuxError("Value is not callable", 0);
        }

        private void RegisterBuiltins()
        {
            void Reg(string name, object? val) { Define(name, val); _builtinNames.Add(name); }

            Reg("print",  new NativeFunc("print",  -1, (_, a) =>
            {
                Console.WriteLine(string.Join(" ", a.ConvertAll(Stringify)));
                return null;
            }));
            Reg("input",  new NativeFunc("input",   0, (_, _) => Console.ReadLine() ?? ""));
            Reg("len",    new NativeFunc("len",     1, (_, a) => LenOf(a[0])));
            Reg("typeof",   new NativeFunc("typeof",    1, (_, a) => TypeOf(a[0])));
            Reg("num",    new NativeFunc("num",     1, (_, a) => ToNum(a[0])));
            Reg("str",    new NativeFunc("str",     1, (_, a) => Stringify(a[0])));
            Reg("range",  new NativeFunc("range",  -1, (_, a) => MakeRange(a)));
            Reg("assert", new NativeFunc("assert",  2, (_, a) =>
            {
                if (!IsTruthy(a[0])) throw new LuxError(Stringify(a[1]), 0);
                return null;
            }));
            Reg("import", new NativeFunc("import",  1, (interp, a) =>
            {
                string relativePath = Stringify(a[0]);
                string baseDir = interp.BasePath ?? Directory.GetCurrentDirectory();
                string fullPath = Path.GetFullPath(Path.IsPathRooted(relativePath)
                    ? relativePath
                    : Path.Combine(baseDir, relativePath));
                if (!File.Exists(fullPath))
                    throw new LuxError($"Cannot import '{relativePath}': file not found", 0);
                // Return cached namespace if this file was already imported
                if (interp.ImportCache.TryGetValue(fullPath, out LuxObject? cached))
                    return cached;
                // Build the ancestor set: everything that was already open, plus the current file
                var childStack = new HashSet<string>(interp.ImportStack);
                if (interp.FilePath != null) childStack.Add(interp.FilePath);
                if (childStack.Contains(fullPath))
                    throw new LuxError($"Circular import detected: '{relativePath}' is already being imported", 0);
                var child = new Interpreter();
                child.BasePath = Path.GetDirectoryName(fullPath);
                child.FilePath = fullPath;
                child.ImportStack = childStack;
                child.ImportCache = interp.ImportCache;
                child.Run(File.ReadAllText(fullPath));
                var ns = new LuxObject();
                foreach (var name in child.GetUserGlobalNames())
                    ns.Fields[name] = child.GetGlobal(name);
                interp.ImportCache[fullPath] = ns;
                return ns;
            }));
            Reg("getType", new NativeFunc("getType", 1, (interp, a) => MakeTypeObject(a[0], interp)));
            Reg("floor",  new NativeFunc("floor",   1, (_, a) => Math.Floor(EnsureNum(a[0], new Token(TokenType.Identifier, "floor", null, 0)))));
        }

        private static LuxObject MakeTypeObject(object? value, Interpreter interp)
        {
            var t = new LuxObject();
            t.Fields["name"] = TypeOf(value);

            // fields(instance) — list field/key names on the passed instance
            t.Fields["fields"] = new NativeFunc("fields", 1, (_, a) =>
            {
                var list = new LuxList();
                if (a[0] is LuxObject o)
                    foreach (var k in o.Fields.Keys) list.Items.Add(k);
                else if (a[0] is LuxDict d)
                    foreach (var k in d.Items.Keys) list.Items.Add(k);
                return list;
            });

            // hasField(instance, name) — bool
            t.Fields["hasField"] = new NativeFunc("hasField", 2, (_, a) =>
            {
                string key = Stringify(a[1]);
                if (a[0] is LuxObject o) return (object)o.Fields.ContainsKey(key);
                if (a[0] is LuxDict d)   return (object)d.Items.ContainsKey(EnsureDictKey(a[1], 0));
                return (object)false;
            });

            // getField(instance, name) — value
            t.Fields["getField"] = new NativeFunc("getField", 2, (_, a) =>
            {
                string key = Stringify(a[1]);
                if (a[0] is LuxObject o)
                {
                    if (!o.Fields.TryGetValue(key, out object? v))
                        throw new LuxError($"Object has no field '{key}'", 0);
                    return v is LiveProperty lp ? lp.Getter() : v;
                }
                if (a[0] is LuxDict d)
                {
                    var dk = EnsureDictKey(a[1], 0);
                    if (!d.Items.TryGetValue(dk, out object? dv))
                        throw new LuxError($"Dict has no key '{key}'", 0);
                    return dv;
                }
                throw new LuxError($"'{TypeOf(a[0])}' does not support getField", 0);
            });

            // setField(instance, name, value) — mutates instance
            t.Fields["setField"] = new NativeFunc("setField", 3, (_, a) =>
            {
                string key = Stringify(a[1]);
                if (a[0] is LuxObject o)
                {
                    if (o.Fields.TryGetValue(key, out object? existing) && existing is LiveProperty lp)
                    {
                        if (lp.Setter == null) throw new LuxError($"Field '{key}' is read-only", 0);
                        lp.Setter(a[2]);
                    }
                    else
                    {
                        o.Fields[key] = a[2];
                    }
                    return null;
                }
                if (a[0] is LuxDict d)
                {
                    d.Items[EnsureDictKey(a[1], 0)] = a[2];
                    return null;
                }
                throw new LuxError($"'{TypeOf(a[0])}' does not support setField", 0);
            });

            // getMethod(instance, name) — returns a MethodInfo object
            t.Fields["getMethod"] = new NativeFunc("getMethod", 2, (_, a) =>
            {
                string key = Stringify(a[1]);
                object? fn = null;
                if (a[0] is LuxObject o)
                {
                    if (!o.Fields.TryGetValue(key, out fn))
                        throw new LuxError($"Object has no method '{key}'", 0);
                    if (fn is LiveProperty lp) fn = lp.Getter();
                }
                else
                {
                    throw new LuxError($"'{TypeOf(a[0])}' does not support getMethod", 0);
                }
                if (fn is not ICallable)
                    throw new LuxError($"Field '{key}' is not callable", 0);

                return MakeMethodInfo(key, fn, interp);
            });

            return t;
        }

        private static LuxObject MakeMethodInfo(string name, object? fn, Interpreter interp)
        {
            var m = new LuxObject();
            m.Fields["name"] = name;

            // getArgs() — returns list of parameter name strings
            m.Fields["getArgs"] = new NativeFunc("getArgs", 0, (_, _) =>
            {
                var list = new LuxList();
                if (fn is LuxFunction lf)
                    foreach (var p in lf.Params) list.Items.Add(p);
                return list;
            });

            // invoke(instance, arg1, arg2, ...) — variadic; first arg is the receiver
            m.Fields["invoke"] = new NativeFunc("invoke", -1, (_, a) =>
            {
                if (a.Count == 0)
                    throw new LuxError("invoke() requires at least one argument (the instance)", 0);
                var receiver = a[0] as LuxObject;
                var callArgs = a.GetRange(1, a.Count - 1);

                if (fn is LuxFunction lf)
                    return lf.Call(interp, callArgs, receiver);
                if (fn is ICallable native)
                    return native.Call(interp, callArgs);
                throw new LuxError($"'{name}' is not callable", 0);
            });

            return m;
        }

        // ── Public interface ──────────────────────────────────────────────────

        public void Run(string source)
        {
            var prev = _currentInterp;
            _currentInterp = this;
            try
            {
                var tokens = new Lexer(source).Tokenize();
                var stmts  = new Parser(tokens).Parse();
                foreach (var s in stmts) Execute(s);
            }
            finally
            {
                _currentInterp = prev;
            }
        }

        /// <summary>
        /// Lexes and parses <paramref name="source"/> without executing it.
        /// Returns the list of syntax errors found; an empty list means the source is valid.
        /// </summary>
        public IReadOnlyList<ValidationError> Validate(string source)
        {
            var errors = new List<ValidationError>();
            var tokens = new Lexer(source).TokenizeAll(errors);
            new Parser(tokens).Parse(errors);
            return errors;
        }

        internal void ExecuteBlock(List<Stmt> stmts, LuxEnvironment env)
        {
            var prev = _env;
            _env = env;
            try   { foreach (var s in stmts) Execute(s); }
            finally { _env = prev; }
        }

        // ── Statement execution ───────────────────────────────────────────────

        internal void Execute(Stmt stmt)
        {
            switch (stmt)
            {
                case ExprStmt s:      Eval(s.Expression);                               break;
                case LetStmt s:       _env.Define(s.Name, Eval(s.Init));               break;
                case FunDecl s:       _env.Define(s.Name, new LuxFunction(s, _env));   break;
                case ReturnStmt s:
                    if (s.Value is CallExpr scx)
                    {
                        var (callee, callArgs, callReceiver) = ResolveCall(scx);
                        throw new TailCallException(callee, callArgs, callReceiver);
                    }
                    throw new ReturnException(s.Value != null ? Eval(s.Value) : null);
                case BreakStmt _:     throw new BreakException();
                case ContinueStmt _:  throw new ContinueException();
                case ThrowStmt s:     throw new LuxThrowException(Eval(s.Value), s.Line);
                case BlockStmt s:     ExecuteBlock(s.Body, new LuxEnvironment(_env));  break;
                case IfStmt s:        ExecuteIf(s);                                     break;
                case WhileStmt s:     ExecuteWhile(s);                                  break;
                case ForStmt s:       ExecuteFor(s);                                    break;
                case TryCatchStmt s:  ExecuteTryCatch(s);                               break;
                case IfLetStmt s:     ExecuteIfLet(s);                                  break;
                default: throw new LuxError($"Unknown statement: {stmt.GetType().Name}", 0);
            }
        }

        private void ExecuteIfLet(IfLetStmt s)
        {
            var val = Eval(s.Init);
            if (IsTruthy(val))
            {
                var env = new LuxEnvironment(_env);
                env.Define(s.Var, val);
                ExecuteBlock(s.Then.Body, env);
            }
            else if (s.Else != null)
            {
                Execute(s.Else);
            }
        }

        private void ExecuteTryCatch(TryCatchStmt s)
        {
            try
            {
                ExecuteBlock(s.Try.Body, new LuxEnvironment(_env));
            }
            catch (LuxThrowException ex)
            {
                var errObj = new LuxObject();
                errObj.Fields["value"] = ex.Value;
                errObj.Fields["line"]  = (double)ex.Line;
                var stackList = new LuxList();
                foreach (var frame in ex.CallStack) stackList.Items.Add(frame);
                errObj.Fields["stacktrace"] = stackList;
                var env = new LuxEnvironment(_env);
                env.Define(s.ErrorVar, errObj);
                ExecuteBlock(s.Catch.Body, env);
            }
            catch (LuxError ex)
            {
                var errObj = new LuxObject();
                errObj.Fields["value"] = ex.Message;
                errObj.Fields["line"]  = (double)ex.Line;
                var stackList = new LuxList();
                foreach (var frame in ex.CallStack) stackList.Items.Add(frame);
                errObj.Fields["stacktrace"] = stackList;
                var env = new LuxEnvironment(_env);
                env.Define(s.ErrorVar, errObj);
                ExecuteBlock(s.Catch.Body, env);
            }
        }

        private void ExecuteIf(IfStmt s)        {
            foreach (var (cond, body) in s.Branches)
            {
                if (IsTruthy(Eval(cond)))
                {
                    ExecuteBlock(body.Body, new LuxEnvironment(_env));
                    return;
                }
            }
            if (s.Else != null) ExecuteBlock(s.Else.Body, new LuxEnvironment(_env));
        }

        private void ExecuteWhile(WhileStmt s)
        {
            while (IsTruthy(Eval(s.Condition)))
            {
                try   { ExecuteBlock(s.Body.Body, new LuxEnvironment(_env)); }
                catch (BreakException)    { break; }
                catch (ContinueException) { continue; }
            }
        }

        private void ExecuteFor(ForStmt s)
        {
            var iterable = Eval(s.Iterable);
            List<object?> items;

            if (iterable is LuxList list)
            {
                items = list.Items;
            }
            else if (iterable is string str)
            {
                items = new List<object?>();
                foreach (char ch in str) items.Add(ch.ToString());
            }
            else
            {
                throw new LuxError($"'{TypeOf(iterable)}' is not iterable", s.Line);
            }

            foreach (var item in items)
            {
                var env = new LuxEnvironment(_env);
                env.Define(s.Var, item);
                try   { ExecuteBlock(s.Body.Body, env); }
                catch (BreakException)    { break; }
                catch (ContinueException) { continue; }
            }
        }

        // ── Expression evaluation ─────────────────────────────────────────────

        internal object? Eval(Expr expr)
        {
            return expr switch
            {
                NumberLit n         => n.Value,
                StringLit s         => s.Value,
                BoolLit b           => b.Value,
                NullLit             => (object?)null,
                ArrayLit a          => new LuxList(a.Elements.ConvertAll(el => Eval(el))),
                DictLit d           => EvalDictLit(d),
                IdentExpr id        => _env.Get(id.Name, id.Line),
                ThisExpr tx         => _env.Get("this", tx.Line),
                FunExpr fx          => new LuxFunction(new FunDecl("(anonymous)", fx.Params, fx.Body, fx.Line), _env),
                AssignExpr ax       => EvalAssign(ax),
                IndexAssignExpr ia  => EvalIndexAssign(ia),
                SetExpr sx          => EvalSet(sx),
                BinaryExpr bx       => EvalBinary(bx),
                LogicalExpr lx      => EvalLogical(lx),
                UnaryExpr ux        => EvalUnary(ux),
                CallExpr cx         => EvalCall(cx),
                GetExpr gx          => EvalGet(gx),
                IndexExpr ix        => EvalIndex(ix),
                _                   => throw new LuxError($"Unknown expression: {expr.GetType().Name}", 0),
            };
        }

        private object? EvalAssign(AssignExpr e)
        {
            var val = Eval(e.Value);
            _env.Set(e.Name, val, e.Line);
            return val;
        }

        private object? EvalIndexAssign(IndexAssignExpr e)
        {
            var obj   = Eval(e.Object);
            var index = Eval(e.Index);
            var val   = Eval(e.Value);
            if (obj is LuxList list)
            {
                list.Items[CheckIndex(index, list.Items.Count, e.Line)] = val;
                return val;
            }
            if (obj is LuxDict dict)
            {
                var key = EnsureDictKey(index, e.Line);
                dict.Items[key] = val;
                return val;
            }
            throw new LuxError("Index assignment only works on lists and dicts", e.Line);
        }

        private object? EvalSet(SetExpr e)
        {
            var obj = Eval(e.Object);
            var val = Eval(e.Value);
            if (obj is LuxObject luxObj)
            {
                if (luxObj.Fields.TryGetValue(e.Property, out var existing) && existing is LiveProperty lp)
                {
                    if (lp.Setter == null) throw new LuxError($"Property '{e.Property}' is read-only", e.Line);
                    lp.Setter(val);
                    return val;
                }
                luxObj.Fields[e.Property] = val;
                return val;
            }
            throw new LuxError($"Cannot set property '{e.Property}' on '{TypeOf(obj)}'", e.Line);
        }

        private object? EvalBinary(BinaryExpr e)
        {
            var left  = Eval(e.Left);
            var right = Eval(e.Right);
            return e.Op.Type switch
            {
                TokenType.Plus         => Add(left, right, e.Op.Line),
                TokenType.Minus        => NumOp(left, right, e.Op, (a, b) => a - b),
                TokenType.Star         => NumOp(left, right, e.Op, (a, b) => a * b),
                TokenType.Slash        => NumOp(left, right, e.Op, (a, b) =>
                {
                    if (b == 0) throw new LuxError("Division by zero", e.Op.Line);
                    return a / b;
                }),
                TokenType.Percent      => NumOp(left, right, e.Op, (a, b) => a % b),
                TokenType.Greater      => NumComp(left, right, e.Op, (a, b) => a > b),
                TokenType.GreaterEqual => NumComp(left, right, e.Op, (a, b) => a >= b),
                TokenType.Less         => NumComp(left, right, e.Op, (a, b) => a < b),
                TokenType.LessEqual    => NumComp(left, right, e.Op, (a, b) => a <= b),
                TokenType.EqualEqual   => IsEqual(left, right),
                TokenType.BangEqual    => !IsEqual(left, right),
                TokenType.BitAnd       => (double)((long)EnsureNum(left, e.Op) &  (long)EnsureNum(right, e.Op)),
                TokenType.BitOr        => (double)((long)EnsureNum(left, e.Op) |  (long)EnsureNum(right, e.Op)),
                TokenType.ShiftLeft    => (double)((long)EnsureNum(left, e.Op) << (int)EnsureNum(right, e.Op)),
                TokenType.ShiftRight   => (double)((long)EnsureNum(left, e.Op) >> (int)EnsureNum(right, e.Op)),
                _ => throw new LuxError($"Unknown operator '{e.Op.Lexeme}'", e.Op.Line),
            };
        }

        private object? EvalLogical(LogicalExpr e)
        {
            var left = Eval(e.Left);
            if (e.Op.Type == TokenType.Or)  return IsTruthy(left) ? left : Eval(e.Right);
            if (e.Op.Type == TokenType.And) return !IsTruthy(left) ? left : Eval(e.Right);
            throw new LuxError("Unknown logical operator", e.Op.Line);
        }

        private object? EvalUnary(UnaryExpr e)
        {
            var right = Eval(e.Right);
            return e.Op.Type switch
            {
                TokenType.Minus  => -(double)EnsureNum(right, e.Op),
                TokenType.Bang   => !IsTruthy(right),
                TokenType.BitNot => (double)(~(long)EnsureNum(right, e.Op)),
                _ => throw new LuxError($"Unknown unary operator '{e.Op.Lexeme}'", e.Op.Line),
            };
        }

        /// <summary>
        /// Resolve a call expression to its callee, evaluated arguments, and optional receiver,
        /// without actually invoking the callee. Used by both <see cref="EvalCall"/> and the
        /// tail-call path in <see cref="Execute"/>.
        /// </summary>
        private (ICallable Callee, List<object?> Args, LuxObject? Receiver) ResolveCall(CallExpr e)
        {
            var args = e.Args.ConvertAll(a => Eval(a));

            if (e.Callee is GetExpr gx)
            {
                var recv = Eval(gx.Object);
                object? method;
                if (recv is LuxObject luxObj)
                {
                    if (!luxObj.Fields.TryGetValue(gx.Property, out method))
                        throw new LuxError($"Object has no method '{gx.Property}'", e.Line);
                }
                else
                {
                    method = EvalGet(gx);
                }
                if (method is not ICallable callable)
                    throw new LuxError($"'{Stringify(method)}' is not callable", e.Line);
                if (callable.Arity != -1 && callable.Arity != args.Count)
                    throw new LuxError($"Expected {callable.Arity} argument(s) but got {args.Count}", e.Line);
                return (callable, args, recv as LuxObject);
            }
            else
            {
                var callee = Eval(e.Callee);
                if (callee is not ICallable fn)
                    throw new LuxError($"'{Stringify(callee)}' is not callable", e.Line);
                if (fn.Arity != -1 && fn.Arity != args.Count)
                    throw new LuxError($"Expected {fn.Arity} argument(s) but got {args.Count}", e.Line);
                return (fn, args, null);
            }
        }

        private object? EvalCall(CallExpr e)
        {
            var (callee, args, receiver) = ResolveCall(e);
            string callName;
            if      (e.Callee is GetExpr   gx) callName = gx.Property;
            else if (e.Callee is IdentExpr ix) callName = ix.Name;
            else                               callName = "<anonymous>";
            PushCall(callName, e.Line);
            try
            {
                if (callee is LuxFunction lf) return lf.Call(this, args, receiver);
                return callee.Call(this, args);
            }
            finally
            {
                PopCall();
            }
        }

        private object? EvalGet(GetExpr e)
        {
            var obj = Eval(e.Object);
            return obj switch
            {
                string s    => GetStringMethod(s, e.Property, e.Line),
                LuxList l   => GetListMethod(l, e.Property, e.Line),
                LuxDict d   => GetDictMethod(d, e.Property, e.Line),
                LuxObject o => o.Fields.TryGetValue(e.Property, out object? v)
                               ? (v is LiveProperty lp ? lp.Getter() : v)
                               : throw new LuxError($"Object has no property '{e.Property}'", e.Line),
                _ => throw new LuxError($"'{TypeOf(obj)}' has no property '{e.Property}'", e.Line),
            };
        }

        private object? EvalIndex(IndexExpr e)
        {
            var obj   = Eval(e.Object);
            var index = Eval(e.Index);
            if (obj is LuxList list) return list.Items[CheckIndex(index, list.Items.Count, e.Line)];
            if (obj is string str)   return str[CheckIndex(index, str.Length, e.Line)].ToString();
            if (obj is LuxDict dict)
            {
                var key = EnsureDictKey(index, e.Line);
                if (dict.Items.TryGetValue(key, out object? v)) return v;
                throw new LuxError($"Key '{Stringify(index)}' not found in dict", e.Line);
            }
            throw new LuxError($"'{TypeOf(obj)}' does not support indexing", e.Line);
        }

        private object? EvalDictLit(DictLit d)
        {
            var dict = new LuxDict();
            foreach (var (k, v) in d.Pairs)
            {
                var key = EnsureDictKey(Eval(k), 0);
                dict.Items[key] = Eval(v);
            }
            return dict;
        }

        // ── Dict methods ──────────────────────────────────────────────────────

        private ICallable GetDictMethod(LuxDict d, string prop, int line)
        {
            switch (prop)
            {
                case "len":
                    return new NativeFunc("len", 0, (_, _) => (double)d.Items.Count);
                case "has":
                    return new NativeFunc("has", 1, (_, a) =>
                        (object)d.Items.ContainsKey(EnsureDictKey(a[0], line)));
                case "get":
                    return new NativeFunc("get", 2, (_, a) =>
                    {
                        var key = EnsureDictKey(a[0], line);
                        return d.Items.TryGetValue(key, out object? v) ? v : a[1];
                    });
                case "set":
                    return new NativeFunc("set", 2, (_, a) =>
                    {
                        d.Items[EnsureDictKey(a[0], line)] = a[1];
                        return null;
                    });
                case "remove":
                    return new NativeFunc("remove", 1, (_, a) =>
                    {
                        d.Items.Remove(EnsureDictKey(a[0], line));
                        return null;
                    });
                case "keys":
                    return new NativeFunc("keys", 0, (_, _) =>
                    {
                        var list = new LuxList();
                        foreach (var k in d.Items.Keys) list.Items.Add(k);
                        return list;
                    });
                case "values":
                    return new NativeFunc("values", 0, (_, _) =>
                    {
                        var list = new LuxList();
                        foreach (var v in d.Items.Values) list.Items.Add(v);
                        return list;
                    });
                default: throw new LuxError($"Dict has no method '{prop}'", line);
            }
        }

        private ICallable GetStringMethod(string s, string prop, int line)
        {
            switch (prop)
            {
                case "len":         return new NativeFunc("len",        0, (_, _) => (double)s.Length);
                case "upper":       return new NativeFunc("upper",      0, (_, _) => s.ToUpper());
                case "lower":       return new NativeFunc("lower",      0, (_, _) => s.ToLower());
                case "trim":        return new NativeFunc("trim",       0, (_, _) => s.Trim());
                case "contains":    return new NativeFunc("contains",   1, (_, a) => (object)s.Contains(Stringify(a[0])));
                case "startsWith":  return new NativeFunc("startsWith", 1, (_, a) => (object)s.StartsWith(Stringify(a[0])));
                case "endsWith":    return new NativeFunc("endsWith",   1, (_, a) => (object)s.EndsWith(Stringify(a[0])));
                case "replace":     return new NativeFunc("replace",    2, (_, a) => s.Replace(Stringify(a[0]), Stringify(a[1])));
                case "split":
                    return new NativeFunc("split", 1, (_, a) =>
                    {
                        var parts  = s.Split(new string[] { Stringify(a[0]) }, StringSplitOptions.None);
                        var result = new LuxList();
                        foreach (var p in parts) result.Items.Add(p);
                        return result;
                    });
                default: throw new LuxError($"String has no method '{prop}'", line);
            }
        }

        // ── List methods ──────────────────────────────────────────────────────

        private ICallable GetListMethod(LuxList l, string prop, int line)
        {
            switch (prop)
            {
                case "len":
                    return new NativeFunc("len", 0, (_, _) => (double)l.Items.Count);
                case "push":
                    return new NativeFunc("push", 1, (_, a) => { l.Items.Add(a[0]); return null; });
                case "pop":
                    return new NativeFunc("pop", 0, (_, _) =>
                    {
                        if (l.Items.Count == 0) throw new LuxError("pop from empty list", line);
                        var v = l.Items[l.Items.Count - 1];
                        l.Items.RemoveAt(l.Items.Count - 1);
                        return v;
                    });
                case "first":
                    return new NativeFunc("first", 0, (_, _) => l.Items.Count > 0 ? l.Items[0] : null);
                case "last":
                    return new NativeFunc("last", 0, (_, _)
                        => l.Items.Count > 0 ? l.Items[l.Items.Count - 1] : null);
                case "contains":
                    return new NativeFunc("contains", 1, (_, a) => (object)l.Items.Exists(x => IsEqual(x, a[0])));
                case "join":
                    return new NativeFunc("join", 1, (_, a)
                        => string.Join(Stringify(a[0]), l.Items.ConvertAll(Stringify)));
                case "reverse":
                    return new NativeFunc("reverse", 0, (_, _) =>
                    {
                        var copy = new LuxList(l.Items);
                        copy.Items.Reverse();
                        return copy;
                    });
                default: throw new LuxError($"List has no method '{prop}'", line);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static object? Add(object? left, object? right, int line)
        {
            if (left is double a && right is double b)   return a + b;
            if (left is string || right is string)        return Stringify(left) + Stringify(right);
            if (left is LuxList la && right is LuxList lb)
            {
                var result = new LuxList(la.Items);
                result.Items.AddRange(lb.Items);
                return result;
            }
            throw new LuxError($"Cannot add '{TypeOf(left)}' and '{TypeOf(right)}'", line);
        }

        private static double NumOp(object? l, object? r, Token op, Func<double, double, double> fn)
            => fn(EnsureNum(l, op), EnsureNum(r, op));

        private static bool NumComp(object? l, object? r, Token op, Func<double, double, bool> fn)
            => fn(EnsureNum(l, op), EnsureNum(r, op));

        private static double EnsureNum(object? v, Token op)
            => v is double d ? d
               : throw new LuxError($"Operand for '{op.Lexeme}' must be a number, got {TypeOf(v)}", op.Line);

        internal static bool IsEqual(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        internal static bool IsTruthy(object? v)
        {
            if (v == null)       return false;
            if (v is bool bl)    return bl;
            if (v is double d)   return d != 0;
            if (v is string s)   return s.Length > 0;
            if (v is LuxList l)  return l.Items.Count > 0;
            if (v is LuxDict dc) return dc.Items.Count > 0;
            if (v is LuxObject)  return true;
            return true;
        }

        private static double LenOf(object? v)
        {
            if (v is string s)  return s.Length;
            if (v is LuxList l) return l.Items.Count;
            if (v is LuxDict d) return d.Items.Count;
            throw new LuxError($"'{TypeOf(v)}' has no length", 0);
        }

        internal static string TypeOf(object? v)
        {
            if (v == null)       return "null";
            if (v is double)     return "number";
            if (v is string)     return "string";
            if (v is bool)       return "bool";
            if (v is LuxList)    return "list";
            if (v is LuxDict)    return "dict";
            if (v is LuxObject)  return "object";
            if (v is ICallable)  return "function";
            return v.GetType().Name;
        }

        private static double ToNum(object? v)
        {
            if (v is double d)  return d;
            if (v is bool b)    return b ? 1 : 0;
            if (v is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double r))
                return r;
            throw new LuxError($"Cannot convert '{TypeOf(v)}' to number", 0);
        }

        private static LuxList MakeRange(List<object?> args)
        {
            double start, end, step;
            switch (args.Count)
            {
                case 1: start = 0;                     end = ToDoubleArg(args[0]); step = 1; break;
                case 2: start = ToDoubleArg(args[0]);  end = ToDoubleArg(args[1]); step = 1; break;
                case 3: start = ToDoubleArg(args[0]);  end = ToDoubleArg(args[1]); step = ToDoubleArg(args[2]); break;
                default: throw new LuxError("range() takes 1-3 arguments", 0);
            }
            var list = new LuxList();
            for (double i = start; step > 0 ? i < end : i > end; i += step)
                list.Items.Add(i);
            return list;
        }

        private static double ToDoubleArg(object? v)
            => v is double d ? d : throw new LuxError("range() arguments must be numbers", 0);

        private static int CheckIndex(object? index, int count, int line)
        {
            if (index is not double d) throw new LuxError("Index must be a number", line);
            int i = (int)d;
            if (i < 0) i += count;
            if (i < 0 || i >= count)
                throw new LuxError($"Index {(int)d} out of range (size {count})", line);
            return i;
        }

        private static object EnsureDictKey(object? v, int line)
        {
            if (v is double || v is string || v is bool)
                return v!;
            throw new LuxError($"Dict keys must be a number, string, or bool — got '{TypeOf(v)}'", line);
        }

        internal static string Stringify(object? v)
        {
            if (v == null)       return "null";
            if (v is double d)   return d % 1 == 0 ? ((long)d).ToString() : d.ToString(CultureInfo.InvariantCulture);
            if (v is bool b)     return b ? "true" : "false";
            if (v is LuxList l)  return l.ToString();
            if (v is LuxDict dc) return dc.ToString();
            if (v is LuxObject o)return o.ToString();
            return v.ToString() ?? "null";
        }
    }
}
#nullable disable