# Documentation development

Genix uses three complementary documentation layers:

1. C# XML comments on public APIs provide IntelliSense and machine-readable API text.
2. Markdown in `Documentation~` is the offline Unity package manual.
3. DocFX turns the Markdown manual and XML comments into a browsable HTML site with a generated API reference.

The manual follows the Diátaxis distinction between learning-oriented guides, task-oriented how-to material, factual reference, and conceptual explanation. Keep procedural instructions out of reference tables and keep implementation rationale out of the getting-started path.

## Build HTML documentation

DocFX is distributed as a .NET tool. On macOS, install the .NET SDK and then install DocFX globally:

```sh
dotnet tool install -g docfx
```

Homebrew can install the .NET SDK, but DocFX itself should be installed through `dotnet tool`. If the command is not on `PATH`, invoke it as `~/.dotnet/tools/docfx`.

Open the Unity project at least once after changing assembly definitions so Unity regenerates its `.csproj` files. Then run the package documentation build:

```sh
Tools/Documentation/build.sh
```

The generated manual is written to `Documentation~/_site`; generated API metadata is written to `Documentation~/api`. Both are build output rather than authored package source. DocFX is optional for package users because Unity opens the Markdown manual directly from Package Manager.

The script compiles the Runtime, Editor.Common, Space Foundation integration, and main Editor assemblies with XML output enabled. DocFX reflects those assemblies and reads their side-by-side XML files. This assembly-based approach avoids requiring Mono-MSBuild to interpret Unity's generated projects on macOS. DocFX then writes YAML metadata and renders it together with the conceptual manual.

DocFX may report `InvalidAssemblyReference` warnings for optional or transitive Unity editor dependencies such as Burst or Microsoft Extensions assemblies. These warnings are acceptable when all Genix assemblies compile, the expected Genix API pages are generated, and their signatures render correctly. Missing `Genix.*` references or missing API pages are build failures and must not be ignored.

## Documentation standard

Use XML comments for contracts that a caller needs while writing code:

- `<summary>` states purpose or observable behavior rather than repeating the identifier.
- `<param>` explains units, accepted ranges, ownership, and `null` behavior when those matter.
- `<returns>` describes success conditions or the meaning of the returned value.
- `<exception>` lists intentionally thrown exceptions and their conditions.
- `<remarks>` captures lifecycle, caching, performance, or ordering constraints that do not fit in the summary.

Internal types and methods need comments only when an invariant, algorithm, cache lifetime, or ownership rule is not clear from the code. Inline comments should explain why a non-obvious step exists, not narrate individual statements.

## Coverage check

The compiler can reject missing or malformed XML comments on publicly visible members. Run the following commands from the Unity project directory; `%3B` is the MSBuild-escaped semicolon:

```sh
dotnet build Genix.Runtime.csproj --no-restore -t:Rebuild \
  -p:DocumentationFile=/tmp/Genix.Runtime.xml \
  -p:WarningsAsErrors=1591%3B1570%3B1587

dotnet build Genix.Editor.csproj --no-restore -t:Rebuild \
  -p:DocumentationFile=/tmp/Genix.Editor.xml \
  -p:WarningsAsErrors=1591%3B1570%3B1587
```

Apply the same check to `Genix.Editor.Common.csproj` and `Genix.SpaceFoundation.Editor.csproj`. `CS1591` catches missing public documentation, `CS1570` catches malformed XML, and `CS1587` catches comments that are not attached to a valid language element.

Treat the generated API pages as verification, not as the only developer guidance. Architectural rationale belongs in the conceptual documentation, while designer-facing behavior belongs in the settings reference and editor tooltips.
