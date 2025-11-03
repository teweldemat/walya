# FuncScript JavaScript Port Overview
This folder contains the JavaScript port of the **.NET FuncScript interpreter**.  
## Implementation Details
Typed Values
- Type information is central to FuncScript’s internal execution model. In this JavaScript port, each value is represented as a tuple of **(type number, value)** to maintain full alignment with the .NET implementation.
- The JavaScript interpreter is entirely self-contained and has no external dependencies.

## Folder Structure
- **funcscript-js** — core implementation of the JavaScript FuncScript interpreter.  
- **funcscript-js-test** — unit tests for the interpreter.
- **funcscript-editor** — a React-based CodeMirror editor with FuncScript syntax highlighting.

## Porting approach
Begin from the `FuncScript.Core.FuncScriptParser.Parse(IFsDataProvider context, String exp, out ParseNode parseNode)` method and follow its call tree.  
Maintain a hierarchical dynamic task progress list in `js-port/port-progress.md`.
