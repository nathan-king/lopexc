using lopexc.Lexer;

namespace lopexc.Driver;

public static class CompilerDriver
{
    public static void Compile(string source)
    {
        var tokens = Lexer.LexerCore.Lex(source);

        foreach (Token token in tokens)
            Console.WriteLine(token);
        {
            
        }
        // var ast = Parser.Parse(tokens);
        // var typedAst = TypeChecker.Check(ast);
        // var ir = Lowerer.Lower(typedAst);
        // IlEmitter.Emit(ir);
    }
}