# `lopexc/Lexer/`

Purpose: convert source text into tokens with line/column positions.

## Current contents

- `Token.cs`: token kinds and token record type
- `Lexer.cs`: scanner implementation

## Implemented behavior

- Keywords, identifiers, numeric/string/char literals
- Operators and punctuation (including multi-char forms)
- Line and block comments
- Source position tracking for diagnostics

## Next likely work

- Richer literal forms
- Better error recovery/diagnostic structure
