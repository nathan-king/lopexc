using lopexc.Lexer;
using lopexc.Parser;
using lopexc.TypeChecker;

var sourcePath = args.Length > 0 ? args[0] : Path.Combine("language", "main.lopex");
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
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}
