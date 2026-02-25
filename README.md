# Lopex

Lopex is a statically typed language that targets .NET CIL.  
The goal is to be expression-oriented like Rust, while staying obviously .NET-native in its type model and interop.

## Current State

Lopex currently has a working end-to-end compiler slice:

1. Read `.lopex` source file
2. Lex to tokens
3. Parse to AST
4. Run semantic/type checks
5. Emit CIL `.dll` (MVP subset)
6. Run emitted assembly

### Implemented Today

- CLI commands: `check`, `build`, `run`
- Lexer with source position tracking and modern operator coverage
- Parser for:
  - function declarations
  - variable declarations
  - `if` expressions
  - `match` expressions (literal + `_` arms)
  - struct declarations
  - struct literals
- Semantic/type checker for:
  - primitive types (`i32`, `bool`, `string`, `char`, `()`)
  - function signatures and call checking
  - arithmetic/logical/comparison rules
  - `if` and `match` typing rules
  - struct literal field validation
- CIL emitter (Mono.Cecil) for MVP runtime subset
- Automated test suite for lexer, parser, semantics, and emitter

### Current Runtime Emission Limits

- Struct runtime emission is not implemented yet (frontend support exists)
- No loops, generics, modules, interfaces, enums, or advanced patterns in emitted code yet
- No full runtime library/stdlib yet

## Quick Start

From repo root:

```bash
./lopex check language/main.lopex
./lopex build language/mvp.lopex -o out/mvp.dll
./lopex run language/mvp.lopex
```

Examples live under `language/`.

## What We Need Next

1. Complete struct runtime support in IL emission
2. Add enums and richer pattern matching
3. Implement loops and more statement forms
4. Introduce typed IR and lowering layer (currently direct AST->IL for subset)
5. Expand type system (user-defined types, better inference, diagnostics with spans/codes)
6. Improve tooling (formatting, richer CLI ergonomics, docs, eventually LSP)

## Repository Guide

Detailed README files exist in each major folder:

- [`language/`](language/README.md)
- [`lopexc/`](lopexc/README.md)
- [`lopexc.Tests/`](lopexc.Tests/README.md)
- [`out/`](out/README.md)

Inside `lopexc/`, each compiler stage folder also has its own README.
