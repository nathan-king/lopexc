using lopexc.Lexer;
using Xunit;

namespace lopexc.Tests;

public sealed class LexerSnapshotTests
{
    [Fact]
    public void Lex_OperatorsAndKeywords_ProducesExpectedTokenKinds()
    {
        var source = """
                     mut var total: i32 = 42i32;
                     if total >= 10 && total != 11 => total += 1;
                     """;

        var tokens = LexerCore.Lex(source);
        var snapshot = string.Join(" ", tokens.Select(t => t.kind.ToString()));

        const string expected =
            "Mut Var Identifier Colon Identifier Assign Integer Semicolon " +
            "If Identifier GreaterThanEquals Integer AndAnd Identifier BangEquals Integer Arrow " +
            "Identifier PlusEquals Integer Semicolon EOF";

        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void Lex_CommentsAndStringForms_SkipsCommentsAndKeepsLiterals()
    {
        var source = """
                     // line comment
                     var a = "x";
                     /* block
                        comment */
                     var b = `sum {a}`;
                     var c = '\n';
                     """;

        var tokens = LexerCore.Lex(source);
        var snapshot = string.Join(" | ", tokens.Select(t => $"{t.kind}:{t.text}"));

        const string expected =
            "Var:var | Identifier:a | Assign:= | String:x | Semicolon:; | " +
            "Var:var | Identifier:b | Assign:= | BacktickString:sum {a} | Semicolon:; | " +
            @"Var:var | Identifier:c | Assign:= | Char:\n | Semicolon:; | EOF:";

        Assert.Equal(expected, snapshot);
    }
}
