using Mono.Cecil;
using lopexc.IlGen;
using lopexc.Lexer;
using lopexc.Parser;
using lopexc.TypeChecker;
using Xunit;

namespace lopexc.Tests;

public sealed class CilEmitterTests
{
    [Fact]
    public void Emit_MvpProgram_WritesAssemblyWithMainEntryPoint()
    {
        var source = """
                     fn add(a: i32, b: i32) -> i32 => a + b;
                     fn main() -> i32 => add(2, 3) * 4;
                     """;

        var tokens = LexerCore.Lex(source);
        var unit = new ParserCore(tokens).ParseCompilationUnit();
        var semantics = new SemanticChecker().Check(unit);
        Assert.False(semantics.HasErrors);

        var outputPath = Path.Combine(Path.GetTempPath(), $"lopex-test-{Guid.NewGuid():N}.dll");
        try
        {
            new CilEmitter().Emit(unit, semantics, outputPath);

            Assert.True(File.Exists(outputPath));

            using var assembly = AssemblyDefinition.ReadAssembly(outputPath);
            Assert.NotNull(assembly.EntryPoint);
            Assert.Equal("main", assembly.EntryPoint!.Name);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}
