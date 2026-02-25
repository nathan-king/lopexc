using lopexc.Lexer;
using lopexc.Parser;
using lopexc.IlGen;
using lopexc.TypeChecker;

var sourcePath = Path.Combine("language", "main.lopex");
string? emitPath = null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--emit")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Missing output path after --emit.");
            Environment.Exit(1);
        }

        emitPath = args[++i];
    }
    else
    {
        sourcePath = args[i];
    }
}

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Source file not found: {sourcePath}");
    Environment.Exit(1);
}

var source = File.ReadAllText(sourcePath);
try
{
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

    Console.WriteLine($"Parsed and type-checked {unit.Declarations.Count} top-level declarations from {sourcePath}");

    if (!string.IsNullOrWhiteSpace(emitPath))
    {
        new CilEmitter().Emit(unit, semantics, emitPath);
        Console.WriteLine($"Emitted CIL assembly to {Path.GetFullPath(emitPath)}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}
