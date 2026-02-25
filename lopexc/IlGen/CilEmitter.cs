using Mono.Cecil;
using Mono.Cecil.Cil;
using lopexc.Ast;
using lopexc.TypeChecker;

namespace lopexc.IlGen;

public sealed class CilEmitter
{
    public void Emit(CompilationUnit unit, SemanticResult semantics, string outputPath)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);
        var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(assemblyName, new Version(1, 0, 0, 0)),
            assemblyName,
            ModuleKind.Console);

        var module = assembly.MainModule;
        var programType = new TypeDefinition(
            string.Empty,
            "Program",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        module.Types.Add(programType);

        if (unit.Declarations.Any(d => d is VariableDecl))
            throw new InvalidOperationException("Top-level variables are not supported by IL emission yet.");

        var functionDecls = unit.Declarations.OfType<FunctionDecl>().ToList();
        var methods = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

        foreach (var fn in functionDecls)
        {
            if (!semantics.Functions.TryGetValue(fn.Name, out var signature))
                throw new InvalidOperationException($"Missing semantic signature for function '{fn.Name}'.");

            var method = new MethodDefinition(
                fn.Name,
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                ToTypeReference(signature.ReturnType, module));

            foreach (var param in fn.Parameters)
            {
                var paramType = ToTypeReference(ParseParamType(param.TypeName), module);
                method.Parameters.Add(new ParameterDefinition(param.Name, ParameterAttributes.None, paramType));
            }

            programType.Methods.Add(method);
            methods[fn.Name] = method;
        }

        foreach (var fn in functionDecls)
        {
            var method = methods[fn.Name];
            var signature = semantics.Functions[fn.Name];
            EmitMethodBody(method, fn, signature, methods);
        }

        if (methods.TryGetValue("main", out var entryPoint))
            assembly.EntryPoint = entryPoint;
        else
            throw new InvalidOperationException("Entry point 'main' was not found.");

        var outputFullPath = Path.GetFullPath(outputPath);
        var outputDir = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        assembly.Write(outputFullPath);
    }

    private void EmitMethodBody(
        MethodDefinition method,
        FunctionDecl fn,
        SemanticFunctionSignature signature,
        IReadOnlyDictionary<string, MethodDefinition> methods)
    {
        method.Body.InitLocals = true;
        var il = method.Body.GetILProcessor();

        var locals = new Dictionary<string, (VariableDefinition Local, SemanticType Type)>(StringComparer.Ordinal);
        var parameters = new Dictionary<string, (int Index, SemanticType Type)>(StringComparer.Ordinal);

        for (var i = 0; i < fn.Parameters.Count; i++)
            parameters[fn.Parameters[i].Name] = (i, ParseParamType(fn.Parameters[i].TypeName));

        var context = new EmitContext(il, method, signature.ReturnType, methods, locals, parameters, signature.ReturnType);
        switch (fn.Body)
        {
            case ExprFunctionBody exprBody:
                EmitExpression(exprBody.Expr, context);
                il.Emit(OpCodes.Ret);
                break;

            case BlockFunctionBody blockBody:
                EmitBlock(blockBody.Block, context);
                break;
        }
    }

    private void EmitBlock(BlockStmt block, EmitContext context)
    {
        if (context.ExpectedReturn == SemanticType.Void)
        {
            foreach (var stmt in block.Statements)
                EmitStatement(stmt, context, isFinalExpressionForReturn: false);

            context.IL.Emit(OpCodes.Ret);
            return;
        }

        if (block.Statements.Count == 0)
            throw new InvalidOperationException("Non-void function must produce a value.");

        for (var i = 0; i < block.Statements.Count; i++)
        {
            var isLast = i == block.Statements.Count - 1;
            EmitStatement(block.Statements[i], context, isLast);
        }

        context.IL.Emit(OpCodes.Ret);
    }

    private void EmitStatement(Stmt stmt, EmitContext context, bool isFinalExpressionForReturn)
    {
        switch (stmt)
        {
            case VariableStmt variable:
                EmitVariable(variable, context);
                return;
            case ExprStmt exprStmt:
            {
                var resultType = EmitExpression(exprStmt.Expr, context);
                if (!isFinalExpressionForReturn || context.ExpectedReturn == SemanticType.Void)
                {
                    if (resultType != SemanticType.Void)
                        context.IL.Emit(OpCodes.Pop);
                }
                else if (resultType != context.ExpectedReturn)
                {
                    throw new InvalidOperationException(
                        $"Block returns '{TypeName(resultType)}' but function expects '{TypeName(context.ExpectedReturn)}'.");
                }

                return;
            }
        }

        throw new InvalidOperationException($"Unsupported statement '{stmt.GetType().Name}'.");
    }

    private void EmitVariable(VariableStmt variable, EmitContext context)
    {
        if (context.Locals.ContainsKey(variable.Name))
            throw new InvalidOperationException($"Duplicate local '{variable.Name}'.");

        var type = variable.TypeName is not null
            ? ParseParamType(variable.TypeName)
            : InferExprType(variable.Initializer ?? throw new InvalidOperationException("Missing variable initializer."), context);

        var local = new VariableDefinition(ToTypeReference(type, context.Method.Module));
        context.Method.Body.Variables.Add(local);
        context.Locals[variable.Name] = (local, type);

        if (variable.Initializer is not null)
        {
            var actual = EmitExpression(variable.Initializer, context);
            if (actual != type && actual != SemanticType.Error)
                throw new InvalidOperationException(
                    $"Variable '{variable.Name}' expected '{TypeName(type)}' but got '{TypeName(actual)}'.");
        }
        else
        {
            EmitDefaultValue(type, context);
        }

        context.IL.Emit(OpCodes.Stloc, local);
    }

    private SemanticType EmitExpression(Expr expr, EmitContext context)
    {
        switch (expr)
        {
            case LiteralExpr literal:
                return EmitLiteral(literal, context);

            case IdentifierExpr identifier:
                return EmitIdentifier(identifier, context);

            case GroupExpr group:
                return EmitExpression(group.Inner, context);

            case UnaryExpr unary:
                return EmitUnary(unary, context);

            case BinaryExpr binary:
                return EmitBinary(binary, context);

            case CallExpr call:
                return EmitCall(call, context);

            case IfExpr ifExpr:
                return EmitIf(ifExpr, context);

            case MatchExpr matchExpr:
                return EmitMatch(matchExpr, context);

            case StructLiteralExpr:
                throw new InvalidOperationException("Struct runtime emission is not implemented yet.");

            case BlockExpr blockExpr:
                return EmitBlockExpr(blockExpr, context);
        }

        throw new InvalidOperationException($"Unsupported expression '{expr.GetType().Name}'.");
    }

    private SemanticType EmitLiteral(LiteralExpr literal, EmitContext context)
    {
        switch (literal.Kind)
        {
            case LiteralKind.Integer:
                context.IL.Emit(OpCodes.Ldc_I4, ParseI32Literal(literal.Value));
                return SemanticType.I32;
            case LiteralKind.True:
                context.IL.Emit(OpCodes.Ldc_I4_1);
                return SemanticType.Bool;
            case LiteralKind.False:
                context.IL.Emit(OpCodes.Ldc_I4_0);
                return SemanticType.Bool;
            case LiteralKind.String:
            case LiteralKind.BacktickString:
                context.IL.Emit(OpCodes.Ldstr, literal.Value);
                return SemanticType.String;
            case LiteralKind.Char:
                context.IL.Emit(OpCodes.Ldc_I4, ParseCharLiteral(literal.Value));
                return SemanticType.Char;
        }

        throw new InvalidOperationException($"Unsupported literal kind '{literal.Kind}'.");
    }

    private SemanticType EmitIdentifier(IdentifierExpr identifier, EmitContext context)
    {
        if (context.Locals.TryGetValue(identifier.Name, out var local))
        {
            context.IL.Emit(OpCodes.Ldloc, local.Local);
            return local.Type;
        }

        if (context.Parameters.TryGetValue(identifier.Name, out var parameter))
        {
            context.IL.Emit(OpCodes.Ldarg, parameter.Index);
            return parameter.Type;
        }

        throw new InvalidOperationException($"Unknown identifier '{identifier.Name}' during IL emission.");
    }

    private SemanticType EmitUnary(UnaryExpr unary, EmitContext context)
    {
        var operandType = EmitExpression(unary.Operand, context);
        switch (unary.Operator)
        {
            case "-":
                if (operandType != SemanticType.I32)
                    throw new InvalidOperationException("Unary '-' currently supports only i32.");
                context.IL.Emit(OpCodes.Neg);
                return SemanticType.I32;

            case "!":
                if (operandType != SemanticType.Bool)
                    throw new InvalidOperationException("Unary '!' currently supports only bool.");
                context.IL.Emit(OpCodes.Ldc_I4_0);
                context.IL.Emit(OpCodes.Ceq);
                return SemanticType.Bool;
        }

        throw new InvalidOperationException($"Unsupported unary operator '{unary.Operator}'.");
    }

    private SemanticType EmitBinary(BinaryExpr binary, EmitContext context)
    {
        var leftType = EmitExpression(binary.Left, context);
        var rightType = EmitExpression(binary.Right, context);

        switch (binary.Operator)
        {
            case "+":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Add);
                return SemanticType.I32;
            case "-":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Sub);
                return SemanticType.I32;
            case "*":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Mul);
                return SemanticType.I32;
            case "/":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Div);
                return SemanticType.I32;
            case "%":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Rem);
                return SemanticType.I32;
            case "==":
                context.IL.Emit(OpCodes.Ceq);
                return SemanticType.Bool;
            case "!=":
                context.IL.Emit(OpCodes.Ceq);
                context.IL.Emit(OpCodes.Ldc_I4_0);
                context.IL.Emit(OpCodes.Ceq);
                return SemanticType.Bool;
            case "<":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Clt);
                return SemanticType.Bool;
            case ">":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Cgt);
                return SemanticType.Bool;
            case "<=":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Cgt);
                context.IL.Emit(OpCodes.Ldc_I4_0);
                context.IL.Emit(OpCodes.Ceq);
                return SemanticType.Bool;
            case ">=":
                RequireTypes(binary.Operator, SemanticType.I32, leftType, rightType);
                context.IL.Emit(OpCodes.Clt);
                context.IL.Emit(OpCodes.Ldc_I4_0);
                context.IL.Emit(OpCodes.Ceq);
                return SemanticType.Bool;
            case "&&":
                RequireTypes(binary.Operator, SemanticType.Bool, leftType, rightType);
                context.IL.Emit(OpCodes.And);
                return SemanticType.Bool;
            case "||":
                RequireTypes(binary.Operator, SemanticType.Bool, leftType, rightType);
                context.IL.Emit(OpCodes.Or);
                return SemanticType.Bool;
        }

        throw new InvalidOperationException($"Unsupported binary operator '{binary.Operator}'.");
    }

    private SemanticType EmitCall(CallExpr call, EmitContext context)
    {
        if (call.Callee is not IdentifierExpr callee)
            throw new InvalidOperationException("Only simple function calls are supported.");

        if (!context.Methods.TryGetValue(callee.Name, out var method))
            throw new InvalidOperationException($"Unknown function '{callee.Name}' during IL emission.");

        if (call.Arguments.Count != method.Parameters.Count)
            throw new InvalidOperationException(
                $"Function '{callee.Name}' expects {method.Parameters.Count} args but got {call.Arguments.Count}.");

        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var argType = EmitExpression(call.Arguments[i], context);
            var expected = ParseTypeFromReference(method.Parameters[i].ParameterType);
            if (argType != expected)
                throw new InvalidOperationException(
                    $"Argument {i + 1} to '{callee.Name}' expected '{TypeName(expected)}' but got '{TypeName(argType)}'.");
        }

        context.IL.Emit(OpCodes.Call, method);
        return ParseTypeFromReference(method.ReturnType);
    }

    private SemanticType EmitIf(IfExpr ifExpr, EmitContext context)
    {
        var conditionType = EmitExpression(ifExpr.Condition, context);
        if (conditionType != SemanticType.Bool)
            throw new InvalidOperationException("If condition must be bool.");

        var elseLabel = Instruction.Create(OpCodes.Nop);
        var endLabel = Instruction.Create(OpCodes.Nop);

        context.IL.Emit(OpCodes.Brfalse, elseLabel);
        var thenType = EmitExpression(ifExpr.ThenExpr, context);

        if (ifExpr.ElseExpr is null)
        {
            if (thenType != SemanticType.Void)
                context.IL.Emit(OpCodes.Pop);
            context.IL.Emit(OpCodes.Br, endLabel);
            context.IL.Append(elseLabel);
            context.IL.Append(endLabel);
            return SemanticType.Void;
        }

        context.IL.Emit(OpCodes.Br, endLabel);
        context.IL.Append(elseLabel);
        var elseType = EmitExpression(ifExpr.ElseExpr, context);

        if (thenType != elseType)
            throw new InvalidOperationException("If branches must have the same type for IL emission.");

        context.IL.Append(endLabel);
        return thenType;
    }

    private SemanticType EmitMatch(MatchExpr matchExpr, EmitContext context)
    {
        if (matchExpr.Arms.Count == 0)
            throw new InvalidOperationException("Match expression must contain at least one arm.");

        var scrutineeType = EmitExpression(matchExpr.Scrutinee, context);
        if (scrutineeType != SemanticType.I32 && scrutineeType != SemanticType.Bool && scrutineeType != SemanticType.Char)
        {
            throw new InvalidOperationException(
                $"Match scrutinee type '{TypeName(scrutineeType)}' is not supported by IL emission yet.");
        }

        var scrutineeLocal = new VariableDefinition(ToTypeReference(scrutineeType, context.Method.Module));
        context.Method.Body.Variables.Add(scrutineeLocal);
        context.IL.Emit(OpCodes.Stloc, scrutineeLocal);

        var endLabel = Instruction.Create(OpCodes.Nop);
        SemanticType? resultType = null;
        var nextArmLabels = new Queue<Instruction>();
        for (var i = 0; i < matchExpr.Arms.Count; i++)
            nextArmLabels.Enqueue(Instruction.Create(OpCodes.Nop));

        for (var i = 0; i < matchExpr.Arms.Count; i++)
        {
            var arm = matchExpr.Arms[i];
            var nextLabel = nextArmLabels.Dequeue();
            if (i > 0)
                context.IL.Append(nextLabel);

            switch (arm.Pattern)
            {
                case WildcardPattern:
                    break;
                case LiteralPattern literalPattern:
                    EmitPatternCompare(scrutineeLocal, scrutineeType, literalPattern, nextArmLabels.TryPeek(out var fallback) ? fallback : null, context);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported match pattern '{arm.Pattern.GetType().Name}'.");
            }

            var armType = EmitExpression(arm.Expr, context);
            resultType ??= armType;
            if (resultType != armType)
                throw new InvalidOperationException("Match arms must return the same type for IL emission.");

            context.IL.Emit(OpCodes.Br, endLabel);
        }

        if (nextArmLabels.Count > 0)
            context.IL.Append(nextArmLabels.Dequeue());

        if (resultType is null)
            throw new InvalidOperationException("Match expression produced no result.");

        context.IL.Append(endLabel);
        return resultType.Value;
    }

    private void EmitPatternCompare(
        VariableDefinition scrutineeLocal,
        SemanticType scrutineeType,
        LiteralPattern literalPattern,
        Instruction? onMismatch,
        EmitContext context)
    {
        var literalType = literalPattern.Literal.Kind switch
        {
            LiteralKind.Integer => SemanticType.I32,
            LiteralKind.True or LiteralKind.False => SemanticType.Bool,
            LiteralKind.Char => SemanticType.Char,
            _ => SemanticType.Error
        };

        if (literalType == SemanticType.Error || literalType != scrutineeType)
        {
            throw new InvalidOperationException(
                $"Match pattern type '{TypeName(literalType)}' does not match scrutinee '{TypeName(scrutineeType)}'.");
        }

        context.IL.Emit(OpCodes.Ldloc, scrutineeLocal);
        EmitLiteral(literalPattern.Literal, context);
        context.IL.Emit(OpCodes.Ceq);

        onMismatch ??= Instruction.Create(OpCodes.Nop);
        context.IL.Emit(OpCodes.Brfalse, onMismatch);
    }

    private SemanticType EmitBlockExpr(BlockExpr blockExpr, EmitContext context)
    {
        _ = blockExpr;
        _ = context;
        throw new InvalidOperationException("Block expressions are not supported by IL emission yet.");
    }

    private static void EmitDefaultValue(SemanticType type, EmitContext context)
    {
        switch (type)
        {
            case SemanticType.I32:
            case SemanticType.Bool:
            case SemanticType.Char:
                context.IL.Emit(OpCodes.Ldc_I4_0);
                return;
            case SemanticType.String:
                context.IL.Emit(OpCodes.Ldnull);
                return;
            case SemanticType.Void:
                throw new InvalidOperationException("Cannot assign default value to void.");
            default:
                throw new InvalidOperationException($"Unsupported default value for type '{type}'.");
        }
    }

    private static int ParseI32Literal(string value)
    {
        var digits = new string(value.TakeWhile(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits) || !int.TryParse(digits, out var parsed))
            throw new InvalidOperationException($"Invalid i32 literal '{value}'.");
        return parsed;
    }

    private static int ParseCharLiteral(string value)
    {
        if (value.Length == 1)
            return value[0];

        if (value.StartsWith('\\'))
        {
            return value switch
            {
                "\\n" => '\n',
                "\\r" => '\r',
                "\\t" => '\t',
                "\\0" => '\0',
                "\\'" => '\'',
                "\\\"" => '"',
                "\\\\" => '\\',
                _ => throw new InvalidOperationException($"Unsupported char escape '{value}'.")
            };
        }

        throw new InvalidOperationException($"Invalid char literal '{value}'.");
    }

    private static void RequireTypes(string op, SemanticType expected, SemanticType left, SemanticType right)
    {
        if (left != expected || right != expected)
            throw new InvalidOperationException(
                $"Operator '{op}' expects '{TypeName(expected)}' operands but got '{TypeName(left)}' and '{TypeName(right)}'.");
    }

    private static string TypeName(SemanticType type) => type switch
    {
        SemanticType.Void => "()",
        SemanticType.Bool => "bool",
        SemanticType.I32 => "i32",
        SemanticType.String => "string",
        SemanticType.Char => "char",
        _ => "<unknown>"
    };

    private static SemanticType ParseParamType(string name) => name switch
    {
        "i32" => SemanticType.I32,
        "bool" => SemanticType.Bool,
        "string" => SemanticType.String,
        "char" => SemanticType.Char,
        "()" => SemanticType.Void,
        _ => throw new InvalidOperationException($"Unsupported parameter type '{name}' for IL emission.")
    };

    private static TypeReference ToTypeReference(SemanticType type, ModuleDefinition module) => type switch
    {
        SemanticType.I32 => module.TypeSystem.Int32,
        SemanticType.Bool => module.TypeSystem.Boolean,
        SemanticType.String => module.TypeSystem.String,
        SemanticType.Char => module.TypeSystem.Char,
        SemanticType.Void => module.TypeSystem.Void,
        _ => throw new InvalidOperationException($"Unsupported semantic type '{type}' for IL emission.")
    };

    private static SemanticType ParseTypeFromReference(TypeReference type) => type.MetadataType switch
    {
        MetadataType.Int32 => SemanticType.I32,
        MetadataType.Boolean => SemanticType.Bool,
        MetadataType.String => SemanticType.String,
        MetadataType.Char => SemanticType.Char,
        MetadataType.Void => SemanticType.Void,
        _ => SemanticType.Error
    };

    private static SemanticType InferExprType(Expr expr, EmitContext context) => expr switch
    {
        LiteralExpr lit => lit.Kind switch
        {
            LiteralKind.Integer => SemanticType.I32,
            LiteralKind.True or LiteralKind.False => SemanticType.Bool,
            LiteralKind.String or LiteralKind.BacktickString => SemanticType.String,
            LiteralKind.Char => SemanticType.Char,
            _ => SemanticType.Error
        },
        IdentifierExpr id when context.Locals.TryGetValue(id.Name, out var local) => local.Type,
        IdentifierExpr id when context.Parameters.TryGetValue(id.Name, out var parameter) => parameter.Type,
        GroupExpr g => InferExprType(g.Inner, context),
        UnaryExpr u when u.Operator == "-" => SemanticType.I32,
        UnaryExpr u when u.Operator == "!" => SemanticType.Bool,
        BinaryExpr b when b.Operator is "+" or "-" or "*" or "/" or "%" => SemanticType.I32,
        BinaryExpr b when b.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" => SemanticType.Bool,
        CallExpr call when call.Callee is IdentifierExpr callee && context.Methods.TryGetValue(callee.Name, out var method) =>
            ParseTypeFromReference(method.ReturnType),
        IfExpr ifExpr when ifExpr.ElseExpr is not null => InferExprType(ifExpr.ThenExpr, context),
        IfExpr => SemanticType.Void,
        MatchExpr matchExpr when matchExpr.Arms.Count > 0 => InferExprType(matchExpr.Arms[0].Expr, context),
        MatchExpr => SemanticType.Error,
        BlockExpr => context.ExpectedReturn,
        _ => SemanticType.Error
    };

    private sealed record EmitContext(
        ILProcessor IL,
        MethodDefinition Method,
        SemanticType MethodReturnType,
        IReadOnlyDictionary<string, MethodDefinition> Methods,
        Dictionary<string, (VariableDefinition Local, SemanticType Type)> Locals,
        Dictionary<string, (int Index, SemanticType Type)> Parameters,
        SemanticType ExpectedReturn);
}
