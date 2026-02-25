# `language/` folder

Purpose: source language design notes and sample Lopex programs.

## What is here

- `datatypes`: mapping notes from Lopex surface types to CIL/.NET types.
- `syntax`: language syntax scratchpad/reference examples.
- `sample`: earlier feature examples.
- `main.lopex`: default source used by CLI when no file is passed.
- `mvp.lopex`: minimal program used for first end-to-end CIL emission.
- `match.lopex`: sample demonstrating `match` expressions.
- `struct.lopex`: sample demonstrating struct declarations and struct literals.
- `blazer-example`: experimental syntax sketch for UI/component-like code.

## Role in project

This folder acts as both:

1. Specification workspace (`datatypes`, `syntax`)
2. Compiler smoke-test inputs (`*.lopex`)

As language features evolve, this folder should stay synchronized with parser/type-checker/emitter support.
