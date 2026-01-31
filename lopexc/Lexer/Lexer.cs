namespace lopexc.Lexer;

public static class LexerCore
{
    public static List<Token> Lex(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        int line = 1;
        int col = 1;

        while (i < source.Length)
        {
            char c = source[i];
            
            // Skip whitespace
            if (char.IsWhiteSpace(c))
            {
                if (c == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
                
                i++;
                continue;
            }
            
            // Identifier & keyword lexing

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                int startCol = col;

                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                    col++;
                }

                string text = source[start..i];

                var kind = text switch
                {
                    "fn" => TokenKind.Fn,
                    "struct" => TokenKind.Struct,
                    "match" => TokenKind.Match,
                    "if" => TokenKind.If,
                    "for" => TokenKind.For,
                    "var" => TokenKind.Var,
                    "const" => TokenKind.Const,
                    "_" => TokenKind.Underscore,
                    _ => TokenKind.Identifier
                };
                
                tokens.Add(new Token(kind, text, line, startCol));
                continue;

            }
            
            // Add integer literals

            if (char.IsDigit(c))
            {
                int start = i;
                int startCol = col;

                while (i < source.Length && char.IsDigit(source[i]))
                {
                    i++;
                    col++;
                }
                
                string text = source[start..i];
                tokens.Add(new Token(TokenKind.Integer, text, line, startCol));
                continue;
            }
            
            // Operators (multi-char first)

            if (c == '=')
            {
                if (i + 1 < source.Length && source[i + 1] == '>')
                {
                    tokens.Add(new Token(TokenKind.Arrow, "=>", line, col));
                    i += 2; col += 2;
                    continue;
                }
                if (i + 1 < source.Length && source[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.Equals, "==", line, col));
                    i += 2; col += 2;
                    continue;
                }

                tokens.Add(new Token(TokenKind.Assign, "=", line, col));
                i++; col++;
                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "(", line, col++));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")", line, col++));
                    i++;
                    continue;
                case '{':
                    tokens.Add(new Token(TokenKind.LBrace, "{", line, col++));
                    i++;
                    continue;
                case '}':
                    tokens.Add(new Token(TokenKind.RBrace, "}", line, col++));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ",", line, col++));
                    i++;
                    continue;
                case ':':
                    tokens.Add(new Token(TokenKind.Colon, ":", line, col++));
                    i++;
                    continue;
                case ';':
                    tokens.Add(new Token(TokenKind.Semicolon, ";", line, col++));
                    i++;
                    continue;
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, "+", line, col++));
                    i++;
                    continue;
                case '>':
                    tokens.Add(new Token(TokenKind.GreaterThan, ">", line, col++));
                    i++;
                    continue;
                case '<':
                    tokens.Add(new Token(TokenKind.LessThan, "<", line, col++));
                    i++;
                    continue;
            }
            
            // =>

            if (c == '=' && i + 1 < source.Length && source[i + 1] == '>')
            {
                tokens.Add(new Token(TokenKind.Arrow, "=>", line, col));
                i += 2;
                col += 2;
                continue;
            }
            
            // Strings


            if (c == '"')
            {
                int startCol = col;
                i++; col++;
                
                int start = i;

                while (i < source.Length && source[i] != '"')
                {
                    i++;
                    col++;
                }

                if (i >= source.Length)
                    throw new Exception($"Unterminated string at {line}:{startCol}");
                
                var text = source[start..i];
                i++;
                col++;
                tokens.Add(new Token(TokenKind.String, text, line, startCol));
                continue;
            }
            
            // Backtick string literals
            
            if (c == '`')
            {
                int startCol = col;
                i++; col++;

                int start = i;

                while (i < source.Length && source[i] != '`')
                {
                    i++;
                    col++;
                }

                if (i >= source.Length)
                    throw new Exception($"Unterminated interpolated string at {line}:{startCol}");

                var text = source[start..i];
                i++; col++; // consume closing `

                tokens.Add(new Token(TokenKind.BacktickString, text, line, startCol));
                continue;
            }
            
            
            
            throw new Exception($"Unexpected character '{c}'");
        }
        
        tokens.Add(new Token(TokenKind.EOF, "", line, col));
        return tokens;
    }
}