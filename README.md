# FuncScript

FuncScript is a .NET-first expression language and runtime that lets you embed concise, functional business logic inside your applications. It ships with a parser, evaluator, formatter, CLI, unit tests, and optional SQL/GIS helpers, making it straightforward to evaluate custom scripts, map data, or compute derived values at runtime.

## Highlights
- Expression-oriented language with blocks, lambdas, pattern-like switches, string templates, lists, and key-value collections.
- Batteries-included standard library covering math, logic, text, list processing, OS helpers, HTML utilities, and JSON conversion.
- Ergonomic .NET embedding via `FuncScript.Engine.Evaluate(...)`, strongly-typed error reporting, and deterministic formatting utilities.
- Extensible through custom `IFsFunction` implementations or ad-hoc variables injected with `DefaultFsDataProvider`.
- Extra packages for SQL Server and NetTopologySuite geometries, plus an experimental JavaScript interpreter port.

## Repository Layout
- `FuncScript/` - core library targeting .NET 6.0; contains the parser, runtime, standard library, and data model.
- `FuncScript.Cli/` - minimal .NET 8 CLI (`funcscriptcli`) that evaluates expressions from the command line.
- `FuncScript.Example/` - interactive console sample that demonstrates the REPL-style usage of the runtime.
- `FuncScript.Sql/` - optional extension with SQL/geometry helpers and type normalization for ADO.NET results.
- `FuncScript.Test/` - NUnit-based test suite covering parsing, evaluation, errors, and formatting.
- `js-port/` - in-progress JavaScript port plus editor experiments and examples.

## Quick Start
### Prerequisites
Install the .NET 8 SDK (includes the .NET 6 tooling required by the library projects).

### Build Everything
```bash
dotnet restore FuncScript.sln
dotnet build FuncScript.sln
```

### Try the CLI
```bash
# Evaluate a simple expression
dotnet run --project FuncScript.Cli -- "(2 + 3) * 4"

# Evaluate a block with variables and built-in helpers
dotnet run --project FuncScript.Cli -- "{ rate:0.13; net:(gross)=>gross*(1-rate); eval net(12500); }"
```

### Embed in Your Application
```csharp
using System;
using System.Collections.Generic;
using FuncScript;
using FuncScript.Model;

var globals = new DefaultFsDataProvider(new List<KeyValuePair<string, object>>
{
    new KeyValuePair<string, object>("taxRate", 0.15),
    new KeyValuePair<string, object>("format", (Func<object,string>)(value => string.Format("{0:#,#0.00}", value)))
});

var expression = "{ net:(gross)=>gross*(1-taxRate); eval format(net(gross)); }";
var context = new ObjectKvc(new { gross = 5200 });
var result = FuncScript.Engine.Evaluate(new KvcProvider(context, globals), expression);

var output = new StringBuilder();
FuncScript.Engine.Format(output, result);
Console.WriteLine(output);
```
The runtime normalizes .NET values so that primitive types, lists, key-value collections, GUIDs, byte arrays, and even delegates translate seamlessly to script data.

## Language at a Glance
Script files are case-insensitive and expression-oriented. Common constructs include:
- **Blocks**: `{ items:[1,2,3]; eval Sum(items); }`
- **Lambdas**: `(x)=>x*x` or `(row, index)=>{ ... }`
- **String templates**: `f"Hello {name}!"`
- **Strings**: standard `'single'`/`"double"` literals or triple-quoted `"""multi-line"""` blocks when you need verbatim text
- **Collections**: Lists `[1, 2, 3]` and records `{name:"Ada", skills:["math","logic"]}`
- **Control**: `If`, `Switch`, `Case`, `fault` for structured errors
- **Functions**: `Map`, `Reduce`, `Filter`, `Distinct`, `Take`, `JoinText`, `Format`, `TicksToDate`, `point`, and many more
- **Operators**: `/` promotes to floating point if a remainder appears, while `div` performs integer-only division (accepting only 32/64-bit integers) and `==` is a synonym for `=` when comparing values
- **Comments**: both `// inline` and `/* multi-line */` styles are supported wherever whitespace is allowed

A new `eval` keyword now serves as the preferred way to emit the final value from a block. The historical `return` keyword continues to work for existing scripts but will be phased out in a future release.

A more elaborate example lives in `FuncScript/TestFormula.text`, where a payroll table is generated via mapping and HTML string templates.

## Extending the Runtime
Custom functions are regular .NET classes implementing `IFsFunction`:
```csharp
public class HexFunction : IFsFunction
{
    public string Symbol => "hex";
    public int MaxParsCount => 1;
    public CallType CallType => CallType.Prefix;
    public int Precedence => 0;

    public object Evaluate(IFsDataProvider context, IParameterList parameters)
    {
        var value = Convert.ToInt64(parameters.GetParameter(context, 0));
        return $"0x{value:X}";
    }

    public string ParName(int index) => "value";
}
```
Create a parameterless constructor, reference the assembly, and `DefaultFsDataProvider.LoadFromAssembly(...)` will auto-register the symbol (including optional aliases via `FunctionAliasAttribute`).

## SQL & GIS Helpers
`FuncScript.Sql` adds converters for `Sql*` types and NetTopologySuite geometries plus GIS helpers such as `point(x, y)`. Reference the project alongside the core library to normalize database values before exposing them to scripts.

## JavaScript Port (Experimental)
The `js-port/` folder contains a progressively feature-complete JavaScript interpreter that mirrors the .NET runtime. See `js-port/AGENTS.md` and `js-port/port-progress.md` for implementation notes and status.

## Testing
Run the NUnit suite to ensure language changes remain backwards compatible:
```bash
dotnet test FuncScript.sln
```

## Contributing
Issues and pull requests are welcome. If you plan substantial language or runtime changes, open an issue first so we can align on approach.

## Documentation
The language manual lives under `docs/` and is powered by [MkDocs Material](https://squidfunk.github.io/mkdocs-material/).

```bash
# install MkDocs dependencies into .venv-docs/
make docs-install

# preview changes locally with live reload
make docs-serve

# build the static site into ./site/
make docs-build
```

The published manual currently covers the language overview, practical examples, and a reference hub
for functions and types. Add new pages inside `docs/`, update the navigation tree in `mkdocs.yml`,
and the `Documentation` GitHub Actions workflow (`.github/workflows/docs.yml`) will keep building the
site for GitHub Pages whenever `main` receives documentation changes. The Vercel deployment uses the
same inputs but is now the canonical host for https://funcscript.org.

### Vercel Deployment
The repository includes `vercel.json` plus `scripts/vercel-build.sh`, which Vercel calls to
reproduce the GitHub Pages bundle. The script installs MkDocs, builds the React-based FuncScript
Studio demo with `PUBLIC_PATH=./`, runs `mkdocs build --clean --site-dir public`, and copies the demo
into `public/web/funcscript-studio/` so the static site can serve everything from the same origin.

To publish updates to https://funcscript.org:
1. Install the Vercel CLI (`npm i -g vercel`) and authenticate (`vercel login`).
2. Run `vercel link` from the repo root and select (or create) the FuncScript docs project.
3. Deploy with `vercel deploy --prod --confirm` (the CLI picks up `vercel.json` and runs the custom
   build).
4. Point the apex domain at Vercel by creating an `A` record for `funcscript.org` that targets
   `76.76.21.21`, and (optionally) a `CNAME` for `www.funcscript.org` that targets
   `cname.vercel-dns.com`.
5. Attach the domain inside Vercel (`vercel domains add funcscript.org`) so certificates are issued
   automatically.

Once linked, subsequent pushes to `main` can be auto-deployed by enabling Vercel's GitHub import, or
you can continue triggering manual `vercel deploy --prod` runs after reviewing changes locally.
FuncDraw lives at https://funcdraw.com as a separately built project, so the docs link to it instead
of building it locally.

## Maintainers
- Tewelde Ma. Tegegne (<teweldemat@gmail.com>)

## License
Released under the MIT License. See `LICENSE` for details.
