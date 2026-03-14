using System;
using System.IO;
using System.Collections.Generic;
using Lux;

// ── Entry point ───────────────────────────────────────────────────────────────

if (args.Length > 0)
{
    if (args[0] == "--check")
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("lux: --check requires a file path");
            Environment.Exit(1);
        }
        Environment.Exit(CheckFile(args[1]) ? 0 : 1);
    }
    else
        Environment.Exit(RunFile(args[0]) ? 0 : 1);
}
else
{
    RunRepl();
}

// ── File execution ────────────────────────────────────────────────────────────

static bool RunFile(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"lux: file not found: '{path}'");
        return false;
    }
    var interp = new Interpreter();
    interp.BasePath = Path.GetDirectoryName(Path.GetFullPath(path));
    interp.FilePath = Path.GetFullPath(path);
    return RunSource(File.ReadAllText(path), interp);
}

// ── Syntax check (no execution) ───────────────────────────────────────────────

static bool CheckFile(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"lux: file not found: '{path}'");
        return false;
    }
    var errors = new Interpreter().Validate(File.ReadAllText(path));
    if (errors.Count == 0)
    {
        Console.WriteLine($"{path}: OK");
        return true;
    }
    foreach (var e in errors)
        Console.Error.WriteLine($"{path}: {e}");
    return false;
}

// ── REPL ──────────────────────────────────────────────────────────────────────

static void RunRepl()
{
    Console.WriteLine("Lux 1.0  —  type 'exit' to quit");
    var interp = new Interpreter();
    while (true)
    {
        Console.Write(">> ");
        string? line = Console.ReadLine();
        if (line == null || line.Trim() == "exit") break;
        if (string.IsNullOrWhiteSpace(line)) continue;
        RunSource(line, interp);
    }
}

// ── Shared runner ─────────────────────────────────────────────────────────────

static bool RunSource(string source, Interpreter interp)
{
    try
    {
        interp.Run(source);
        return true;
    }
    catch (LexError e)
    {
        Console.Error.WriteLine($"[Lex Error]     line {e.Line}: {e.Message}");
    }
    catch (ParseError e)
    {
        Console.Error.WriteLine($"[Parse Error]   line {e.Line}: {e.Message}");
    }
    catch (LuxThrowException e)
    {
        Console.Error.WriteLine($"[Unhandled Throw] line {e.Line}: {Interpreter.Stringify(e.Value)}");
        foreach (var frame in e.CallStack) Console.Error.WriteLine("  " + frame);
    }
    catch (LuxError e)
    {
        Console.Error.WriteLine($"[Runtime Error] line {e.Line}: {e.Message}");
        foreach (var frame in e.CallStack) Console.Error.WriteLine("  " + frame);
    }
    return false;
}
