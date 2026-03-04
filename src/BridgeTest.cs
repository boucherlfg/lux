using System;
using System.Collections.Generic;

namespace Lux
{
    /// <summary>
    /// Demonstrates the C# ↔ Lux bridge API.
    /// Run with: lux --bridge-test
    /// </summary>
    internal static class BridgeTest
    {
        // A sample C# class to expose to Lux via reflection
        private sealed class Vector3
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
            public override string ToString() => $"({X}, {Y}, {Z})";
        }

        internal static void Run()
        {
            Console.WriteLine("=== Lux Bridge Test ===\n");
            var interp = new Interpreter();

            // 1. Register C# delegates as Lux functions
            interp.Register("csLog",  (Action<string>)(msg => Console.WriteLine("[C#] " + msg)));
            interp.Register("csMul",  (Func<double, double, double>)((a, b) => a * b));
            interp.Register("csNow",  (Func<string>)(() => DateTime.Now.ToString("HH:mm:ss")));
            interp.Register("csSqrt", (Func<double, double>)(n => Math.Sqrt(n)));

            // 2. Register a C# object (reflected: properties + methods exposed)
            interp.RegisterObject("vec", new Vector3(1.0, 2.0, 3.0));

            // 3. Push a value into a Lux global from C#
            interp.SetGlobal("gravity", -9.81);

            // 4. Run Lux source that uses all of the above
            const string source = @"
csLog(""hello from Lux"")
print(""6 * 7 = "" + str(csMul(6, 7)))
print(""time = ""  + csNow())
print(""sqrt(144) = "" + str(csSqrt(144)))

print(""vec.X = "" + str(vec.X))
print(""vec.Length = "" + str(vec.Length()))

print(""gravity = "" + str(gravity))

fun onEvent(name, value) {
    return ""got event: "" + name + "" = "" + str(value)
}

fun sumList(items) {
    let total = 0
    for x in items { total += x }
    return total
}
";
            interp.Run(source);

            // 5. Call a Lux function from C# (returns a typed value)
            string result = interp.CallFunction<string>("onEvent", "score", 42.0);
            Console.WriteLine("[C#] " + result);

            // 6. Pass a C# List<double> — auto-converted to LuxList
            double total = interp.CallFunction<double>("sumList", new List<double> { 1, 2, 3, 4, 5 });
            Console.WriteLine("[C#] sum = " + total);

            // 7. Read a Lux global back into C#
            double g = interp.GetGlobal<double>("gravity");
            Console.WriteLine("[C#] gravity = " + g);

            Console.WriteLine("\n=== Bridge Test Passed ===");
        }
    }
}
