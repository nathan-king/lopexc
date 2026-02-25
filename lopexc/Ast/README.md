# `lopexc/Ast/`

Purpose: abstract syntax tree definitions for parsed Lopex code.

## Current contents

- `AstNodes.cs`: declaration, statement, expression, and pattern nodes.

## What it models today

- Declarations: functions, variables, structs
- Statements: expression statements, variable statements, blocks
- Expressions: literals, identifiers, unary/binary, calls, member access, `if`, `match`, struct literals
- Patterns: wildcard and literal match patterns

## Why this exists

AST is the boundary between parser and semantic/type analysis, and later lowering/IL emission.
