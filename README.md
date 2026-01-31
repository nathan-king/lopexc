# Lopex 🦊

**Lopex** is a statically typed programming language targeting **.NET (CIL)**, designed to combine:

* the ergonomics, tooling, and ecosystem of C# and .NET
* safety and correctness principles popularised by Rust, including:
  * explicit handling of absence with Option<T> instead of implicit nulls 
  * Result<T, E> for structured, predictable error handling 
  * expression-oriented control flow (match, if as expressions)
* a concise, expression-first syntax that reduces boilerplate without sacrificing clarity

Lopex compiles directly to **Common Intermediate Language (CIL)**, enabling seamless interoperability with the full .NET ecosystem.

## Goals

Lopex is designed with the following principles:

- **First-class .NET interop**  
  Full access to the .NET standard library and existing C# libraries.

- **Expression-oriented syntax**  
  Control flow constructs return values (`if`, `match`, etc.).

- **Minimal ceremony**  
  Fewer keywords, less boilerplate, readable defaults.

- **Predictable semantics**  
  No magic. Clear evaluation rules and explicit types where it matters.

- **Modern language features**  
  Pattern matching, strong typing, value semantics, and safe defaults.

---

## Example

```lopex
fn main() {
    var a: i32 = 5;
    var b: i32 = 4;

    if a > b =>
        println(`Result {a + b}`);
}
```

---

## Language Features

### Core
- Static typing
- Type inference (where unambiguous)
- Functions as first-class values
- Expression-based `if` and `match`
- Immutable-by-default values (configurable)

### Control Flow
- `if` expressions
- `match` expressions with value and condition matching
- Block expressions

```lopex
match x {
    0 => 0,
    x < 0 => -1,
    _ => 1
}
```

### Data Types
- Primitive numeric types (`i32`, etc.)
- Strings
- Structs
- Interfaces + implementations

### Interop
- Calls into .NET APIs
- Uses CIL as the compilation target
- Compatible with async/await via .NET tasks

## Compiler Architecture

The Lopex compiler is written in **C#** and follows a traditional pipeline:

```
Source
  ↓
Lexer
  ↓
Parser
  ↓
AST
  ↓
Type Checker
  ↓
IR
  ↓
CIL Emission
```

## Current Status

**Early-stage, actively developed**

### Implemented
- Lexer
- Token definitions
- Source position tracking

### In Progress
- Parser (AST generation)
- Expression parsing
- IR design

### Planned
- Type checking
- IL emission
- Minimal standard library
- CLI tooling

## Why Lopex?

Lopex explores a space not fully covered by C#:

- More **expression-oriented**
- Less ceremony
- Stronger pattern matching
- Designed for clarity first

It is not intended to replace C#, but to complement it.

## Name

**Lopex** is inspired by the fox, a symbol of adaptability and intelligence, and references multi-tailed fox legends from Japanese folklore.

## License

MIT