# Changelog

---

All notable changes to this project will be documented in this file.

## [1.0.1] - 2026-06-08

### FIXED

- Fixed operator lookahead logic in Lua lexer.
- Fixed Luau long comment parsing to support multi-level bracket delimiters.
- Fixed Long comment termination now correctly supports both `]]` and `--]]` as valid closing sequences.
- Fixed Long comment parsing no longer relies on strict bracket matching only, improving resilience to non-standard but commonly used comment endings.
- Fixed Improved long comment scanning logic to prevent incorrect parsing when encountering malformed or mixed comment terminators.
- Fixed hightlight not properly applying on syntax
- Fixed layer order so memebers color dont get wiped out

### CHANGED

- Changed Color scheme of highlight


### ADDED

- Added built in support for TODOs
- Added various new options in the top drop-downs
- Added settings for Font Family, Size, Tab Width, Color and much more

---

## [1.0.0] - 2026-05-14

*Initial release.*