using System.Diagnostics;
using lopexc.Lexer;
using lopexc.Parser;
using lopexc.IlGen;
using lopexc.TypeChecker;

var command = "build";
var sourcePath = Path.Combine("language", "main.lopex");
string? outputPath = null;

var index = 0;
if (args.Length > 0 && (args[0] is "check" or "build" or "run"))
{
    command = args[0];
    index = 1;
}
else if (args.Length > 0 && (args[0] is "--help" or "-h" or "help"))
{
    PrintUsage();
    return;
}

for (var i = index; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out":
        case "-o":
        case "--emit":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing path after {args[i]}.");
                Environment.Exit(1);
            }

            outputPath = args[++i];
            break;
        case "--help":
        case "-h":
        case "help":
            PrintUsage();
            return;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                PrintUsage();
                Environment.Exit(1);
            }

            sourcePath = args[i];
            break;
    }
}

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Source file not found: {sourcePath}");
    Environment.Exit(1);
}

if (command is "build" or "run")
{
    outputPath ??= Path.Combine("out", $"{Path.GetFileNameWithoutExtension(sourcePath)}.dll");
}

try
{
    var source = File.ReadAllText(sourcePath);
    List<Token> tokens = LexerCore.Lex(source);
    var parser = new ParserCore(tokens);
    var unit = parser.ParseCompilationUnit();

    var semantics = new SemanticChecker().Check(unit);
    if (semantics.HasErrors)
    {
        Console.Error.WriteLine($"Semantic check failed for {sourcePath}:");
        foreach (var diagnostic in semantics.Diagnostics)
            Console.Error.WriteLine($"  - {diagnostic.Message}");
        Environment.Exit(1);
    }

    Console.WriteLine($"Checked {unit.Declarations.Count} top-level declarations from {sourcePath}");

    if (command is "build" or "run")
    {
        new CilEmitter().Emit(unit, semantics, outputPath!);
        Console.WriteLine($"Emitted CIL assembly to {Path.GetFullPath(outputPath!)}");
    }

    if (command == "run")
    {
        RunAssembly(outputPath!);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}

static void RunAssembly(string assemblyPath)
{
    var runtimeConfig = Path.Combine(AppContext.BaseDirectory, "lopexc.runtimeconfig.json");
    if (!File.Exists(runtimeConfig))
    {
        Console.Error.WriteLine($"Runtime config not found: {runtimeConfig}");
        Environment.Exit(1);
    }

    var process = new Process
    {
        StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        }
    };

    process.StartInfo.ArgumentList.Add("exec");
    process.StartInfo.ArgumentList.Add("--runtimeconfig");
    process.StartInfo.ArgumentList.Add(runtimeConfig);
    process.StartInfo.ArgumentList.Add(Path.GetFullPath(assemblyPath));

    process.Start();
    process.WaitForExit();
    Environment.Exit(process.ExitCode);
}

static void PrintUsage()
{
    Console.WriteLine("Lopex compiler");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  lopexc check <source.lopex>");
    Console.WriteLine("  lopexc build <source.lopex> [-o out/app.dll]");
    Console.WriteLine("  lopexc run   <source.lopex> [-o out/app.dll]");
    Console.WriteLine("  lopexc <source.lopex>                     (same as build)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project lopexc -- check language/main.lopex");
    Console.WriteLine("  dotnet run --project lopexc -- build language/match.lopex -o out/match.dll");
    Console.WriteLine("  dotnet run --project lopexc -- run language/mvp.lopex");
}
