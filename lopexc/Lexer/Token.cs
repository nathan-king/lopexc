namespace lopexc.Lexer;

public enum TokenKind
{
    // Literals and identifiers
    Identifier,
    Integer,
    Float,
    String,
    Char,
    BacktickString,

    // Keywords
    Fn,
    Struct,
    Enum,
    Interface,
    Impl,
    Use,
    Extern,
    Match,
    If,
    Else,
    For,
    In,
    While,
    Loop,
    Break,
    Continue,
    Var,
    Mut,
    Const,
    True,
    False,
    
    // Symbols / delimiters
    LParen, // (
    RParen, // )
    LBrace, // {
    RBrace, // }
    LBracket, // [
    RBracket, // ]
    Comma,
    Semicolon,
    Colon,
    Dot,
    Question,
    Arrow,
    ThinArrow,
    Underscore,

    // Operators
    Assign, // =
    PlusEquals, // +=
    MinusEquals, // -=
    Equals, // ==
    BangEquals, // !=
    Plus, // +
    Minus, // -
    Star, // *
    Slash, // /
    Percent, // %
    Bang, // !
    AndAnd, // &&
    OrOr, // ||
    GreaterThan, // >
    GreaterThanEquals, // >=
    LessThan, // <
    LessThanEquals, // <=

    EOF
}

public sealed record Token(
    TokenKind kind,
    string text,
    int Line,
    int Column
);
