namespace lopexc.Lexer;

public enum TokenKind
{
    Identifier,
    Integer,
    String,
    BacktickString,
    Fn,
    Struct,
    Match,
    If,
    For,
    Var,
    Const,
    
    // Symbols
    LParen, // (
    RParen, // )
    LBrace, // {
    RBrace, // "
    Comma,
    Semicolon,
    Colon,
    Arrow,
    DblArrow,
    Underscore,
    Assign, // =
    Equals, // ==
    Plus, // +
    GreaterThan, // >
    LessThan, // <
    EOF
}

public sealed record Token(
    TokenKind kind,
    string text,
    int Line,
    int Column
);