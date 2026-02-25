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

    [Fact]
    public void Check_MatchExpression_ValidArms_HasNoDiagnostics()
    {
        var source = """
                     fn main() -> i32 => match 2 { 0 => 10, 1 => 11, _ => 99 };
                     """;

        var result = Run(source);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Check_MatchExpression_WildcardMustBeLast_ReportsError()
    {
        var source = """
                     fn main() -> i32 => match 2 { _ => 99, 1 => 11 };
                     """;

        var result = Run(source);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Wildcard match arm must be the last arm"));
    }

    [Fact]
    public void Check_StructLiteral_Valid_HasNoDiagnostics()
    {
        var source = """
                     struct Point { x: i32, y: i32 }
                     fn main() -> i32 {
                         var p: Point = Point { x: 1, y: 2 };
                         0
                     }
                     """;

        var result = Run(source);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Check_StructLiteral_MissingField_ReportsError()
    {
        var source = """
                     struct Point { x: i32, y: i32 }
                     fn main() -> i32 => {
                         var p: Point = Point { x: 1 };
                         0
                     };
                     """;

        var result = Run(source);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Missing field 'y'"));
    }

    private static SemanticResult Run(string source)
    {
        var tokens = LexerCore.Lex(source);
        var unit = new ParserCore(tokens).ParseCompilationUnit();
        return new SemanticChecker().Check(unit);
    }
}
