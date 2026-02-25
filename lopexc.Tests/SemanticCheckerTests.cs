using lopexc.Lexer;
using lopexc.Parser;
using lopexc.TypeChecker;
using Xunit;

namespace lopexc.Tests;

public sealed class SemanticCheckerTests
{
    [Fact]
    public void Check_ValidProgram_HasNoDiagnostics()
    {
        var source = """
                     fn add(a: i32, b: i32) -> i32 => a + b;
                     fn main() -> i32 => add(1, 2);
                     """;

        var result = Run(source);
        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Check_AssignmentTypeMismatch_ReportsError()
    {
        var source = """
                     fn main() -> i32 {
                         var x: i32 = true;
                         x
                     }
                     """;

        var result = Run(source);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Variable 'x' declared as 'i32'"));
    }

    [Fact]
    public void Check_CallArgTypeMismatch_ReportsError()
    {
        var source = """
                     fn take(x: i32) -> i32 => x;
                     fn main() -> i32 => take(false);
                     """;

        var result = Run(source);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Argument 1 of 'take' expects 'i32'"));
    }

    private static SemanticResult Run(string source)
    {
        var tokens = LexerCore.Lex(source);
        var unit = new ParserCore(tokens).ParseCompilationUnit();
        return new SemanticChecker().Check(unit);
    }
}
