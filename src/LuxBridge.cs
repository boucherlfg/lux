using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Lux
{
    /// <summary>
    /// Utilities for interoperating between Lux scripts and C# host code.
    ///
    /// Quick-start:
    ///   var interp = new Interpreter();
    ///   interp.Register("log", (Action&lt;string&gt;)(msg =&gt; Console.WriteLine(msg)));
    ///   interp.RegisterObject("time", DateTime.Now);
    ///   interp.Run(source);
    ///   double result = interp.CallFunction&lt;double&gt;("myLuxFun", 1, 2);
    /// </summary>
    public static class LuxBridge
    {
        // ── C# → Lux type conversion ──────────────────────────────────────────

        /// <summary>Convert any C# value to a Lux-compatible value.</summary>
        internal static object? ToLux(object? value)
        {
            if (value == null) return null;

            // Already a Lux type — pass through
            if (value is double || value is string || value is bool
                || value is LuxObject || value is LuxList || value is LuxDict
                || value is ICallable)
                return value;

            // Numeric types → double
            if (value is float f)   return (double)f;
            if (value is int i)     return (double)i;
            if (value is long l)    return (double)l;
            if (value is uint ui)   return (double)ui;
            if (value is short sh)  return (double)sh;
            if (value is byte by)   return (double)by;
            if (value is decimal dc)return (double)dc;

            // Dictionary → LuxDict
            if (value is IDictionary dict)
            {
                var luxDict = new LuxDict();
                foreach (DictionaryEntry entry in dict)
                {
                    var k = ToLux(entry.Key);
                    if (k is double || k is string || k is bool)
                        luxDict.Items[k!] = ToLux(entry.Value);
                }
                return luxDict;
            }

            // IEnumerable (non-string) → LuxList
            if (value is IEnumerable enumerable)
            {
                var list = new LuxList();
                foreach (var item in enumerable) list.Items.Add(ToLux(item));
                return list;
            }

            // Arbitrary C# object → LuxObject via reflection (lazy to prevent cyclic overflow)
            return WrapLive(value);
        }

        // ── Lux → C# type conversion ──────────────────────────────────────────

        /// <summary>Convert a Lux value to a C# type T.</summary>
        internal static T FromLux<T>(object? value) => (T)FromLux(value, typeof(T))!;

        /// <summary>Convert a Lux value to a specific C# type (with coercion).</summary>
        internal static object? FromLux(object? value, Type target)
        {
            if (value == null)
                return target.IsValueType ? Activator.CreateInstance(target) : null;

            if (target == typeof(object))  return value;
            if (target == typeof(string))  return Interpreter.Stringify(value);
            if (target == typeof(bool))    return value is bool b ? b : Interpreter.IsTruthy(value);

            // All numeric types come from Lux double
            if (IsNumericType(target))
            {
                double d = value is double dv ? dv : Convert.ToDouble(value);
                return Convert.ChangeType(d, target);
            }

            if (target == typeof(LuxObject) && value is LuxObject lo) return lo;
            if (target == typeof(LuxList)   && value is LuxList ll)   return ll;
            if (target == typeof(LuxDict)   && value is LuxDict ld)   return ld;

            return Convert.ChangeType(value, target);
        }

        // ── Wrap a C# object with live property access ────────────────────────────

        /// <summary>
        /// Like <see cref="Wrap"/>, but properties and public fields are stored as
        /// <see cref="LiveProperty"/> slots so every Lux read/write hits the live C# object.
        /// Also covers public fields, which <see cref="Wrap"/> silently skips.
        /// </summary>
        internal static LuxObject WrapLive(object csObj)
        {
            var obj  = new LuxObject();
            var type = csObj.GetType();

            // Public properties → LiveProperty (getter + optional setter)
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                var p = prop;
                Action<object?>? setter = p.CanWrite
                    ? v => p.SetValue(csObj, FromLux(v, p.PropertyType))
                    : null;
                obj.Fields[p.Name] = new LiveProperty(() => ToLux(p.GetValue(csObj)), setter);
            }

            // Public fields → LiveProperty (always readable; writable unless readonly)
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var f = field;
                Action<object?>? setter = f.IsInitOnly
                    ? null
                    : v => f.SetValue(csObj, FromLux(v, f.FieldType));
                obj.Fields[f.Name] = new LiveProperty(() => ToLux(f.GetValue(csObj)), setter);
            }

            // Public methods (non-property accessors) → NativeFunc (same as Wrap)
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsSpecialName) continue;
                var m     = method;
                var parms = m.GetParameters();
                obj.Fields[m.Name] = new NativeFunc(m.Name, parms.Length, (interp, args) =>
                {
                    var converted = new object?[parms.Length];
                    for (int i = 0; i < parms.Length; i++)
                        converted[i] = FromLux(args[i], parms[i].ParameterType);
                    var result = m.Invoke(csObj, converted);
                    return m.ReturnType == typeof(void) ? null : ToLux(result);
                });
            }

            return obj;
        }

        /// <summary>Register a C# object with fully live property access (read and write).</summary>
        public static void RegisterObjectLive(this Interpreter interp, string name, object csObj)
            => interp.Define(name, WrapLive(csObj));

        /// <summary>
        /// Expose a C# object to Lux by wrapping its public properties and
        /// methods in a LuxObject. Property values are snapshotted at wrap time;
        /// method calls are live (invoked on the original object).
        /// </summary>
        internal static LuxObject Wrap(object csObj)
        {
            var obj  = new LuxObject();
            var type = csObj.GetType();

            // Public readable properties → fields
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                try { obj.Fields[prop.Name] = ToLux(prop.GetValue(csObj, null)); }
                catch { /* skip unreadable */ }
            }

            // Public methods (non-property accessors) → NativeFunc
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsSpecialName) continue; // skip get_X / set_X
                var m      = method;                 // capture for lambda
                var parms  = m.GetParameters();
                obj.Fields[m.Name] = new NativeFunc(m.Name, parms.Length, (interp, args) =>
                {
                    var converted = new object?[parms.Length];
                    for (int i = 0; i < parms.Length; i++)
                        converted[i] = FromLux(args[i], parms[i].ParameterType);
                    var result = m.Invoke(csObj, converted);
                    return m.ReturnType == typeof(void) ? null : ToLux(result);
                });
            }

            return obj;
        }

        // ── Register helpers (extension methods on Interpreter) ───────────────

        /// <summary>
        /// Register a C# delegate as a callable Lux function.
        /// Parameter and return types are automatically converted.
        ///
        /// Examples:
        ///   interp.Register("log",  (Action&lt;string&gt;)(s => Console.WriteLine(s)));
        ///   interp.Register("add",  (Func&lt;double, double, double&gt;)((a, b) => a + b));
        ///   interp.Register("now",  (Func&lt;string&gt;)(() => DateTime.Now.ToString()));
        /// </summary>
        public static void Register(this Interpreter interp, string name, Delegate fn)
        {
            var parms = fn.Method.GetParameters();
            int arity = parms.Length;
            interp.Define(name, new NativeFunc(name, arity, (interpreter, args) =>
            {
                var converted = new object?[arity];
                for (int i = 0; i < arity; i++)
                    converted[i] = FromLux(args[i], parms[i].ParameterType);
                var result = fn.DynamicInvoke(converted);
                return fn.Method.ReturnType == typeof(void) ? null : ToLux(result);
            }));
        }

        /// <summary>
        /// Register a C# object so Lux scripts can access its members.
        ///   interp.RegisterObject("transform", myTransform);
        ///   // Lux: print(transform.Position)
        /// </summary>
        public static void RegisterObject(this Interpreter interp, string name, object csObj)
            => interp.Define(name, Wrap(csObj));

        // ── Call Lux from C# ──────────────────────────────────────────────────

        /// <summary>
        /// Call a named Lux function from C# and get the raw Lux result.
        ///   var raw = interp.CallFunction("onUpdate", deltaTime);
        /// </summary>
        public static object? CallFunction(this Interpreter interp, string name, params object?[] args)
        {
            var fn      = interp.GetGlobal(name);
            var luxArgs = new List<object?>();
            foreach (var a in args) luxArgs.Add(ToLux(a));
            return interp.Invoke(fn, luxArgs);
        }

        /// <summary>
        /// Call a named Lux function and convert the result to T.
        ///   double score = interp.CallFunction&lt;double&gt;("getScore");
        /// </summary>
        public static T CallFunction<T>(this Interpreter interp, string name, params object?[] args)
            => FromLux<T>(CallFunction(interp, name, args));

        // ── Get / set Lux globals from C# ────────────────────────────────────

        /// <summary>Read a global Lux variable and convert it to T.</summary>
        public static T GetGlobal<T>(this Interpreter interp, string name)
            => FromLux<T>(interp.GetGlobal(name));

        /// <summary>Write a C# value into a Lux global variable.</summary>
        public static void SetGlobal(this Interpreter interp, string name, object? value)
            => interp.Define(name, ToLux(value));

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsNumericType(Type t)
            => t == typeof(double) || t == typeof(float)  || t == typeof(int)
            || t == typeof(long)   || t == typeof(uint)   || t == typeof(short)
            || t == typeof(byte)   || t == typeof(decimal);
    }
}
