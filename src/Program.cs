using System;
using System.IO;
using System.Collections.Generic;
using Lux;

// ── Entry point ───────────────────────────────────────────────────────────────

if (args.Length > 0)
{
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
    catch (LexError e)   { Console.Error.WriteLine($"[Lex Error]     line {e.Line}: {e.Message}"); }
    catch (ParseError e) { Console.Error.WriteLine($"[Parse Error]   line {e.Line}: {e.Message}"); }
    catch (LuxError e)   { Console.Error.WriteLine($"[Runtime Error] line {e.Line}: {e.Message}"); }
    return false;
}
