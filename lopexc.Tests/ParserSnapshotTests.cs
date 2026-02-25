using System.Text;
using lopexc.Ast;
using lopexc.Lexer;
using lopexc.Parser;
using Xunit;

namespace lopexc.Tests;

public sealed class ParserSnapshotTests
{
    [Fact]
    public void Parse_TopLevelDeclarations_ProducesExpectedShape()
    {
        var source = """
                     var x: i32 = 2;
                     fn add(a: i32, b: i32) -> i32 => a + b;
                     """;

        var unit = Parse(source);
        var snapshot = DumpCompilationUnit(unit);

        const string expected = """
            VarDecl(const=False, mut=False, name=x, type=i32)
              Init:
                Literal(2)
            Fn(add) -> i32
              Params: a:i32, b:i32
              ExprBody:
                Binary(+)
                  Left:
                    Identifier(a)
                  Right:
                    Identifier(b)
            """;

        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void Parse_ExpressionPrecedence_PrefersMultiplyOverAdd()
    {
        var source = """
                     fn main() => 1 + 2 * 3;
                     """;

        var unit = Parse(source);
        var snapshot = DumpCompilationUnit(unit);

        const string expected = """
            Fn(main) -> <none>
              Params: <none>
              ExprBody:
                Binary(+)
                  Left:
                    Literal(1)
                  Right:
                    Binary(*)
                      Left:
                        Literal(2)
                      Right:
                        Literal(3)
            """;

        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void Parse_IfExpressionArrowForm_InFunctionBody()
    {
        var source = """
                     fn max(a: i32, b: i32) -> i32 => if a > b => a else => b;
                     """;

        var unit = Parse(source);
        var snapshot = DumpCompilationUnit(unit);

        const string expected = """
            Fn(max) -> i32
              Params: a:i32, b:i32
              ExprBody:
                If
                  Condition:
                    Binary(>)
                      Left:
                        Identifier(a)
                      Right:
                        Identifier(b)
                  Then:
                    Identifier(a)
                  Else:
                    Identifier(b)
            """;

        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void Parse_MatchExpression_InFunctionBody()
    {
        var source = """
                     fn classify(x: i32) -> i32 => match x { 0 => 10, 1 => 11, _ => 99 };
                     """;

        var unit = Parse(source);
        var snapshot = DumpCompilationUnit(unit);

        const string expected = """
            Fn(classify) -> i32
              Params: x:i32
              ExprBody:
                Match
                  Scrutinee:
                    Identifier(x)
                  Arm:
                    Pattern: Literal(0)
                    Expr:
                      Literal(10)
                  Arm:
                    Pattern: Literal(1)
                    Expr:
                      Literal(11)
                  Arm:
                    Pattern: Wildcard
                    Expr:
                      Literal(99)
            """;

        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void Parse_StructDeclaration_AndLiteral()
    {
        var source = """
                     struct Point { x: i32, y: i32 }
                     fn main() -> i32 {
                         var p: Point = Point { x: 1, y: 2 };
                         0
                     }
                     """;

        var unit = Parse(source);
        var snapshot = DumpCompilationUnit(unit);

        const string expected = """
            Struct(Point)
              Field: x:i32
              Field: y:i32
            Fn(main) -> i32
              Params: <none>
              BlockBody:
                Block:
                  VarStmt(const=False, mut=False, name=p, type=Point)
                    Init:
                      StructLiteral(Point)
                        FieldInit: x
                          Literal(1)
                        FieldInit: y
                          Literal(2)
                  ExprStmt:
                    Literal(0)
            """;

        Assert.Equal(expected, snapshot);
    }

    private static CompilationUnit Parse(string source)
    {
        var tokens = LexerCore.Lex(source);
        return new ParserCore(tokens).ParseCompilationUnit();
    }

    private static string DumpCompilationUnit(CompilationUnit unit)
    {
        var sb = new StringBuilder();
        foreach (var decl in unit.Declarations)
            DumpDecl(sb, decl, 0);
        return sb.ToString().TrimEnd();
    }

    private static void DumpDecl(StringBuilder sb, Decl decl, int indent)
    {
        switch (decl)
        {
            case VariableDecl v:
                AppendLine(sb, indent, $"VarDecl(const={v.IsConst}, mut={v.IsMutable}, name={v.Name}, type={v.TypeName ?? "<inferred>"})");
                if (v.Initializer is not null)
                {
                    AppendLine(sb, indent + 1, "Init:");
                    DumpExpr(sb, v.Initializer, indent + 2);
                }
                break;
            case FunctionDecl f:
                AppendLine(sb, indent, $"Fn({f.Name}) -> {f.ReturnType ?? "<none>"}");
                AppendLine(sb, indent + 1, f.Parameters.Count == 0
                    ? "Params: <none>"
                    : $"Params: {string.Join(", ", f.Parameters.Select(p => $"{p.Name}:{p.TypeName}"))}");
                switch (f.Body)
                {
                    case ExprFunctionBody exprBody:
                        AppendLine(sb, indent + 1, "ExprBody:");
                        DumpExpr(sb, exprBody.Expr, indent + 2);
                        break;
                    case BlockFunctionBody blockBody:
                        AppendLine(sb, indent + 1, "BlockBody:");
                        DumpStmt(sb, blockBody.Block, indent + 2);
                        break;
                }
                break;
            case StructDecl s:
                AppendLine(sb, indent, $"Struct({s.Name})");
                foreach (var field in s.Fields)
                    AppendLine(sb, indent + 1, $"Field: {field.Name}:{field.TypeName}");
                break;
        }
    }

    private static void DumpStmt(StringBuilder sb, Stmt stmt, int indent)
    {
        switch (stmt)
        {
            case ExprStmt e:
                AppendLine(sb, indent, "ExprStmt:");
                DumpExpr(sb, e.Expr, indent + 1);
                break;
            case VariableStmt v:
                AppendLine(sb, indent, $"VarStmt(const={v.IsConst}, mut={v.IsMutable}, name={v.Name}, type={v.TypeName ?? "<inferred>"})");
                if (v.Initializer is not null)
                {
                    AppendLine(sb, indent + 1, "Init:");
                    DumpExpr(sb, v.Initializer, indent + 2);
                }
                break;
            case BlockStmt b:
                AppendLine(sb, indent, "Block:");
                foreach (var inner in b.Statements)
                    DumpStmt(sb, inner, indent + 1);
                break;
        }
    }

    private static void DumpExpr(StringBuilder sb, Expr expr, int indent)
    {
        switch (expr)
        {
            case IdentifierExpr id:
                AppendLine(sb, indent, $"Identifier({id.Name})");
                break;
            case LiteralExpr l:
                AppendLine(sb, indent, $"Literal({l.Value})");
                break;
            case UnaryExpr u:
                AppendLine(sb, indent, $"Unary({u.Operator})");
                AppendLine(sb, indent + 1, "Operand:");
                DumpExpr(sb, u.Operand, indent + 2);
                break;
            case BinaryExpr b:
                AppendLine(sb, indent, $"Binary({b.Operator})");
                AppendLine(sb, indent + 1, "Left:");
                DumpExpr(sb, b.Left, indent + 2);
                AppendLine(sb, indent + 1, "Right:");
                DumpExpr(sb, b.Right, indent + 2);
                break;
            case CallExpr c:
                AppendLine(sb, indent, "Call");
                AppendLine(sb, indent + 1, "Callee:");
                DumpExpr(sb, c.Callee, indent + 2);
                AppendLine(sb, indent + 1, c.Arguments.Count == 0 ? "Args: <none>" : "Args:");
                foreach (var arg in c.Arguments)
                    DumpExpr(sb, arg, indent + 2);
                break;
            case MemberAccessExpr m:
                AppendLine(sb, indent, $"Member({m.Member})");
                AppendLine(sb, indent + 1, "Target:");
                DumpExpr(sb, m.Target, indent + 2);
                break;
            case GroupExpr g:
                AppendLine(sb, indent, "Group");
                DumpExpr(sb, g.Inner, indent + 1);
                break;
            case BlockExpr blockExpr:
                AppendLine(sb, indent, "BlockExpr:");
                DumpStmt(sb, blockExpr.Block, indent + 1);
                break;
            case IfExpr ifExpr:
                AppendLine(sb, indent, "If");
                AppendLine(sb, indent + 1, "Condition:");
                DumpExpr(sb, ifExpr.Condition, indent + 2);
                AppendLine(sb, indent + 1, "Then:");
                DumpExpr(sb, ifExpr.ThenExpr, indent + 2);
                AppendLine(sb, indent + 1, "Else:");
                if (ifExpr.ElseExpr is null)
                {
                    AppendLine(sb, indent + 2, "<none>");
                }
                else
                {
                    DumpExpr(sb, ifExpr.ElseExpr, indent + 2);
                }
                break;
            case MatchExpr matchExpr:
                AppendLine(sb, indent, "Match");
                AppendLine(sb, indent + 1, "Scrutinee:");
                DumpExpr(sb, matchExpr.Scrutinee, indent + 2);
                foreach (var arm in matchExpr.Arms)
                {
                    AppendLine(sb, indent + 1, "Arm:");
                    AppendLine(sb, indent + 2, $"Pattern: {DumpPattern(arm.Pattern)}");
                    AppendLine(sb, indent + 2, "Expr:");
                    DumpExpr(sb, arm.Expr, indent + 3);
                }
                break;
            case StructLiteralExpr structLiteral:
                AppendLine(sb, indent, $"StructLiteral({structLiteral.StructName})");
                foreach (var field in structLiteral.Fields)
                {
                    AppendLine(sb, indent + 1, $"FieldInit: {field.Name}");
                    DumpExpr(sb, field.Value, indent + 2);
                }
                break;
        }
    }

    private static string DumpPattern(MatchPattern pattern) => pattern switch
    {
        WildcardPattern => "Wildcard",
        LiteralPattern lp => $"Literal({lp.Literal.Value})",
        _ => "<unknown>"
    };

    private static void AppendLine(StringBuilder sb, int indent, string line)
    {
        sb.Append(' ', indent * 2);
        sb.AppendLine(line);
    }
}
