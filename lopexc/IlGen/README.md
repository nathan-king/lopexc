# `lopexc/IlGen/`

Purpose: emit CIL assemblies from checked AST.

## Current contents

- `CilEmitter.cs`: Mono.Cecil-based MVP emitter

## Implemented emission

- Assembly/module/type creation
- Method emission for function declarations
- Arithmetic/logical/comparison operations
- Calls, `if`, `match` (MVP patterns)
- Local variables and return values
- Entrypoint wiring to `main`

## Current limitations

- Struct runtime emission not implemented yet
- No full lowering/IR boundary yet (subset is emitted directly from AST)

## Next likely work

- Emit struct types/constructors/field access
- Add explicit lowering step input instead of direct AST emission
