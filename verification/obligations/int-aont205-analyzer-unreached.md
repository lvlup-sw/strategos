# int-aont205-analyzer-unreached

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | A `DiagnosticDescriptor` that ships in the ontology analyzer package is reported by `OntologyDefinitionAnalyzer`, or it is not a compile-time control. |
| **Scope** | `OntologyDiagnostics.IngestedContributesToIntentOnly` (id AONT205) and `OntologyDefinitionAnalyzer.ReportDiagnostics`. Runtime builder/freeze AONT205 is a different composition. |
| **Consequence** | Consumers of `LevelUp.Strategos.Ontology.Generators` cannot get AONT205 at compile time. An ingested+intent fluent `Define()` that never calls `ApplyDelta` / `Build()` produces no AONT205. The id exists, is enabled-by-default Error, and is inert. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | An analyzer call-graph check: `ReportDiagnostics` (or any `ReportDiagnostic` site in the analyzer assembly) must construct `OntologyDiagnostics.IngestedContributesToIntentOnly`. |
| **Why not cheaper** | A static `DiagnosticDescriptor` field compiling is not a registration. Runtime `OntologyCompositionException` with `Id: "AONT205"` is a different root. |
| **Failure signal** | Nothing at compile time. Runtime throw only if the builder/freeze path runs. |
| **Rollback** | Revert the descriptor. Leaves runtime AONT205 in place. Removing an unused Error descriptor is source-compatible. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Is compile-time AONT205 intentional-but-deferred this wave? The descriptor is `DiagnosticSeverity.Error` and `isEnabledByDefault: true`. The package description still says "AONT001-AONT035".
- Did any generated analyzer (not `OntologyDefinitionAnalyzer`) report this id? `rg IngestedContributesToIntentOnly` finds only the two definition files.

**Confidence:** high.

## What led here

Production-path survey §5c and existing-proof P38. This wave retargets AONT205 to mechanical ingestion. The runtime invariant is wired. The Roslyn descriptor is not.

Competing explanation: `ReportDiagnostics` already reports `IngestedContributesToIntentOnly` under another helper. False. The symbol has no references outside its defining files.

## Composition

Descriptor: `OntologyDiagnostics.cs:355-361`, id `OntologyDiagnosticIds.IngestedContributesToIntentOnly` = `"AONT205"` (`OntologyDiagnosticIds.cs:63`). Error, enabled by default.

Analyzer entry: `OntologyDefinitionAnalyzer` calls `ReportDiagnostics` (`:215`, body `:1205-1223`). That method dispatches duplicate-object, object-type basic, link, action, postcondition, lifecycle, derived-property, interface-action, interface-no-implementors, cross-domain-link, and extension-point reporters. None references `IngestedContributesToIntentOnly`.

`rg IngestedContributesToIntentOnly` (whole tree, exclude bin/obj):

- `OntologyDiagnosticIds.cs:63`
- `OntologyDiagnostics.cs:355-356`
- survey notes

No `Diagnostic.Create(OntologyDiagnostics.IngestedContributesToIntentOnly, ...)` exists.

The descriptor still compiles into `Strategos.Ontology.Generators.dll`, packed at `analyzers/dotnet/cs` (`Strategos.Ontology.Generators.csproj:12-13`, `:27`). Capability is in the analyzer nupkg. The analyzer cannot fire it.

Runtime AONT205 **is** reached: `OntologyBuilder.ValidateIngestedIntentInvariant` (`OntologyBuilder.cs:263-276`) and freeze scan (`OntologyGraphBuilder.cs:494-496`) throw `OntologyCompositionException` with `Id: "AONT205"`. Those are builder/freeze, not `spc.ReportDiagnostic`.

Package description (`Strategos.Ontology.Generators.csproj:13`) still advertises "AONT001-AONT035". AONT205 is outside that range and unwired.

## Path tests reach that shipping does not

`AONT205Tests` / `IOntologyBuilderInvariantTests` drive `ApplyDelta` and `Build()`. No `Strategos.Ontology.Generators.Tests` case found that expects analyzer diagnostic `AONT205`.

## Why cheaper rungs fail

- **Rung 1:** descriptor is hand-authored, not generated from a report-site list.
- **Rung 2:** an unused `static readonly DiagnosticDescriptor` is legal.
- **Rung 4:** runtime tests prove the builder exception, not the analyzer.

## Failure scenario

A contributor writes `Define()` that would be ingested+intent if it went through `ApplyDelta`. They never freeze. The analyzer package is referenced. Build is clean. AONT205 Error never appears. The id looks like a compile-time sibling of AONT201–204 and is not.

## Code read (this revision)

- `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnostics.cs:355-361`
- `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs:63`
- `src/Strategos.Ontology.Generators/Analyzers/OntologyDefinitionAnalyzer.cs:215`, `:1205-1223`
- `src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj:12-13`, `:27`
- `src/Strategos.Ontology/Builder/OntologyBuilder.cs:263-276`
- `src/Strategos.Ontology/OntologyGraphBuilder.cs:477-504`

### Investigation Log

#### Is there a ReportDiagnostic site for IngestedContributesToIntentOnly?

- Read: `ReportDiagnostics` and every `rg` hit for the descriptor name and AONT205 in the generators project.
- Found: definition only. Runtime throw sites use the string `"AONT205"`, not the Roslyn descriptor.
- Not found: any analyzer `Diagnostic.Create` for this descriptor.
- Conclusion: compile-time AONT205 is unreached.
