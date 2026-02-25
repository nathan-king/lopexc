using lopexc.Ast;
using lopexc.Lexer;

namespace lopexc.Parser;

public sealed class ParserCore
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    public ParserCore(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public CompilationUnit ParseCompilationUnit()
    {
        var declarations = new List<Decl>();

        while (!IsAtEnd())
        {
            if (Match(TokenKind.Fn))
            {
                declarations.Add(ParseFunctionDecl());
                continue;
            }

            if (Match(TokenKind.Struct))
            {
                declarations.Add(ParseStructDecl());
                continue;
            }

            if (Check(TokenKind.Const) || Check(TokenKind.Var) || Check(TokenKind.Mut))
            {
                declarations.Add(ParseVariableDeclTopLevel());
                continue;
            }

            throw Error($"Unexpected token at top level: {Current().kind}");
        }

        return new CompilationUnit(declarations);
    }

    private StructDecl ParseStructDecl()
    {
        var name = Consume(TokenKind.Identifier, "Expected struct name.");
        Consume(TokenKind.LBrace, "Expected '{' after struct name.");

        var fields = new List<StructFieldDecl>();
        while (!Check(TokenKind.RBrace) && !IsAtEnd())
        {
            var fieldName = Consume(TokenKind.Identifier, "Expected field name.");
            Consume(TokenKind.Colon, "Expected ':' after field name.");
            var typeName = ParseTypeNameUntil(TokenKind.Comma, TokenKind.RBrace);
            fields.Add(new StructFieldDecl(fieldName.text, typeName));

            Match(TokenKind.Comma);
        }

        Consume(TokenKind.RBrace, "Expected '}' after struct fields.");
        return new StructDecl(name.text, fields);
    }

    private FunctionDecl ParseFunctionDecl()
    {
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");
        Consume(TokenKind.LParen, "Expected '(' after function name.");

        var parameters = new List<Parameter>();
        if (!Check(TokenKind.RParen))
        {
            do
            {
                var paramName = Consume(TokenKind.Identifier, "Expected parameter name.");
                Consume(TokenKind.Colon, "Expected ':' after parameter name.");
                var typeName = ParseTypeNameUntil(TokenKind.Comma, TokenKind.RParen);
                parameters.Add(new Parameter(paramName.text, typeName));
            } while (Match(TokenKind.Comma));
        }

        Consume(TokenKind.RParen, "Expected ')' after parameters.");

        string? returnType = null;
        if (Match(TokenKind.ThinArrow))
            returnType = ParseTypeNameUntil(TokenKind.Arrow, TokenKind.LBrace, TokenKind.Semicolon);

        FunctionBody body;
        if (Match(TokenKind.Arrow))
        {
            var expr = ParseExpression();
            Consume(TokenKind.Semicolon, "Expected ';' after expression-bodied function.");
            body = new ExprFunctionBody(expr);
        }
        else
        {
            body = new BlockFunctionBody(ParseBlock());
        }

        return new FunctionDecl(nameToken.text, parameters, returnType, body);
    }

    private VariableDecl ParseVariableDeclTopLevel()
    {
        var parsed = ParseVariableDeclCore();
        return new VariableDecl(parsed.IsConst, parsed.IsMutable, parsed.Name, parsed.TypeName, parsed.Initializer);
    }

    private VariableStmt ParseVariableStmt()
    {
        var parsed = ParseVariableDeclCore();
        return new VariableStmt(parsed.IsConst, parsed.IsMutable, parsed.Name, parsed.TypeName, parsed.Initializer);
    }

    private (bool IsConst, bool IsMutable, string Name, string? TypeName, Expr? Initializer) ParseVariableDeclCore()
    {
        bool isMutable = false;
        if (Match(TokenKind.Mut))
            isMutable = true;

        bool isConst = Match(TokenKind.Const);
        if (!isConst)
            Consume(TokenKind.Var, "Expected 'var' or 'const'.");

        var nameToken = Consume(TokenKind.Identifier, "Expected variable name.");

        string? typeName = null;
        if (Match(TokenKind.Colon))
            typeName = ParseTypeNameUntil(TokenKind.Assign, TokenKind.Semicolon);

        Expr? initializer = null;
        if (Match(TokenKind.Assign))
            initializer = ParseExpression();

        Consume(TokenKind.Semicolon, "Expected ';' after variable declaration.");
        return (isConst, isMutable, nameToken.text, typeName, initializer);
    }

    private BlockStmt ParseBlock()
    {
        Consume(TokenKind.LBrace, "Expected '{' to start block.");
        var statements = new List<Stmt>();

        while (!Check(TokenKind.RBrace) && !IsAtEnd())
        {
            if (Check(TokenKind.Const) || Check(TokenKind.Var) || Check(TokenKind.Mut))
            {
                statements.Add(ParseVariableStmt());
                continue;
            }

            var expr = ParseExpression();
            Match(TokenKind.Semicolon);
            statements.Add(new ExprStmt(expr));
        }

        Consume(TokenKind.RBrace, "Expected '}' to end block.");
        return new BlockStmt(statements);
    }

    private Expr ParseExpression(int minPrecedence = 1)
    {
        var left = ParsePrefix();

        while (true)
        {
            if (Check(TokenKind.LParen))
            {
                left = ParseCall(left);
                continue;
            }

            if (Match(TokenKind.Dot))
            {
                var member = Consume(TokenKind.Identifier, "Expected member name after '.'.");
                left = new MemberAccessExpr(left, member.text);
                continue;
            }

            var precedence = GetBinaryPrecedence(Current().kind);
            if (precedence < minPrecedence)
                break;

            var op = Advance();
            var right = ParseExpression(precedence + 1);
            left = new BinaryExpr(left, op.text, right);
        }

        return left;
    }

    private Expr ParsePrefix()
    {
        if (Check(TokenKind.Identifier) && PeekKind() == TokenKind.LBrace && LooksLikeTypeName(Current().text))
            return ParseStructLiteral();

        if (Match(TokenKind.Match))
            return ParseMatchExpr();

        if (Match(TokenKind.If))
            return ParseIfExpr();

        if (Check(TokenKind.LBrace))
            return ParseBlockExpr();

        if (Match(TokenKind.Bang))
            return new UnaryExpr("!", ParseExpression(7));

        if (Match(TokenKind.Minus))
            return new UnaryExpr("-", ParseExpression(7));

        if (Match(TokenKind.LParen))
        {
            var inner = ParseExpression();
            Consume(TokenKind.RParen, "Expected ')' after grouped expression.");
            return new GroupExpr(inner);
        }

        if (Match(TokenKind.Identifier))
            return new IdentifierExpr(Previous().text);

        if (Match(TokenKind.Integer) || Match(TokenKind.Float) || Match(TokenKind.String) ||
            Match(TokenKind.Char) || Match(TokenKind.BacktickString) ||
            Match(TokenKind.True) || Match(TokenKind.False))
        {
            var token = Previous();
            var kind = token.kind switch
            {
                TokenKind.Integer => LiteralKind.Integer,
                TokenKind.Float => LiteralKind.Float,
                TokenKind.String => LiteralKind.String,
                TokenKind.Char => LiteralKind.Char,
                TokenKind.BacktickString => LiteralKind.BacktickString,
                TokenKind.True => LiteralKind.True,
                TokenKind.False => LiteralKind.False,
                _ => throw Error($"Unexpected literal token: {token.kind}")
            };
            return new LiteralExpr(kind, token.text);
        }

        throw Error($"Expected expression but found: {Current().kind}");
    }

    private StructLiteralExpr ParseStructLiteral()
    {
        var structName = Consume(TokenKind.Identifier, "Expected struct name in literal.");
        Consume(TokenKind.LBrace, "Expected '{' in struct literal.");

        var fields = new List<StructFieldInit>();
        while (!Check(TokenKind.RBrace) && !IsAtEnd())
        {
            var fieldName = Consume(TokenKind.Identifier, "Expected field name in struct literal.");
            Consume(TokenKind.Colon, "Expected ':' in struct literal field.");
            var expr = ParseExpression();
            fields.Add(new StructFieldInit(fieldName.text, expr));

            Match(TokenKind.Comma);
        }

        Consume(TokenKind.RBrace, "Expected '}' after struct literal.");
        return new StructLiteralExpr(structName.text, fields);
    }

    private MatchExpr ParseMatchExpr()
    {
        var scrutinee = ParseExpression();
        Consume(TokenKind.LBrace, "Expected '{' after match scrutinee.");

        var arms = new List<MatchArm>();
        while (!Check(TokenKind.RBrace) && !IsAtEnd())
        {
            var pattern = ParseMatchPattern();
            Consume(TokenKind.Arrow, "Expected '=>' in match arm.");
            var expr = ParseExpression();
            arms.Add(new MatchArm(pattern, expr));

            Match(TokenKind.Comma);
            Match(TokenKind.Semicolon);
        }

        Consume(TokenKind.RBrace, "Expected '}' after match arms.");
        return new MatchExpr(scrutinee, arms);
    }

    private MatchPattern ParseMatchPattern()
    {
        if (Match(TokenKind.Underscore))
            return new WildcardPattern();

        if (Match(TokenKind.Integer) || Match(TokenKind.Float) || Match(TokenKind.String) ||
            Match(TokenKind.Char) || Match(TokenKind.BacktickString) ||
            Match(TokenKind.True) || Match(TokenKind.False))
        {
            var token = Previous();
            var kind = token.kind switch
            {
                TokenKind.Integer => LiteralKind.Integer,
                TokenKind.Float => LiteralKind.Float,
                TokenKind.String => LiteralKind.String,
                TokenKind.Char => LiteralKind.Char,
                TokenKind.BacktickString => LiteralKind.BacktickString,
                TokenKind.True => LiteralKind.True,
                TokenKind.False => LiteralKind.False,
                _ => throw Error($"Unexpected literal token in pattern: {token.kind}")
            };

            return new LiteralPattern(new LiteralExpr(kind, token.text));
        }

        throw Error($"Unsupported match pattern token: {Current().kind}");
    }

    private IfExpr ParseIfExpr()
    {
        var condition = ParseExpression();

        Expr thenExpr;
        if (Match(TokenKind.Arrow))
        {
            thenExpr = ParseExpression();
        }
        else
        {
            thenExpr = ParseBlockExpr();
        }

        Expr? elseExpr = null;
        if (Match(TokenKind.Else))
        {
            if (Match(TokenKind.Arrow))
                elseExpr = ParseExpression();
            else if (Match(TokenKind.If))
                elseExpr = ParseIfExpr();
            else
                elseExpr = ParseBlockExpr();
        }

        return new IfExpr(condition, thenExpr, elseExpr);
    }

    private CallExpr ParseCall(Expr callee)
    {
        Consume(TokenKind.LParen, "Expected '(' for function call.");
        var args = new List<Expr>();

        if (!Check(TokenKind.RParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (Match(TokenKind.Comma));
        }

        Consume(TokenKind.RParen, "Expected ')' after arguments.");
        return new CallExpr(callee, args);
    }

    private BlockExpr ParseBlockExpr() => new(ParseBlock());

    private string ParseTypeNameUntil(params TokenKind[] terminators)
    {
        var start = _position;
        while (!IsAtEnd() && !terminators.Contains(Current().kind))
            Advance();

        if (_position == start)
            throw Error("Expected type name.");

        return string.Concat(_tokens.Skip(start).Take(_position - start).Select(t => t.text));
    }

    private static int GetBinaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.OrOr => 1,
        TokenKind.AndAnd => 2,
        TokenKind.Equals or TokenKind.BangEquals => 3,
        TokenKind.LessThan or TokenKind.LessThanEquals or TokenKind.GreaterThan or TokenKind.GreaterThanEquals => 4,
        TokenKind.Plus or TokenKind.Minus => 5,
        TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 6,
        _ => 0
    };

    private bool IsAtEnd() => _position >= _tokens.Count || _tokens[_position].kind == TokenKind.EOF;

    private Token Current() => _tokens[_position];

    private Token Previous() => _tokens[_position - 1];

    private TokenKind PeekKind(int offset = 1)
    {
        var index = _position + offset;
        if (index >= _tokens.Count)
            return TokenKind.EOF;
        return _tokens[index].kind;
    }

    private static bool LooksLikeTypeName(string text) =>
        !string.IsNullOrEmpty(text) && char.IsUpper(text[0]);

    private Token Advance()
    {
        if (!IsAtEnd())
            _position++;
        return Previous();
    }

    private bool Check(TokenKind kind)
    {
        if (_position >= _tokens.Count)
            return kind == TokenKind.EOF;
        if (kind == TokenKind.EOF)
            return _tokens[_position].kind == TokenKind.EOF;
        return !IsAtEnd() && Current().kind == kind;
    }

    private bool Match(TokenKind kind)
    {
        if (!Check(kind))
            return false;
        Advance();
        return true;
    }

    private Token Consume(TokenKind kind, string message)
    {
        if (Check(kind))
            return Advance();
        throw Error(message);
    }

    private Exception Error(string message)
    {
        var token = Current();
        return new Exception($"{message} (line {token.Line}, col {token.Column})");
    }
}
