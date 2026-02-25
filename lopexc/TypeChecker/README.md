# `lopexc/TypeChecker/`

Purpose: semantic analysis and type validation over AST.

## Current contents

- `SemanticChecker.cs`: symbol checks, type checks, and diagnostics collection

## Implemented checks

- Function signature collection and duplicate detection
- Scope-based identifier resolution
- Primitive type compatibility checks
- Call arity/type checks
- `if` and `match` type rules
- Struct declaration table and struct literal field validation
- Basic builtin registration (`println(string)`)

## Next likely work

- Stronger type identity for user-defined structs/enums
- Member access typing
- Better typed representation output for lowering/emission
- Richer diagnostics (codes, spans, hints)
