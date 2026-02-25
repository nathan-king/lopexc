# `lopexc/Ir/`

Purpose: planned intermediate representation layer.

## Current status

Scaffold folder only. No concrete IR types yet.

## Intended role

- Provide a stable, typed representation between semantic analysis and IL emission.
- Simplify optimization and backend work.

## Next likely work

- Define MVP IR instruction set
- Add AST-to-IR lowering pass
- Switch IL emitter to consume IR
