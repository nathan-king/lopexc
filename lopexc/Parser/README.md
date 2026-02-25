# `lopexc/Parser/`

Purpose: transform token stream into AST.

## Current contents

- `ParserCore.cs`: recursive-descent + precedence expression parser

## Implemented parse features

- Function declarations (expression body and block body)
- Variable declarations (`var`, `const`, `mut var`)
- Struct declarations
- Expressions: unary/binary, calls, member access, grouping
- `if` expressions
- `match` expressions (literal and wildcard patterns)
- Struct literals (`TypeName { field: expr, ... }`)

## Next likely work

- Enums, interfaces, impl blocks
- Loops and additional statement forms
- Better parse diagnostics and recovery
