using lopexc.Lexer;

var source = """
             fn main() {
                 var a: i32 = 5;
                 var b: i32 = 4;
                 println(`Result {x}`);
                 if a > b
                    => a + b
             }
             
             var text: string = "Hello, world!";
             
             fn add(a: i32, b: i32) => a + b;
             
             """;

List<Token> tokens = LexerCore.Lex(source);
foreach (Token token in tokens)
    Console.WriteLine(token);