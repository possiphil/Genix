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

Open a Unity project with the Genix packages installed at least once after changing assembly definitions so Unity regenerates its `.csproj` files. For an embedded package, run the documentation build directly. For a local or Git package, provide the host project explicitly:

```sh
UNITY_PROJECT=/absolute/path/to/unity-project \
  Packages/com.possiphil.genix/Documentation~/build.sh
```

The generated manual is written to `Documentation~/_site`; generated API metadata is written to `Documentation~/api`; staged assemblies and XML files are written to `Documentation~/.artifacts`. All three are ignored build output rather than authored package source. DocFX is optional for package users because Unity opens the Markdown manual directly from Package Manager.

The script compiles the Runtime, Editor.Common, Space Foundation integration, and main Editor assemblies with XML output enabled. DocFX reflects those assemblies and reads their side-by-side XML files. This assembly-based approach avoids requiring Mono-MSBuild to interpret Unity's generated projects on macOS. DocFX then writes YAML metadata and renders it together with the conceptual manual.

DocFX may report `InvalidAssemblyReference` warnings for optional or transitive Unity editor dependencies such as Burst or Microsoft Extensions assemblies. These warnings are acceptable when all Genix assemblies compile, the expected Genix API pages are generated, and their signatures render correctly. Missing `Genix.*` references or missing API pages are build failures and must not be ignored.

## Documentation standard

Microsoft's [C# XML documentation guidance](https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags) recommends documenting every publicly visible type and member. Genix applies that rule to package-owned public APIs and uses XML comments for contracts that a caller needs while writing code:

- `<summary>` states purpose or observable behavior rather than repeating the identifier.
- `<param>` explains units, accepted ranges, ownership, and `null` behavior when those matter.
- `<returns>` describes success conditions or the meaning of the returned value.
- `<exception>` lists intentionally thrown exceptions and their conditions.
- `<remarks>` captures lifecycle, caching, performance, or ordering constraints that do not fit in the summary.

Internal types and methods need comments only when an invariant, algorithm, cache lifetime, or ownership rule is not clear from the code. Inline comments should explain why a non-obvious step exists, not narrate individual statements.

The manual follows Unity's [recommended package layout](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-layout.html): contributor context stays in `README.md` and user documentation stays in `Documentation~`. Interface text and tooltips follow Apple's [writing guidance](https://developer.apple.com/design/human-interface-guidelines/writing) where it is compatible with Unity conventions: familiar terms, concise action-oriented labels, consistent language, and errors that explain the next corrective step. Domain terms remain technical when replacing them would make diagnostics ambiguous.

## Structural conventions

- Keep independent policies, providers, and result values in separate types. Do not place unrelated provider classes in one source file.
- Use responsibility-named partial files for a Unity window, inspector, or hot-path orchestrator only when all parts must share the same lifecycle and state. This avoids presenter indirection in IMGUI and virtual dispatch inside generation loops.
- Keep orchestration entry points short enough to expose their phases. Put geometry, deterministic ordering, persistence, rendering, and export mechanics behind narrowly named internal methods or collaborators.
- Preserve serialized field names and public contracts during structural refactors. Unity asset compatibility takes precedence over cosmetic renaming.
- Benchmark and evaluation runners must acquire editor-global state through the shared campaign session. New campaign code must not independently toggle profiling, lock assembly reloads, or restore scenes.
- Do not add interfaces to inner candidate or validation loops merely to reduce file length. Add an abstraction only when it establishes an ownership boundary or allows a genuinely replaceable policy.

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

Apply the same check to `Genix.Editor.Common.csproj`, `Genix.SpaceFoundation.Editor.csproj`, and the optional DevTools assemblies. `CS1591` catches missing public documentation, `CS1570` catches malformed XML, and `CS1587` catches comments that are not attached to a valid language element.

Treat the generated API pages as verification, not as the only developer guidance. Architectural rationale belongs in the conceptual documentation, while designer-facing behavior belongs in the settings reference and editor tooltips.
