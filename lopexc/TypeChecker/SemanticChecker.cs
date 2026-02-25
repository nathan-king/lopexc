using lopexc.Ast;

namespace lopexc.TypeChecker;

public enum SemanticType
{
    Error,
    Void,
    Bool,
    I32,
    String,
    Char
}

public sealed record Diagnostic(string Message);

public sealed record SemanticFunctionSignature(
    string Name,
    IReadOnlyList<SemanticType> ParameterTypes,
    SemanticType ReturnType,
    bool IsVariadic = false);

public sealed record SemanticResult(
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyDictionary<string, SemanticFunctionSignature> Functions)
{
    public bool HasErrors => Diagnostics.Count > 0;
}

public sealed class SemanticChecker
{
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly Dictionary<string, SemanticFunctionSignature> _functions = new(StringComparer.Ordinal);

    public SemanticResult Check(CompilationUnit unit)
    {
        _diagnostics.Clear();
        _functions.Clear();
        RegisterBuiltins();

        CollectFunctionSignatures(unit);

        foreach (var decl in unit.Declarations)
        {
            switch (decl)
            {
                case FunctionDecl fn:
                    CheckFunction(fn);
                    break;
                case VariableDecl topLevelVar:
                    CheckTopLevelVariable(topLevelVar);
                    break;
            }
        }

        return new SemanticResult(_diagnostics.ToArray(), new Dictionary<string, SemanticFunctionSignature>(_functions));
    }

    private void CollectFunctionSignatures(CompilationUnit unit)
    {
        foreach (var decl in unit.Declarations.OfType<FunctionDecl>())
        {
            if (_functions.ContainsKey(decl.Name))
            {
                AddError($"Duplicate function '{decl.Name}'.");
                continue;
            }

            var paramTypes = new List<SemanticType>(decl.Parameters.Count);
            foreach (var param in decl.Parameters)
                paramTypes.Add(ParseTypeName(param.TypeName));

            var returnType = decl.ReturnType is null ? SemanticType.Error : ParseTypeName(decl.ReturnType);
            _functions[decl.Name] = new SemanticFunctionSignature(decl.Name, paramTypes, returnType);
        }
    }

    private void CheckFunction(FunctionDecl fn)
    {
        var scope = new Scope();

        foreach (var param in fn.Parameters)
        {
            var type = ParseTypeName(param.TypeName);
            if (!scope.Declare(param.Name, type))
                AddError($"Duplicate parameter '{param.Name}' in function '{fn.Name}'.");
        }

        var expectedReturn = fn.ReturnType is null ? SemanticType.Error : ParseTypeName(fn.ReturnType);
        var hasExplicitReturn = fn.ReturnType is not null;

        switch (fn.Body)
        {
            case ExprFunctionBody exprBody:
            {
                var actual = InferExprType(exprBody.Expr, scope);
                if (!hasExplicitReturn)
                    UpdateFunctionReturnType(fn.Name, actual);
                else if (!TypesMatch(expectedReturn, actual))
                    AddError($"Function '{fn.Name}' expects return type '{TypeName(expectedReturn)}' but expression body is '{TypeName(actual)}'.");
                break;
            }
            case BlockFunctionBody blockBody:
            {
                var lastType = InferBlockType(blockBody.Block, scope);
                if (!hasExplicitReturn)
                {
                    UpdateFunctionReturnType(fn.Name, lastType);
                }
                else if (expectedReturn != SemanticType.Void && !TypesMatch(expectedReturn, lastType))
                {
                    AddError(
                        $"Function '{fn.Name}' expects return type '{TypeName(expectedReturn)}' but block returns '{TypeName(lastType)}'.");
                }

                break;
            }
        }
    }

    private void CheckTopLevelVariable(VariableDecl variable)
    {
        var type = variable.TypeName is not null ? ParseTypeName(variable.TypeName) : SemanticType.Error;
        if (variable.Initializer is not null)
        {
            var initType = InferExprType(variable.Initializer, new Scope());
            if (variable.TypeName is not null && !TypesMatch(type, initType))
                AddError($"Top-level variable '{variable.Name}' declared as '{TypeName(type)}' but initialized with '{TypeName(initType)}'.");
        }
    }

    private SemanticType InferBlockType(BlockStmt block, Scope outerScope)
    {
        var scope = new Scope(outerScope);
        var last = SemanticType.Void;

        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableStmt v:
                    CheckVariableStmt(v, scope);
                    last = SemanticType.Void;
                    break;
                case ExprStmt e:
                    last = InferExprType(e.Expr, scope);
                    break;
            }
        }

        return last;
    }

    private void CheckVariableStmt(VariableStmt variable, Scope scope)
    {
        SemanticType declared = SemanticType.Error;
        if (variable.TypeName is not null)
            declared = ParseTypeName(variable.TypeName);

        SemanticType actual = SemanticType.Error;
        if (variable.Initializer is not null)
            actual = InferExprType(variable.Initializer, scope);

        var finalType = variable.TypeName is not null ? declared : actual;
        if (variable.TypeName is null && variable.Initializer is null)
        {
            AddError($"Variable '{variable.Name}' needs a type annotation or initializer.");
            finalType = SemanticType.Error;
        }

        if (variable.TypeName is not null && variable.Initializer is not null && !TypesMatch(declared, actual))
            AddError($"Variable '{variable.Name}' declared as '{TypeName(declared)}' but initialized with '{TypeName(actual)}'.");

        if (!scope.Declare(variable.Name, finalType))
            AddError($"Variable '{variable.Name}' is already defined in this scope.");
    }

    private SemanticType InferExprType(Expr expr, Scope scope)
    {
        switch (expr)
        {
            case IdentifierExpr id:
                if (!scope.TryLookup(id.Name, out var symbolType))
                {
                    AddError($"Unknown identifier '{id.Name}'.");
                    return SemanticType.Error;
                }

                return symbolType;

            case LiteralExpr lit:
                return lit.Kind switch
                {
                    LiteralKind.Integer => SemanticType.I32,
                    LiteralKind.String or LiteralKind.BacktickString => SemanticType.String,
                    LiteralKind.Char => SemanticType.Char,
                    LiteralKind.True or LiteralKind.False => SemanticType.Bool,
                    LiteralKind.Float => SemanticType.Error,
                    _ => SemanticType.Error
                };

            case UnaryExpr unary:
            {
                var operandType = InferExprType(unary.Operand, scope);
                if (unary.Operator == "!")
                {
                    if (!TypesMatch(SemanticType.Bool, operandType))
                        AddError($"Operator '!' requires bool but got '{TypeName(operandType)}'.");
                    return SemanticType.Bool;
                }

                if (unary.Operator == "-")
                {
                    if (!TypesMatch(SemanticType.I32, operandType))
                        AddError($"Unary '-' requires i32 but got '{TypeName(operandType)}'.");
                    return SemanticType.I32;
                }

                AddError($"Unsupported unary operator '{unary.Operator}'.");
                return SemanticType.Error;
            }

            case BinaryExpr binary:
                return InferBinaryType(binary, scope);

            case GroupExpr group:
                return InferExprType(group.Inner, scope);

            case BlockExpr blockExpr:
                return InferBlockType(blockExpr.Block, scope);

            case IfExpr ifExpr:
            {
                var condType = InferExprType(ifExpr.Condition, scope);
                if (!TypesMatch(SemanticType.Bool, condType))
                    AddError($"If condition must be bool, got '{TypeName(condType)}'.");

                var thenType = InferExprType(ifExpr.ThenExpr, scope);
                if (ifExpr.ElseExpr is null)
                    return SemanticType.Void;

                var elseType = InferExprType(ifExpr.ElseExpr, scope);
                if (!TypesMatch(thenType, elseType))
                {
                    AddError($"If branches must have the same type, got '{TypeName(thenType)}' and '{TypeName(elseType)}'.");
                    return SemanticType.Error;
                }

                return thenType;
            }

            case CallExpr call:
                return InferCallType(call, scope);

            case MemberAccessExpr member:
                AddError($"Member access '{member.Member}' is not type-checked yet.");
                return SemanticType.Error;
        }

        AddError($"Unsupported expression kind '{expr.GetType().Name}'.");
        return SemanticType.Error;
    }

    private SemanticType InferBinaryType(BinaryExpr binary, Scope scope)
    {
        var leftType = InferExprType(binary.Left, scope);
        var rightType = InferExprType(binary.Right, scope);

        return binary.Operator switch
        {
            "+" or "-" or "*" or "/" or "%" => RequireBoth(binary.Operator, SemanticType.I32, leftType, rightType, SemanticType.I32),
            "==" or "!=" => RequireComparable(binary.Operator, leftType, rightType),
            "<" or "<=" or ">" or ">=" => RequireBoth(binary.Operator, SemanticType.I32, leftType, rightType, SemanticType.Bool),
            "&&" or "||" => RequireBoth(binary.Operator, SemanticType.Bool, leftType, rightType, SemanticType.Bool),
            _ => ReportUnsupportedBinary(binary.Operator)
        };
    }

    private SemanticType InferCallType(CallExpr call, Scope scope)
    {
        if (call.Callee is not IdentifierExpr id)
        {
            AddError("Only simple function calls are supported (callee must be an identifier).");
            return SemanticType.Error;
        }

        if (!_functions.TryGetValue(id.Name, out var signature))
        {
            AddError($"Unknown function '{id.Name}'.");
            return SemanticType.Error;
        }

        if (!signature.IsVariadic && signature.ParameterTypes.Count != call.Arguments.Count)
            AddError($"Function '{id.Name}' expects {signature.ParameterTypes.Count} arguments but got {call.Arguments.Count}.");

        var checkCount = signature.IsVariadic
            ? Math.Min(call.Arguments.Count, signature.ParameterTypes.Count)
            : Math.Min(call.Arguments.Count, signature.ParameterTypes.Count);

        for (var i = 0; i < checkCount; i++)
        {
            var argType = InferExprType(call.Arguments[i], scope);
            var expected = signature.ParameterTypes[i];
            if (!TypesMatch(expected, argType))
            {
                AddError(
                    $"Argument {i + 1} of '{id.Name}' expects '{TypeName(expected)}' but got '{TypeName(argType)}'.");
            }
        }

        return signature.ReturnType;
    }

    private SemanticType RequireBoth(string op, SemanticType expected, SemanticType left, SemanticType right, SemanticType result)
    {
        if (!TypesMatch(expected, left) || !TypesMatch(expected, right))
        {
            AddError(
                $"Operator '{op}' expects '{TypeName(expected)}' operands but got '{TypeName(left)}' and '{TypeName(right)}'.");
            return SemanticType.Error;
        }

        return result;
    }

    private SemanticType RequireComparable(string op, SemanticType left, SemanticType right)
    {
        if (!TypesMatch(left, right))
        {
            AddError($"Operator '{op}' requires both sides to match, got '{TypeName(left)}' and '{TypeName(right)}'.");
            return SemanticType.Error;
        }

        return SemanticType.Bool;
    }

    private SemanticType ReportUnsupportedBinary(string op)
    {
        AddError($"Unsupported operator '{op}'.");
        return SemanticType.Error;
    }

    private SemanticType ParseTypeName(string name) =>
        name switch
        {
            "i32" => SemanticType.I32,
            "bool" => SemanticType.Bool,
            "string" => SemanticType.String,
            "char" => SemanticType.Char,
            "()" => SemanticType.Void,
            _ => UnknownType(name)
        };

    private SemanticType UnknownType(string name)
    {
        AddError($"Unknown type '{name}'.");
        return SemanticType.Error;
    }

    private static bool TypesMatch(SemanticType expected, SemanticType actual)
    {
        if (expected == SemanticType.Error || actual == SemanticType.Error)
            return true;
        return expected == actual;
    }

    private static string TypeName(SemanticType type) => type switch
    {
        SemanticType.Error => "<error>",
        SemanticType.Void => "()",
        SemanticType.Bool => "bool",
        SemanticType.I32 => "i32",
        SemanticType.String => "string",
        SemanticType.Char => "char",
        _ => "<unknown>"
    };

    private void AddError(string message) => _diagnostics.Add(new Diagnostic(message));

    private void RegisterBuiltins()
    {
        _functions["println"] = new SemanticFunctionSignature(
            "println",
            [SemanticType.String],
            SemanticType.Void,
            IsVariadic: false);
    }

    private void UpdateFunctionReturnType(string name, SemanticType inferred)
    {
        if (_functions.TryGetValue(name, out var sig))
            _functions[name] = sig with { ReturnType = inferred };
    }

    private sealed class Scope
    {
        private readonly Dictionary<string, SemanticType> _symbols = new(StringComparer.Ordinal);
        private readonly Scope? _parent;

        public Scope(Scope? parent = null)
        {
            _parent = parent;
        }

        public bool Declare(string name, SemanticType type) => _symbols.TryAdd(name, type);

        public bool TryLookup(string name, out SemanticType type)
        {
            if (_symbols.TryGetValue(name, out type))
                return true;
            if (_parent is not null)
                return _parent.TryLookup(name, out type);
            type = SemanticType.Error;
            return false;
        }
    }
}
