# Changelog

---

All notable changes to this project will be documented in this file.


## [1.0.2] - 2026-06-27

### FIXED

- Explorer Actions had white overlapping left margin gutter
### CHANGED

- Changed RestoreWorkspace Check box to match the rest of the styling
- Refactored WorkspaceExplorer header: replaced the Button + ContextMenu approach with a standard Menu control, making the "⋯" overflow dropdown visually and behaviorally consistent with the main menu bar (File, View, etc.).
- Updated right‑click context menus for files and folders to use the same popup styling as main dropdowns – now featuring rounded corners, proper background/border, and a uniform MenuItem appearance thanks to the global implicit style.
- Removed obsolete resources: ExplorerOverflowMenu, NoIconMenuItemStyle, and the associated button click handler.
- Added global ContextMenu and Separator styles to ensure consistent theming across all explorer menus.

### ADDED

---

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
- TODO comments now appear in bold in the editor with proper highlighting

### ADDED

- Added built in support for TODOs
- Added various new options in the top drop-downs
- Added settings for Font Family, Size, Tab Width, Color and much more


---

## [1.0.0] - 2026-05-14

*Initial release.*