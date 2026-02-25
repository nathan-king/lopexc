# `lopexc/` folder

Purpose: the Lopex compiler implementation.

## Compiler pipeline status

Current implemented flow:

1. Lexer (`Lexer/`)
2. Parser (`Parser/`)
3. AST (`Ast/`)
4. Semantic checks (`TypeChecker/`)
5. MVP CIL emission (`IlGen/`)

## Key files

- `Program.cs`: CLI entrypoint (`check`, `build`, `run`).
- `lopexc.csproj`: compiler project definition.

## Subfolder map

Each stage folder contains its own README:

- `Ast/`
- `Driver/`
- `IlGen/`
- `Ir/`
- `Lexer/`
- `Lowering/`
- `Parser/`
- `TypeChecker/`
- `Types/`
