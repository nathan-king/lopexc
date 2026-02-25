namespace lopexc.Ast;

public sealed record CompilationUnit(IReadOnlyList<Decl> Declarations);

public abstract record Decl;

public sealed record FunctionDecl(
    string Name,
    IReadOnlyList<Parameter> Parameters,
    string? ReturnType,
    FunctionBody Body) : Decl;

public sealed record StructDecl(
    string Name,
    IReadOnlyList<StructFieldDecl> Fields) : Decl;

public sealed record StructFieldDecl(string Name, string TypeName);

public sealed record VariableDecl(
    bool IsConst,
    bool IsMutable,
    string Name,
    string? TypeName,
    Expr? Initializer) : Decl;

public sealed record Parameter(string Name, string TypeName);

public abstract record FunctionBody;

public sealed record BlockFunctionBody(BlockStmt Block) : FunctionBody;

public sealed record ExprFunctionBody(Expr Expr) : FunctionBody;

public abstract record Stmt;

public sealed record ExprStmt(Expr Expr) : Stmt;

public sealed record VariableStmt(
    bool IsConst,
    bool IsMutable,
    string Name,
    string? TypeName,
    Expr? Initializer) : Stmt;

public sealed record BlockStmt(IReadOnlyList<Stmt> Statements) : Stmt;

public abstract record Expr;

public sealed record IdentifierExpr(string Name) : Expr;

public enum LiteralKind
{
    Integer,
    Float,
    String,
    Char,
    BacktickString,
    True,
    False
}

public sealed record LiteralExpr(LiteralKind Kind, string Value) : Expr;

public sealed record UnaryExpr(string Operator, Expr Operand) : Expr;

public sealed record BinaryExpr(Expr Left, string Operator, Expr Right) : Expr;

public sealed record CallExpr(Expr Callee, IReadOnlyList<Expr> Arguments) : Expr;

public sealed record MemberAccessExpr(Expr Target, string Member) : Expr;

public sealed record GroupExpr(Expr Inner) : Expr;

public sealed record BlockExpr(BlockStmt Block) : Expr;

public sealed record IfExpr(Expr Condition, Expr ThenExpr, Expr? ElseExpr) : Expr;

public abstract record MatchPattern;

public sealed record WildcardPattern : MatchPattern;

public sealed record LiteralPattern(LiteralExpr Literal) : MatchPattern;

public sealed record MatchArm(MatchPattern Pattern, Expr Expr);

public sealed record MatchExpr(Expr Scrutinee, IReadOnlyList<MatchArm> Arms) : Expr;

public sealed record StructFieldInit(string Name, Expr Value);

public sealed record StructLiteralExpr(string StructName, IReadOnlyList<StructFieldInit> Fields) : Expr;
