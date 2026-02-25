namespace lopexc.Lexer;

public static class LexerCore
{
    public static List<Token> Lex(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        int line = 1;
        int col = 1;

        static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
        static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

        bool IsAtEnd() => i >= source.Length;
        char Current() => source[i];
        char Peek(int offset = 1) => i + offset < source.Length ? source[i + offset] : '\0';

        void Advance()
        {
            if (source[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }

            i++;
        }

        while (!IsAtEnd())
        {
            char c = Current();

            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            // // line comments
            if (c == '/' && Peek() == '/')
            {
                Advance();
                Advance();
                while (!IsAtEnd() && Current() != '\n')
                    Advance();
                continue;
            }

            // /* block comments */
            if (c == '/' && Peek() == '*')
            {
                int startLine = line;
                int startCol = col;
                bool closed = false;

                Advance();
                Advance();

                while (!IsAtEnd())
                {
                    if (Current() == '*' && Peek() == '/')
                    {
                        Advance();
                        Advance();
                        closed = true;
                        break;
                    }

                    Advance();
                }

                if (!closed)
                    throw new Exception($"Unterminated block comment at {startLine}:{startCol}");

                continue;
            }

            // Identifier & keyword lexing
            if (IsIdentStart(c))
            {
                int start = i;
                int startCol = col;

                while (!IsAtEnd() && IsIdentPart(Current()))
                    Advance();

                string text = source[start..i];

                var kind = text switch
                {
                    "fn" => TokenKind.Fn,
                    "struct" => TokenKind.Struct,
                    "enum" => TokenKind.Enum,
                    "interface" => TokenKind.Interface,
                    "impl" => TokenKind.Impl,
                    "use" => TokenKind.Use,
                    "extern" => TokenKind.Extern,
                    "match" => TokenKind.Match,
                    "if" => TokenKind.If,
                    "else" => TokenKind.Else,
                    "for" => TokenKind.For,
                    "in" => TokenKind.In,
                    "while" => TokenKind.While,
                    "loop" => TokenKind.Loop,
                    "break" => TokenKind.Break,
                    "continue" => TokenKind.Continue,
                    "var" => TokenKind.Var,
                    "mut" => TokenKind.Mut,
                    "const" => TokenKind.Const,
                    "true" => TokenKind.True,
                    "false" => TokenKind.False,
                    "_" => TokenKind.Underscore,
                    _ => TokenKind.Identifier
                };

                tokens.Add(new Token(kind, text, line, startCol));
                continue;
            }

            // Numeric literals (with optional decimal point and suffix: 42i32, 3.14f64)
            if (char.IsDigit(c))
            {
                int start = i;
                int startCol = col;
                bool isFloat = false;

                while (!IsAtEnd() && char.IsDigit(Current()))
                    Advance();

                if (!IsAtEnd() && Current() == '.' && char.IsDigit(Peek()))
                {
                    isFloat = true;
                    Advance(); // '.'
                    while (!IsAtEnd() && char.IsDigit(Current()))
                        Advance();
                }

                if (!IsAtEnd() && IsIdentStart(Current()))
                {
                    while (!IsAtEnd() && IsIdentPart(Current()))
                        Advance();
                }

                string text = source[start..i];
                tokens.Add(new Token(isFloat ? TokenKind.Float : TokenKind.Integer, text, line, startCol));
                continue;
            }

            // Strings
            if (c == '"')
            {
                int startCol = col;
                Advance(); // opening "
                int start = i;

                while (!IsAtEnd() && Current() != '"')
                {
                    if (Current() == '\\' && i + 1 < source.Length)
                    {
                        Advance();
                        Advance();
                        continue;
                    }

                    Advance();
                }

                if (IsAtEnd())
                    throw new Exception($"Unterminated string at {line}:{startCol}");

                var text = source[start..i];
                Advance(); // closing "
                tokens.Add(new Token(TokenKind.String, text, line, startCol));
                continue;
            }

            // Backtick string literals
            if (c == '`')
            {
                int startCol = col;
                Advance(); // opening `
                int start = i;

                while (!IsAtEnd() && Current() != '`')
                    Advance();

                if (IsAtEnd())
                    throw new Exception($"Unterminated interpolated string at {line}:{startCol}");

                var text = source[start..i];
                Advance(); // closing `
                tokens.Add(new Token(TokenKind.BacktickString, text, line, startCol));
                continue;
            }

            // Character literals
            if (c == '\'')
            {
                int startCol = col;
                Advance(); // opening '

                if (IsAtEnd())
                    throw new Exception($"Unterminated char literal at {line}:{startCol}");

                int valueStart = i;

                if (Current() == '\\')
                {
                    Advance();
                    if (IsAtEnd())
                        throw new Exception($"Unterminated char escape at {line}:{startCol}");
                    Advance();
                }
                else
                {
                    Advance();
                }

                if (IsAtEnd() || Current() != '\'')
                    throw new Exception($"Unterminated char literal at {line}:{startCol}");

                var text = source[valueStart..i];
                Advance(); // closing '
                tokens.Add(new Token(TokenKind.Char, text, line, startCol));
                continue;
            }

            // Operators (multi-char first)
            if (!IsAtEnd())
            {
                string? op = null;
                TokenKind opKind = TokenKind.EOF;

                if (c == '=' && Peek() == '>')
                {
                    op = "=>";
                    opKind = TokenKind.Arrow;
                }
                else if (c == '-' && Peek() == '>')
                {
                    op = "->";
                    opKind = TokenKind.ThinArrow;
                }
                else if (c == '=' && Peek() == '=')
                {
                    op = "==";
                    opKind = TokenKind.Equals;
                }
                else if (c == '!' && Peek() == '=')
                {
                    op = "!=";
                    opKind = TokenKind.BangEquals;
                }
                else if (c == '<' && Peek() == '=')
                {
                    op = "<=";
                    opKind = TokenKind.LessThanEquals;
                }
                else if (c == '>' && Peek() == '=')
                {
                    op = ">=";
                    opKind = TokenKind.GreaterThanEquals;
                }
                else if (c == '+' && Peek() == '=')
                {
                    op = "+=";
                    opKind = TokenKind.PlusEquals;
                }
                else if (c == '-' && Peek() == '=')
                {
                    op = "-=";
                    opKind = TokenKind.MinusEquals;
                }
                else if (c == '&' && Peek() == '&')
                {
                    op = "&&";
                    opKind = TokenKind.AndAnd;
                }
                else if (c == '|' && Peek() == '|')
                {
                    op = "||";
                    opKind = TokenKind.OrOr;
                }

                if (op is not null)
                {
                    tokens.Add(new Token(opKind, op, line, col));
                    Advance();
                    Advance();
                    continue;
                }
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "(", line, col));
                    Advance();
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")", line, col));
                    Advance();
                    continue;
                case '{':
                    tokens.Add(new Token(TokenKind.LBrace, "{", line, col));
                    Advance();
                    continue;
                case '}':
                    tokens.Add(new Token(TokenKind.RBrace, "}", line, col));
                    Advance();
                    continue;
                case '[':
                    tokens.Add(new Token(TokenKind.LBracket, "[", line, col));
                    Advance();
                    continue;
                case ']':
                    tokens.Add(new Token(TokenKind.RBracket, "]", line, col));
                    Advance();
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ",", line, col));
                    Advance();
                    continue;
                case ';':
                    tokens.Add(new Token(TokenKind.Semicolon, ";", line, col));
                    Advance();
                    continue;
                case ':':
                    tokens.Add(new Token(TokenKind.Colon, ":", line, col));
                    Advance();
                    continue;
                case '.':
                    tokens.Add(new Token(TokenKind.Dot, ".", line, col));
                    Advance();
                    continue;
                case '?':
                    tokens.Add(new Token(TokenKind.Question, "?", line, col));
                    Advance();
                    continue;
                case '=':
                    tokens.Add(new Token(TokenKind.Assign, "=", line, col));
                    Advance();
                    continue;
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, "+", line, col));
                    Advance();
                    continue;
                case '-':
                    tokens.Add(new Token(TokenKind.Minus, "-", line, col));
                    Advance();
                    continue;
                case '*':
                    tokens.Add(new Token(TokenKind.Star, "*", line, col));
                    Advance();
                    continue;
                case '/':
                    tokens.Add(new Token(TokenKind.Slash, "/", line, col));
                    Advance();
                    continue;
                case '%':
                    tokens.Add(new Token(TokenKind.Percent, "%", line, col));
                    Advance();
                    continue;
                case '!':
                    tokens.Add(new Token(TokenKind.Bang, "!", line, col));
                    Advance();
                    continue;
                case '<':
                    tokens.Add(new Token(TokenKind.LessThan, "<", line, col));
                    Advance();
                    continue;
                case '>':
                    tokens.Add(new Token(TokenKind.GreaterThan, ">", line, col));
                    Advance();
                    continue;
            }

            throw new Exception($"Unexpected character '{c}' at {line}:{col}");
        }

        tokens.Add(new Token(TokenKind.EOF, "", line, col));
        return tokens;
    }
}
