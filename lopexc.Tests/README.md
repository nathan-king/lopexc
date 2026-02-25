# `lopexc.Tests/`

Purpose: automated tests for compiler behavior.

## Current test areas

- `LexerSnapshotTests.cs`: tokenization behavior
- `ParserSnapshotTests.cs`: AST shape and parse behavior
- `SemanticCheckerTests.cs`: semantic/type diagnostics
- `CilEmitterTests.cs`: assembly emission smoke tests

## Role in workflow

This suite is the primary regression safety net while adding language features incrementally.
